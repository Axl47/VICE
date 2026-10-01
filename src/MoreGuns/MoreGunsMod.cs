using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Trash;
using Il2CppScheduleOne.UI.Shop;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;
using Avatar = Il2CppScheduleOne.AvatarFramework.Avatar;

[assembly: MelonInfo(typeof(PersonalMods.MoreGuns.MoreGunsMod), "Personal More Guns", "0.1.0", "Axl47; original assets by Voidane")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.MoreGuns;

public sealed class MoreGunsMod : MelonMod
{
    internal static MelonPreferences_Entry<bool> Enabled = null!;
    private static bool Registering;

    public override void OnInitializeMelon()
    {
        var category = MelonPreferences.CreateCategory("PersonalMoreGuns");
        Enabled = ModSupport.Entry(category, "Enabled", true, "Offer custom guns and enable their automatic firing. Keep the DLL installed while guns remain in saves.");
        WeaponAssets.Options = new[]
        {
            new GunOptions("ak47", "AK47", 15000, 1000, 70, 0.1f, 30, 0),
            new GunOptions("minigun", "MiniGun", 75000, 10000, 80, 0.05f, 400, 2)
        };
        var self = typeof(MoreGunsMod);
        ModSupport.Patch(HarmonyInstance, typeof(Registry), "Awake", self, postfix: nameof(RegistryReady));
        ModSupport.Patch(HarmonyInstance, typeof(Registry), "Start", self, postfix: nameof(RegistryReady));
        ModSupport.Patch(HarmonyInstance, typeof(Registry), "_GetItem", self, prefix: nameof(BeforeLookup));
        ModSupport.Patch(HarmonyInstance, typeof(ShopInterface), "Awake", self, prefix: nameof(BeforeShopAwake));
        ModSupport.Patch(HarmonyInstance, typeof(ShopInterface), "Start", self, postfix: nameof(AfterShopStart));
        ModSupport.Patch(HarmonyInstance, typeof(ShopInterface), "Open", self, prefix: nameof(BeforeShopOpen));
        ModSupport.Patch(HarmonyInstance, typeof(DialogueController_ArmsDealer), "Awake", self, postfix: nameof(DealerReady));
        ModSupport.Patch(HarmonyInstance, typeof(DialogueController_ArmsDealer), "Start", self, postfix: nameof(DealerReady));
        ModSupport.Patch(HarmonyInstance, typeof(DialogueController_ArmsDealer), "ModifyChoiceList", self, prefix: nameof(DealerReady));
        ModSupport.Patch(HarmonyInstance, typeof(Avatar), "SetEquippable", self, prefix: nameof(EquipAvatar));
        ModSupport.Patch(HarmonyInstance, typeof(TrashManager), "Start", self, prefix: nameof(TrashReady));
        ModSupport.Patch(HarmonyInstance, typeof(TrashManager), "GetTrashPrefab", self, prefix: nameof(TrashReady));
        GunInput.Initialize(HarmonyInstance);
    }

    public override void OnLateInitializeMelon()
    {
        WeaponAssets.Prepare(Registry.Instance);
        Register(Registry.Instance);
    }
    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Main") Register(Registry.Instance);
    }

    private static void RegistryReady(Registry __instance) => Register(__instance);
    private static void BeforeLookup(Registry __instance, string ID)
    {
        if (ID is "ak47" or "minigun" or "ak47mag" or "minigunmag") Register(__instance);
    }

    private static void Register(Registry? registry)
    {
        if (Registering || registry is null || !registry) return;
        Registering = true;
        try
        {
            WeaponAssets.Prepare(registry);
            foreach (var gun in WeaponAssets.Guns.Values)
            {
                Add(gun.Gun); Add(gun.Magazine);
            }
            void Add(ItemDefinition item)
            {
                var hash = Registry.GetHash(item.ID);
                if (registry.ItemDictionary is not null && registry.ItemDictionary.ContainsKey(hash)) return;
                registry.AddToRegistry(item);
                MelonLogger.Msg($"Registered {item.ID} in the current item registry.");
            }
        }
        catch (Exception error) { ModSupport.WarnOnce("guns:registry", $"Weapon registration will retry: {error.Message}"); }
        finally { Registering = false; }
    }

    private static void BeforeShopAwake(ShopInterface __instance) => AddListings(__instance, false);
    private static void AfterShopStart(ShopInterface __instance) => AddListings(__instance, true);
    private static void BeforeShopOpen(ShopInterface __instance) => AddListings(__instance, true);
    private static void AddListings(ShopInterface shop, bool initialized)
    {
        if (!Enabled.Value || !shop || shop.gameObject.name != "ArmsDealerInterface" || shop.Listings is null) return;
        try
        {
            Register(Registry.Instance);
            foreach (var gun in WeaponAssets.Guns.Values) { Add(gun.Gun); Add(gun.Magazine); }
            if (initialized) shop.RefreshShownItems();
            void Add(IntegerItemDefinition definition)
            {
                if (shop.GetListing(definition.ID) is not null) return;
                var listing = new ShopListing
                {
                    name = definition.Name, Item = definition, LimitedStock = false,
                    OverridePrice = false, CanBeDelivered = false, EnforceMinimumGameCreationVersion = false
                };
                if (initialized) listing.Initialize(shop);
                shop.Listings.Add(listing);
                if (initialized) shop.CreateListingUI(listing);
                MelonLogger.Msg($"Arms dealer now sells {definition.Name} (${definition.BasePurchasePrice}).");
            }
        }
        catch (Exception error) { ModSupport.WarnOnce("guns:shop", $"Gun listings will retry when the shop opens: {error.Message}"); }
    }

    private static void DealerReady(DialogueController_ArmsDealer __instance)
    {
        if (!Enabled.Value || !__instance) return;
        try
        {
            Register(Registry.Instance);
            foreach (var gun in WeaponAssets.Guns.Values)
            {
                Add(__instance.RangedWeapons, gun.Gun); Add(__instance.Ammo, gun.Magazine);
                Add(__instance.allWeapons, gun.Gun); Add(__instance.allWeapons, gun.Magazine);
            }
            static void Add(Il2CppSystem.Collections.Generic.List<DialogueController_ArmsDealer.WeaponOption>? list, IntegerItemDefinition item)
            {
                if (list is null) return;
                foreach (var option in list) if (option.Item?.ID == item.ID) return;
                list.Add(new DialogueController_ArmsDealer.WeaponOption { Item = item, IsAvailable = true, NotAvailableReason = "" });
            }
        }
        catch (Exception error) { ModSupport.WarnOnce("guns:dialogue", $"Arms dealer dialogue unavailable: {error.Message}"); }
    }

    private static bool EquipAvatar(Avatar __instance, string assetPath, ref AvatarEquippable __result)
    {
        if (assetPath is null || !WeaponAssets.AvatarPrefabs.TryGetValue(assetPath, out var prefab)) return true;
        GameObject? created = null;
        try
        {
            created = Object.Instantiate(prefab);
            var equippable = created.GetComponent<AvatarEquippable>() ?? throw new InvalidOperationException("Missing avatar equippable.");
            __instance.CurrentEquippable?.Unequip();
            __instance.CurrentEquippable = equippable;
            equippable.Equip(__instance);
            __result = equippable;
            return false;
        }
        catch (Exception error)
        {
            if (created) Object.Destroy(created);
            ModSupport.WarnOnce($"guns:avatar:{assetPath}", $"Custom avatar equipping failed: {error.Message}");
            return true;
        }
    }

    private static void TrashReady(TrashManager __instance)
    {
        try
        {
            Register(Registry.Instance);
            var prefabs = __instance.TrashPrefabs?.ToList() ?? new List<TrashItem>();
            var changed = false;
            foreach (var gun in WeaponAssets.Guns.Values)
            {
                if (gun.MagazineTrash is not { } trash || !trash || prefabs.Any(x => x.ID == trash.ID)) continue;
                prefabs.Add(trash); changed = true;
            }
            if (changed) __instance.TrashPrefabs = new Il2CppReferenceArray<TrashItem>(prefabs.ToArray());
        }
        catch (Exception error) { ModSupport.WarnOnce("guns:trash", $"Magazine trash registration failed: {error.Message}"); }
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Main") GunInput.Reset();
    }
}
