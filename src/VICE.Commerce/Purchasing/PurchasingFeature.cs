using Il2CppInterop.Runtime;
using Il2CppScheduleOne.UI.Shop;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.Events;


namespace VICE.Commerce.Purchasing;

internal sealed record RestoredOrder(bool Loaded, string Message);

internal sealed class PurchasingFeature
{
    internal static MelonPreferences_Entry<bool> Enabled = null!, RememberLast = null!;
    internal static SavedOrders Orders = null!;
    private static readonly Dictionary<IntPtr, SavedPurchasePanel> Panels = new();
    private static readonly Dictionary<IntPtr, UnityAction> CompletionHandlers = new();
    private static readonly VICE.Core.SubscriptionScope Subscriptions = new();
    private static readonly Dictionary<IntPtr, (SavedOrder Order, float Time)> Pending = new();

    public void Initialize(HarmonyLib.Harmony harmony)
    {
        var category = MelonPreferences.CreateCategory("PersonalGasPurchases");
        Enabled = ModSupport.Entry(category, "Enabled", true, "Show saved shopping lists in both gas-marts.");
        RememberLast = ModSupport.Entry(category, "RememberLastPurchase", true, "Remember the last successfully completed gas-mart purchase.");
        Orders = new SavedOrders(Path.Combine(MelonEnvironment.UserDataDirectory, "PersonalGasPurchases", "orders.json"));
        if (Orders.ReadError is not null) MelonLogger.Warning($"Saved orders could not be read: {Orders.ReadError}");
        var self = typeof(PurchasingFeature);
        ModSupport.Patch(harmony, typeof(ShopInterface), "Open", self, postfix: nameof(ShopOpened));
        ModSupport.Patch(harmony, typeof(ShopInterface), "Close", self, postfix: nameof(ShopClosed));
        ModSupport.Patch(harmony, typeof(ShopInterface), "OnClose", self, postfix: nameof(ShopClosed));
        ModSupport.Patch(harmony, typeof(Cart), "Buy", self, prefix: nameof(BeforeBuy));
    }

    internal static bool IsGasMart(ShopInterface shop)
    {
        if (!shop) return false;
        static string Key(string? text) => new((text ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        var code = Key(shop.ShopCode);
        var name = Key(shop.ShopName);
        var objectName = Key(shop.gameObject.name);
        return code.StartsWith("gasmart", StringComparison.Ordinal) || name.StartsWith("gasmart", StringComparison.Ordinal)
            || objectName.StartsWith("gasstationinterface", StringComparison.Ordinal);
    }

    private static void ShopOpened(ShopInterface __instance)
    {
        if (!Enabled.Value || !IsGasMart(__instance) || !__instance.Cart) return;
        try
        {
            BindCompletion(__instance);
            if (!Panels.TryGetValue(__instance.Pointer, out var panel)) Panels.Add(__instance.Pointer, panel = new SavedPurchasePanel(__instance));
            panel.RefreshOrders();
            panel.Tick();
        }
        catch (Exception error) { ModSupport.WarnOnce("gas:ui", $"Saved purchase controls unavailable: {error.Message}"); }
    }

    private static void ShopClosed(ShopInterface __instance)
    {
        if (Panels.TryGetValue(__instance.Pointer, out var panel)) panel.Hide();
    }

    private static void BindCompletion(ShopInterface shop)
    {
        if (CompletionHandlers.ContainsKey(shop.Pointer)) return;
        if (shop.onOrderCompleted is null) throw new InvalidOperationException("The shop has no order-completion event.");
        var action = DelegateSupport.ConvertDelegate<UnityAction>((Action)(() => Completed(shop)))
            ?? throw new InvalidOperationException("Could not bind the shop's purchase-completion event.");
        shop.onOrderCompleted.AddListener(action);
        Subscriptions.Add(() =>
        {
            if (shop && shop.onOrderCompleted is not null) shop.onOrderCompleted.RemoveListener(action);
        });
        CompletionHandlers.Add(shop.Pointer, action);
    }

    private static void BeforeBuy(Cart __instance)
    {
        if (!Enabled.Value || !RememberLast.Value || !__instance || !IsGasMart(__instance.Shop)) return;
        try
        {
            Pending.Remove(__instance.Shop.Pointer);
            BindCompletion(__instance.Shop);
            if (!__instance.CanCheckout(out _)) return;
            var order = Capture(__instance, "Last purchase");
            if (order is not null) Pending[__instance.Shop.Pointer] = (order, Time.realtimeSinceStartup);
        }
        catch (Exception error) { ModSupport.WarnOnce("gas:buy", $"Could not remember this purchase: {error.Message}"); }
    }

    private static void Completed(ShopInterface shop)
    {
        if (!Pending.Remove(shop.Pointer, out var pending) || !Enabled.Value || !RememberLast.Value) return;
        try
        {
            Orders.Remember(pending.Order);
            foreach (var panel in Panels.Values) panel.RefreshOrders();
        }
        catch (Exception error) { ModSupport.WarnOnce("gas:remember", $"Purchase completed, but its shopping list could not be saved: {error.Message}"); }
    }

    internal static SavedOrder? Capture(Cart cart, string name)
    {
        if (cart.cartDictionary is null || cart.cartDictionary.Count == 0) return null;
        var rows = new List<OrderItem>();
        foreach (var pair in cart.cartDictionary)
        {
            if (pair.Value <= 0 || pair.Key.Item is null) continue;
            rows.Add(new OrderItem(pair.Key.Item.ID, pair.Value, pair.Key.Item.Name));
        }
        return rows.Count == 0 ? null : SavedOrders.Create(name, rows);
    }

    internal static RestoredOrder Restore(ShopInterface shop, SavedOrder order)
    {
        if (!Enabled.Value || !shop || !shop.IsOpen || !shop.Cart) return new(false, "Open the gas-mart shop first.");
        var native = new Dictionary<string, ShopListing>(StringComparer.OrdinalIgnoreCase);
        var available = new Dictionary<string, AvailableOrderItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var listing in shop.Listings)
        {
            if (listing.Item is null) continue;
            var item = listing.Item;
            native.TryAdd(item.ID, listing);
            available.TryAdd(item.ID, new AvailableOrderItem(item.ID, item.Name, listing.ShouldShow() && item.GetIsUnlocked(),
                listing.IsUnlimitedStock ? null : listing.CurrentStock));
        }
        var plan = OrderPlanner.Plan(order, available, ShopInterface.MAX_ITEM_QUANTITY);
        if (!plan.Success) return new(false, plan.Error!); // Validate every item before clearing the current basket.
        var cart = shop.Cart;
        var before = new List<(ShopListing Listing, int Quantity)>();
        foreach (var pair in cart.cartDictionary) before.Add((pair.Key, pair.Value));
        try
        {
            cart.ClearCart();
            foreach (var row in plan.Items) cart.SetItemQuantity(native[row.ItemId], row.Quantity);
            RefreshCart(cart);
        }
        catch
        {
            cart.ClearCart();
            foreach (var row in before) cart.SetItemQuantity(row.Listing, row.Quantity);
            RefreshCart(cart);
            throw;
        }
        return new(true, cart.CanCheckout(out var reason) ? $"Loaded {order.Name}. Check out when ready." : $"Loaded {order.Name}. {reason}");
    }

    private static void RefreshCart(Cart cart) { cart.UpdateEntries(); cart.UpdateTotal(); cart.UpdateProblem(); }

    public void OnUpdate()
    {
        foreach (var panel in Panels.Values)
        {
            try { panel.Tick(); }
            catch (Exception error) { ModSupport.WarnOnce("gas:tick", $"Saved purchase UI update failed: {error.Message}"); }
        }
        foreach (var key in Pending.Where(x => Time.realtimeSinceStartup - x.Value.Time > 10f).Select(x => x.Key).ToArray()) Pending.Remove(key);
    }

    private static void Cleanup()
    {
        Subscriptions.Dispose();
        foreach (var panel in Panels.Values) { try { panel.Dispose(); } catch { } }
        Panels.Clear(); CompletionHandlers.Clear(); Pending.Clear();
    }

    public void OnSceneWasUnloaded(int buildIndex, string sceneName) { if (sceneName == "Main") Cleanup(); }
    public void OnApplicationQuit() => Cleanup();
}
