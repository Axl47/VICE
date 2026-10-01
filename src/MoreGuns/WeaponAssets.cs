using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Trash;
using Il2CppScheduleOne.UI.Shop;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.MoreGuns;

internal sealed class GunOptions
{
    internal string Id { get; }
    internal string Name { get; }
    internal MelonPreferences_Entry<float> Price, MagazinePrice, Damage, FireInterval, WindupSeconds;
    internal MelonPreferences_Entry<int> MagazineSize;
    internal MelonPreferences_Entry<bool> RequireRank;

    internal GunOptions(string id, string name, float price, float magazinePrice, float damage, float interval, int rounds, float windup)
    {
        Id = id; Name = name;
        var category = MelonPreferences.CreateCategory($"PersonalMoreGuns_{id}");
        Price = ModSupport.Entry(category, "PurchasePrice", price, "Gun purchase price.");
        MagazinePrice = ModSupport.Entry(category, "MagazinePrice", magazinePrice, "Price of a full spare magazine.");
        Damage = ModSupport.Entry(category, "Damage", damage, "Damage of each bullet.");
        FireInterval = ModSupport.Entry(category, "FireInterval", interval, "Seconds between automatic shots.");
        MagazineSize = ModSupport.Entry(category, "MagazineSize", rounds, "Rounds in the gun and each spare magazine.");
        WindupSeconds = ModSupport.Entry(category, "WindupSeconds", windup, "Seconds holding aim before firing; zero disables windup.");
        RequireRank = ModSupport.Entry(category, "RequirePurchaseRank", false, "Use the original bundle's purchase rank.");
    }

    internal static float Safe(float value, float fallback, float minimum, float maximum)
        => float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}

internal sealed record CustomGun(GunOptions Options, IntegerItemDefinition Gun, IntegerItemDefinition Magazine,
    Equippable_RangedWeapon Viewmodel, GameObject AvatarPrefab, GameObject MagazineAvatarPrefab, TrashItem? MagazineTrash);

internal static class WeaponAssets
{
    private static Il2CppAssetBundle? Bundle;
    private static bool Preparing;
    private static readonly HashSet<string> Failed = new(StringComparer.OrdinalIgnoreCase);
    internal static readonly Dictionary<string, CustomGun> Guns = new(StringComparer.OrdinalIgnoreCase);
    internal static readonly Dictionary<string, GameObject> AvatarPrefabs = new(StringComparer.OrdinalIgnoreCase);
    internal static GunOptions[] Options = Array.Empty<GunOptions>();

    internal static void Prepare(Registry? registry)
    {
        if (Preparing || Guns.Count == Options.Length) return;
        Preparing = true;
        try
        {
            if (Bundle is null)
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Personal.MoreGuns.bundle")
                    ?? throw new InvalidOperationException("Private weapon bundle missing. Run scripts/Prepare-Assets.ps1 before building.");
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                Bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(memory.ToArray()))
                    ?? throw new InvalidOperationException("Unity could not load the original weapon bundle.");
            }
            // Clone a current definition so new beta fields have the game's own defaults.
            IEnumerable<ItemDefinition> definitions = registry is not null && registry
                ? registry.GetAllItems().ToArray() : Resources.FindObjectsOfTypeAll<IntegerItemDefinition>().Select(x => (ItemDefinition)x);
            IntegerItemDefinition? prototype = null;
            foreach (var definition in definitions)
            {
                var integer = definition.TryCast<IntegerItemDefinition>();
                if (integer is null || Options.Any(x => integer.ID == x.Id || integer.ID == x.Id + "mag")) continue;
                if (prototype is null) prototype = integer;
                if (integer.ID.Equals("m1911", StringComparison.OrdinalIgnoreCase)) { prototype = integer; break; }
            }
            if (prototype is null) return; // Retry when the native registry has finished initializing.
            foreach (var options in Options)
            {
                if (Guns.ContainsKey(options.Id) || Failed.Contains(options.Id)) continue;
                try { Build(options, prototype); }
                catch (Exception error)
                {
                    Failed.Add(options.Id);
                    ModSupport.WarnOnce($"guns:asset:{options.Id}", $"{options.Name} could not be prepared; other guns remain available: {error.Message}");
                }
            }
        }
        catch (Exception error) { ModSupport.WarnOnce("guns:bundle", error.Message); }
        finally { Preparing = false; }
    }

    private static T Required<T>(string path) where T : Object
        => Bundle!.LoadAsset<T>(path) ?? throw new InvalidOperationException($"Missing {typeof(T).Name}: {path}");

    private static void Build(GunOptions options, IntegerItemDefinition prototype)
    {
        var id = options.Id;
        var originalGun = Required<IntegerItemDefinition>($"assets/resources/weapons/{id}/{id}.asset");
        var originalMagazine = Required<IntegerItemDefinition>($"{id}_Magazine");
        var prefab = Required<GameObject>($"assets/resources/weapons/{id}/{id}_equippable.prefab");
        var viewmodel = prefab.GetComponent<Equippable_RangedWeapon>()
            ?? throw new InvalidOperationException("Weapon prefab has no compatible ranged weapon component.");
        var avatar = Required<GameObject>($"assets/resources/avatar/equippables/{id}.prefab");
        var magazineAvatar = Required<GameObject>($"assets/resources/weapons/{id}/magazine/{id}_magazine_avatarequippable.prefab");
        var avatarComponent = avatar.GetComponent<AvatarEquippable>() ?? throw new InvalidOperationException("Missing gun avatar component.");
        var magazineComponent = magazineAvatar.GetComponent<AvatarEquippable>() ?? throw new InvalidOperationException("Missing magazine avatar component.");
        var trashPrefab = Bundle!.LoadAsset<GameObject>($"assets/resources/weapons/{id}/magazine/{id}_Magazine_Trash.prefab");
        var trash = trashPrefab ? trashPrefab.GetComponent<TrashItem>() : null;
        var gun = MakeDefinition(prototype, originalGun, options.Id, options.Name, viewmodel,
            GunOptions.Safe(options.Price.Value, 15000, 1, 10_000_000), options);
        var magazine = MakeDefinition(prototype, originalMagazine, options.Id + "mag", options.Name + " Magazine", originalMagazine.Equippable,
            GunOptions.Safe(options.MagazinePrice.Value, 1000, 1, 10_000_000), options);
        var capacity = Math.Clamp(options.MagazineSize.Value, 1, 10000);
        gun.DefaultValue = capacity;
        magazine.DefaultValue = capacity;
        viewmodel.MagazineSize = capacity;
        viewmodel.Magazine = magazine;
        viewmodel.CanReload = true;
        viewmodel.ReloadType = Equippable_RangedWeapon.EReloadType.Magazine;
        viewmodel.ReloadTrash = trash;
        viewmodel.Damage = GunOptions.Safe(options.Damage.Value, 70, 1, 10000);
        viewmodel.FireCooldown = GunOptions.Safe(options.FireInterval.Value, 0.1f, 0.02f, 5f);
        viewmodel.Range = id == "ak47" ? 30 : 25;
        viewmodel.RayRadius = 0.05f;
        viewmodel.MinSpread = id == "ak47" ? 2 : 4;
        viewmodel.MaxSpread = id == "ak47" ? 6 : 8;
        viewmodel.ImpactForce = id == "ak47" ? 200 : 300;
        viewmodel.HeadshotMultiplier = 2;
        viewmodel.MustBeCocked = false;
        viewmodel.CockedByDefault = true;
        viewmodel.AutoCockAfterReload = true;
        viewmodel.MinAimFOVReduction = 10;
        viewmodel.MaxAimFOVReduction = 10;
        viewmodel.AccuracyDropPerShot = 0.1f;
        avatarComponent.AssetPath = $"Avatar/Equippables/{options.Name}";
        magazineComponent.AssetPath = $"Weapons/{id}/Magazine/{options.Name}_Magazine_AvatarEquippable";
        viewmodel.AvatarEquippable = avatarComponent;
        if (magazine.Equippable?.TryCast<Equippable_Viewmodel>() is { } magazineViewmodel) magazineViewmodel.AvatarEquippable = magazineComponent;
        Guns.Add(id, new CustomGun(options, gun, magazine, viewmodel, avatar, magazineAvatar, trash));
        AvatarPrefabs[avatarComponent.AssetPath] = avatar;
        AvatarPrefabs[magazineComponent.AssetPath] = magazineAvatar;
        MelonLogger.Msg($"Prepared {options.Name} with {capacity} rounds and compatible beta item definitions.");
    }

    private static IntegerItemDefinition MakeDefinition(IntegerItemDefinition prototype, IntegerItemDefinition legacy,
        string id, string name, Equippable equippable, float price, GunOptions options)
    {
        var result = Object.Instantiate(prototype);
        result.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        result.name = name;
        result.ID = id;
        result.Name = name;
        result.Description = id.EndsWith("mag") ? $"Full spare magazine for the {options.Name}." : $"Automatic {options.Name}. Hold aim to spin up the minigun.";
        result.Category = legacy.Category;
        result.legalStatus = legacy.legalStatus;
        result.Icon = legacy.Icon ? legacy.Icon : prototype.Icon;
        result.StackLimit = 1;
        result.EquippableData = null; // This bundle uses the game's supported legacy equipping path.
        result.Equippable = equippable;
        result.StoredItem = legacy.StoredItem ? legacy.StoredItem : prototype.StoredItem;
        result.BasePurchasePrice = price;
        result.RequiresLevelToPurchase = options.RequireRank.Value;
        result.RequiredRank = legacy.RequiredRank;
        result.ShopCategories = new Il2CppSystem.Collections.Generic.List<ShopListing.CategoryInstance>();
        result.ShopCategories.Add(new ShopListing.CategoryInstance { Category = EShopCategory.Tools });
        result.ResellMultiplier = 0.5f;
        return result;
    }
}
