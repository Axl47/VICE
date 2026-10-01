using Il2CppScheduleOne;
using Il2CppScheduleOne.Doors;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.UI.Shop;
using MelonLoader;
using PersonalMods;
using UnityEngine;

[assembly: MelonInfo(typeof(PersonalMods.Warehouse.WarehouseMod), "Personal Warehouse", "0.1.0", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.Warehouse;

public sealed class WarehouseMod : MelonMod
{
    private static MelonPreferences_Entry<bool> AlwaysOpen = null!, IgnoreUnlockRank = null!, SellStorage = null!;
    private static MelonPreferences_Entry<string> ShopIds = null!, ExtraStorageIds = null!;
    private static readonly HashSet<IntPtr> Prepared = new();

    public override void OnInitializeMelon()
    {
        var config = MelonPreferences.CreateCategory("PersonalWarehouse");
        AlwaysOpen = ModSupport.Entry(config, "AlwaysOpen", true, "Keep warehouse access open at all hours.");
        IgnoreUnlockRank = ModSupport.Entry(config, "IgnoreUnlockRank", true, "Allow access before the normal warehouse unlock rank, matching the old personal config.");
        SellStorage = ModSupport.Entry(config, "SellStorage", true, "Add the game's racks, storage cabinets and lockers to the warehouse furniture vendor.");
        ShopIds = ModSupport.Entry(config, "StorageShopIds", "warehouse,oscar,darkmarket,warehouseinterface,oscarshop,oscarinterface,oscarsstore", "Exact shop code, shop name or object name matches, separated by commas.");
        ExtraStorageIds = ModSupport.Entry(config, "ExtraStorageItemIds", "", "Additional existing registry item IDs to sell, separated by commas.");
        var self = typeof(WarehouseMod);
        ModSupport.Patch(HarmonyInstance, typeof(DarkMarket), "ShouldBeOpen", self, postfix: nameof(OpenResult));
        ModSupport.Patch(HarmonyInstance, typeof(DarkMarketAccessZone), "GetIsOpen", self, postfix: nameof(OpenResult));
        ModSupport.Patch(HarmonyInstance, typeof(DarkMarketRollerDoors), "CanOpen", self, postfix: nameof(OpenResult));
        ModSupport.Patch(HarmonyInstance, typeof(DarkMarket), "get_Unlocked", self, postfix: nameof(UnlockedResult));
        ModSupport.Patch(HarmonyInstance, typeof(ShopInterface), "Awake", self, prefix: nameof(BeforeShopAwake));
        ModSupport.Patch(HarmonyInstance, typeof(ShopInterface), "Start", self, postfix: nameof(AfterShopStart));
        ModSupport.Patch(HarmonyInstance, typeof(ShopInterface), "Open", self, prefix: nameof(BeforeShopOpen));
    }

    private static void OpenResult(ref bool __result)
    {
        if (AlwaysOpen.Value) __result = true;
    }

    private static void UnlockedResult(ref bool __result)
    {
        if (AlwaysOpen.Value && IgnoreUnlockRank.Value) __result = true;
    }

    private static string Key(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static bool IsWarehouseShop(ShopInterface shop)
    {
        var ids = ShopIds.Value.Split(',').Select(Key).Where(x => x.Length > 0).ToHashSet();
        return ids.Contains(Key(shop.ShopCode ?? "")) || ids.Contains(Key(shop.ShopName ?? "")) || ids.Contains(Key(shop.gameObject.name));
    }

    private static void BeforeShopAwake(ShopInterface __instance) => AddStorage(__instance, false);
    private static void AfterShopStart(ShopInterface __instance) => AddStorage(__instance, true);
    private static void BeforeShopOpen(ShopInterface __instance) => AddStorage(__instance, true);

    private static void AddStorage(ShopInterface shop, bool initialized)
    {
        if (!SellStorage.Value || !shop || !IsWarehouseShop(shop) || shop.Listings is null) return;
        try
        {
            var registry = Registry.Instance;
            if (!registry) return; // Registry may not exist during the shop's Awake; retry at Start/Open.
            var extra = ExtraStorageIds.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = 0;
            foreach (var definition in registry.GetAllItems())
            {
                var item = definition.TryCast<StorableItemDefinition>();
                if (item is null || !item || item.BasePurchasePrice <= 0) continue;
                var text = Key($"{item.ID} {item.Name}");
                if (!extra.Contains(item.ID) && !IsStorageFurniture(text)) continue;
                if (shop.GetListing(item.ID) is not null) continue;
                var listing = new ShopListing
                {
                    name = item.Name, Item = item, LimitedStock = false,
                    OverridePrice = false, CanBeDelivered = true, EnforceMinimumGameCreationVersion = false
                };
                if (initialized) listing.Initialize(shop);
                shop.Listings.Add(listing);
                if (initialized) shop.CreateListingUI(listing);
                added++;
                MelonLogger.Msg($"Warehouse now sells {item.Name} ({item.ID}) at its native price ${item.BasePurchasePrice}.");
            }
            if (added > 0 && initialized) shop.RefreshShownItems();
            if (Prepared.Add(shop.Pointer))
                MelonLogger.Msg($"Warehouse storage catalog prepared: code='{shop.ShopCode}', name='{shop.ShopName}', added={added}.");
        }
        catch (Exception error)
        {
            ModSupport.WarnOnce("warehouse:catalog", $"Warehouse catalog update failed; retrying on next open: {error.Message}");
        }
    }

    internal static bool IsStorageFurniture(string normalizedName)
        => normalizedName.Contains("locker") || normalizedName.Contains("cabinet")
           || normalizedName.Contains("storagerack") || normalizedName.Contains("storageshelf")
           || normalizedName.Contains("storageunit") || normalizedName.Contains("storagecloset");

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Main") Prepared.Clear();
    }
}
