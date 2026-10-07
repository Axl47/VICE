using System.Collections;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using MelonLoader;
using PersonalMods.Algorithms;
using UnityEngine;


namespace VICE.Commerce.Trading;

internal sealed class TradingFeature
{
    internal static MelonPreferences_Entry<bool> Enabled = null!, AutoHandover = null!, SkipCustom = null!;
    private static MelonPreferences_Entry<bool> OptimizePrice = null!, IncreaseQuantity = null!, AutoCounter = null!, AutoSchedule = null!;
    private static MelonPreferences_Entry<float> MinimumChance = null!;
    private static MelonPreferences_Entry<EDealWindow> Window = null!;
    private static CounterofferInterface? OpenInterface;
    private static string OriginalTitle = "Counteroffer";
    private static bool Updating;
    private static int SceneGeneration;
    private static readonly HashSet<(IntPtr Customer, IntPtr Offer)> Queued = new();
    private static readonly Dictionary<IntPtr, InitialOffer> OriginalOffers = new();
    private sealed record InitialOffer(string ProductId, int Quantity, float Payment);

    public void Initialize(HarmonyLib.Harmony harmony)
    {
        var config = MelonPreferences.CreateCategory("PersonalHighballer");
        Enabled = ModSupport.Entry(config, "Enabled", true, "Enable customer deal features.");
        OptimizePrice = ModSupport.Entry(config, "PriceOptimization", true, "Find the highest estimated acceptable offer price.");
        MinimumChance = ModSupport.Entry(config, "MinimumSuccessProbability", 0.98f, "Minimum estimated counteroffer success probability, from 0 to 1.");
        IncreaseQuantity = ModSupport.Entry(config, "IncreaseQuantity", false, "Try up to 20 extra units within the game's order limit.");
        AutoCounter = ModSupport.Entry(config, "AutoCounteroffer", false, "Automatically send improved counteroffers for ordinary customers.");
        AutoSchedule = ModSupport.Entry(config, "AutoSchedule", false, "Automatically accept ordinary customer contracts in the configured time window.");
        Window = ModSupport.Entry(config, "AutoScheduleWindow", EDealWindow.Night, "Morning, Afternoon, Night or LateNight.");
        AutoHandover = ModSupport.Entry(config, "AutoHandover", true, "Fill and submit exact packaged deliveries using the native handover screen.");
        SkipCustom = ModSupport.Entry(config, "SkipCustomNPCs", true, "Leave S1API and special customer contracts to their owning mod.");
        var self = typeof(TradingFeature);
        ModSupport.Patch(harmony, typeof(CounterofferInterface), "Open", self, postfix: nameof(CounterofferOpened));
        ModSupport.Patch(harmony, typeof(CounterofferInterface), "Close", self, postfix: nameof(CounterofferClosed));
        ModSupport.Patch(harmony, typeof(CounterofferInterface), "SetProduct", self, postfix: nameof(ProductChanged));
        ModSupport.Patch(harmony, typeof(CounterofferInterface), "ChangeQuantity", self, postfix: nameof(QuantityChanged), parameters: new[] { typeof(float) });
        ModSupport.Patch(harmony, typeof(CounterofferInterface), "ChangeQuantity", self, postfix: nameof(QuantityChanged), parameters: new[] { typeof(string) });
        ModSupport.Patch(harmony, typeof(AmountSelector), "SetAmount", self, postfix: nameof(PriceChanged));
        ModSupport.Patch(harmony, typeof(Customer), "OfferContract", self, postfix: nameof(Offered));
        ModSupport.Patch(harmony, typeof(Customer), "SetOfferedContract", self, postfix: nameof(Offered));
        HandoverAutomation.Initialize(harmony);
    }

    internal static bool CanHandle(Customer customer) => Enabled.Value && customer && customer.NPC && (!SkipCustom.Value || !CustomCustomerGuard.IsCustom(customer.NPC));
    internal static bool IsBusy => (SaveManager.Instance && SaveManager.Instance.IsSaving)
        || (LoadManager.Instance && (LoadManager.Instance.IsLoading || !LoadManager.Instance.IsGameLoaded));

    private static Customer? ResolveCustomer(CounterofferInterface ui)
    {
        if (ui.conversation is null) return null;
        foreach (var customer in Customer.UnlockedCustomers)
            if (customer && customer.NPC && customer.NPC.MSGConversation?.Pointer == ui.conversation.Pointer) return customer;
        return null;
    }

    private static InitialOffer? Snapshot(Customer customer)
    {
        if (OriginalOffers.TryGetValue(customer.Pointer, out var saved)) return saved;
        var info = customer.OfferedContractInfo;
        if (info?.Products?.entries is null || info.Products.entries.Count != 1) return null;
        var entry = info.Products.entries[0];
        if (entry.Quantity <= 0 || info.Payment <= 0) return null;
        saved = new InitialOffer(entry.ProductID, entry.Quantity, info.Payment);
        OriginalOffers[customer.Pointer] = saved;
        return saved;
    }

    private static float SpendLimit(Customer customer)
    {
        var days = new Il2CppSystem.Collections.Generic.List<EDay>();
        var relationship = customer.NPC.RelationData.NormalizedRelationDelta;
        customer.CustomerData.GetOrderDays(customer.CurrentAddiction, relationship, days);
        return customer.CustomerData.GetAdjustedWeeklySpend(relationship) / Math.Max(days.Count, 1) * 3f;
    }

    private static double Chance(Customer customer, ProductDefinition product, int quantity, float totalPrice, InitialOffer initial)
    {
        var original = Registry.GetItem<ProductDefinition>(initial.ProductId);
        if (!original || quantity <= 0) return 0;
        var value = Customer.GetValueProposition(original, initial.Payment / initial.Quantity);
        var counterValue = Customer.GetValueProposition(product, totalPrice / quantity);
        var enjoyment = customer.GetProductEnjoyment(product, StandardsMethod.GetCorrespondingQuality(customer.CustomerData.Standards));
        return PriceSearch.EstimateChance(value, counterValue, enjoyment, initial.Quantity, quantity,
            customer.CurrentAddiction, customer.NPC.RelationData.NormalizedRelationDelta);
    }

    private static (int Quantity, int Price)? BestOffer(Customer customer, ProductDefinition product, int quantity, int maximumQuantity, int maximumPrice)
    {
        var initial = Snapshot(customer);
        if (initial is null || quantity < 1 || !product || !customer.CustomerData) return null;
        var spend = SpendLimit(customer);
        if (!float.IsFinite(spend) || spend < 1) return null;
        var cap = (int)Math.Min(Math.Floor(spend), maximumPrice);
        var threshold = float.IsFinite(MinimumChance.Value) ? Math.Clamp(MinimumChance.Value, 0f, 1f) : 0.98f;
        (int Quantity, int Price)? best = null;
        var lastQuantity = IncreaseQuantity.Value ? Math.Min(maximumQuantity, quantity + 20) : quantity;
        for (var count = quantity; count <= lastQuantity; count++)
        {
            var fixedCount = count;
            var price = PriceSearch.HighestAcceptable(1, cap, threshold, dollars => Chance(customer, product, fixedCount, dollars, initial));
            if (price is not null && (best is null || price > best.Value.Price)) best = (count, price.Value);
        }
        return best;
    }

    private static void CounterofferOpened(CounterofferInterface __instance)
    {
        OpenInterface = __instance;
        OriginalTitle = __instance.TitleLabel ? __instance.TitleLabel.text : "Counteroffer";
        Optimize(__instance, false);
    }

    private static void CounterofferClosed()
    {
        if (OpenInterface is not null && OpenInterface && OpenInterface.TitleLabel) OpenInterface.TitleLabel.text = OriginalTitle;
        OpenInterface = null;
    }
    private static void ProductChanged(CounterofferInterface __instance) => Optimize(__instance, true);
    private static void QuantityChanged(CounterofferInterface __instance) => Optimize(__instance, true);
    private static void PriceChanged(AmountSelector __instance)
    {
        if (!Updating && OpenInterface is not null && OpenInterface && OpenInterface.PriceSelector?.Pointer == __instance.Pointer) Display(OpenInterface);
    }

    private static void Optimize(CounterofferInterface ui, bool allowDecrease)
    {
        if (!Enabled.Value || Updating || !ui || !ui.IsOpen) return;
        try
        {
            var customer = ResolveCustomer(ui);
            if (customer is null || !CanHandle(customer)) return;
            if (OptimizePrice.Value)
            {
                var best = BestOffer(customer, ui.selectedProduct, ui.quantity, ui.MaxQuantity, (int)CounterofferInterface.MaxPrice);
                if (best is not null && (allowDecrease || best.Value.Price > ui.PriceSelector.SelectedAmount))
                {
                    Updating = true;
                    ui.ChangeQuantity((float)(best.Value.Quantity - ui.quantity));
                    ui.PriceSelector.SetAmount(best.Value.Price);
                }
            }
            Display(ui);
        }
        catch (Exception error) { ModSupport.WarnOnce("highballer:offer", $"Offer optimization unavailable; keeping the native controls: {error.Message}"); }
        finally { Updating = false; }
    }

    private static void Display(CounterofferInterface ui)
    {
        var customer = ResolveCustomer(ui);
        if (customer is null || !CanHandle(customer) || !ui.TitleLabel || ui.quantity <= 0 || !ui.selectedProduct) return;
        var initial = Snapshot(customer);
        if (initial is null) return;
        var chance = Chance(customer, ui.selectedProduct, ui.quantity, ui.PriceSelector.SelectedAmount, initial);
        ui.TitleLabel.text = $"{OriginalTitle}\nEstimated success: {chance:P0} · Budget: ${SpendLimit(customer):N0}";
    }

    private static void Offered(Customer __instance, ContractInfo info)
    {
        if (info is null || !CanHandle(__instance)) return;
        if (!info.IsCounterOffer) { OriginalOffers.Remove(__instance.Pointer); Snapshot(__instance); }
        if (!AutoCounter.Value && !AutoSchedule.Value) return;
        if (Queued.Add((__instance.Pointer, info.Pointer))) MelonCoroutines.Start(AutomateOffer(__instance, info, SceneGeneration));
    }

    private static IEnumerator AutomateOffer(Customer customer, ContractInfo offer, int generation)
    {
        yield return new WaitForSeconds(1f);
        var deadline = Time.realtimeSinceStartup + 30f;
        while (IsBusy && Time.realtimeSinceStartup < deadline) yield return new WaitForSeconds(0.25f);
        if (IsBusy || generation != SceneGeneration || !customer || !CanHandle(customer) || customer.OfferedContractInfo?.Pointer != offer.Pointer) yield break;
        if (AutoCounter.Value && !offer.IsCounterOffer && offer.Products?.entries?.Count == 1)
        {
            var entry = offer.Products.entries[0];
            var product = Registry.GetItem<ProductDefinition>(entry.ProductID);
            var best = BestOffer(customer, product, entry.Quantity, Customer.MaxOrderQuantityPerProduct, (int)CounterofferInterface.MaxPrice);
            if (best is not null && best.Value.Price > offer.Payment)
            {
                customer.SendCounteroffer(product, best.Value.Quantity, best.Value.Price);
                // A new accepted offer queues its own scheduling. Never accept the stale original while waiting.
                yield break;
            }
        }
        if (AutoSchedule.Value && Enum.IsDefined(typeof(EDealWindow), Window.Value)) customer.PlayerAcceptedContract(Window.Value);
    }

    public void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Main") return;
        SceneGeneration++; OriginalOffers.Clear(); Queued.Clear(); OpenInterface = null;
        HandoverAutomation.Reset();
    }
}
