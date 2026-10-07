#nullable enable
using Il2Cpp;
using Il2CppScheduleOne.Cartel;
using MelonLoader;
using VICE.Core;

namespace VICE.Syndicate;

internal static class Difficulty
{
    private static MelonPreferences_Entry<bool> Enabled = null!;
    private static MelonPreferences_Entry<float> Cooldown = null!, AmbushRadius = null!;

    internal static void Initialize(MelonPreferences_Category config, HarmonyLib.Harmony harmony)
    {
        Enabled = ModSupport.MigratedEntry(config, "RivalGang", true, "More frequent hostile cartel activities and a wider player ambush detection area on the host.", "PersonalBetterPolice");
        Cooldown = ModSupport.MigratedEntry(config, "GangCooldownMultiplier", 0.5f, "Multiply new native global/regional cooldowns while hostile (0.25 to 1); minimum one game hour.", "PersonalBetterPolice");
        AmbushRadius = ModSupport.MigratedEntry(config, "GangAmbushRadiusMultiplier", 1.5f, "Multiply configured ambush detection radii while hostile (1 to 2); native spawn points and safety checks still apply.", "PersonalBetterPolice");
        var self = typeof(Difficulty);
        RemoveLegacyHooks(harmony);
        // Both cooldown generators have distinct native bodies. Shared constant
        // predicates such as CanNewActivityBegin must never be detoured.
        ModSupport.Patch(harmony, typeof(CartelActivities), "GetNewCooldown", self, postfix: nameof(ShorterCooldown));
        ModSupport.Patch(harmony, typeof(CartelRegionActivities), "GetNewCooldown", self, postfix: nameof(ShorterCooldown));
        ModSupport.Patch(harmony, typeof(Ambush), "MinPassed", self,
            prefix: nameof(BeforeAmbushCheck), finalizer: nameof(AfterAmbushCheck));
    }

    internal static void RemoveLegacyHooks(HarmonyLib.Harmony harmony)
    {
        // Transfer only legacy cartel hooks; Personal Better Police can retain its police features.
        foreach (var original in new[] { HarmonyLib.AccessTools.Method(typeof(CartelActivities), "GetNewCooldown"), HarmonyLib.AccessTools.Method(typeof(CartelRegionActivities), "GetNewCooldown"), HarmonyLib.AccessTools.Method(typeof(Ambush), "MinPassed") })
        {
            var info = HarmonyLib.Harmony.GetPatchInfo(original);
            if (info == null) continue;
            foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers).ToArray())
                if (patch.PatchMethod.DeclaringType?.FullName == "PersonalMods.BetterPolice.RivalGang") harmony.Unpatch(original, patch.PatchMethod);
        }
    }

    private static bool Active
    {
        get
        {
            if (!SyndicateMod.registered || !Enabled.Value) return false;
            var cartel = Il2CppScheduleOne.Cartel.Cartel.Instance;
            return cartel && cartel.IsServerInitialized && cartel.Status == ECartelStatus.Hostile;
        }
    }

    private static void ShorterCooldown(ref int __result)
    {
        try { if (Active) __result = GangTuning.Cooldown(__result, Cooldown.Value); }
        catch (Exception error) { ModSupport.WarnOnce("gang:cooldown", $"Gang cooldown adjustment unavailable: {error.Message}"); }
    }

    private static void BeforeAmbushCheck(Ambush __instance, out List<RadiusSnapshot>? __state)
    {
        __state = null;
        try
        {
            if (!Active || !__instance || !__instance.IsActive || !__instance._regionActivities) return;
            var locations = __instance._regionActivities.AmbushLocations;
            if (locations is null) return;
            __state = new();
            var seen = new HashSet<IntPtr>();
            foreach (var location in locations)
            {
                if (!location || !seen.Add(location.Pointer)) continue;
                var original = location.DetectionRadius;
                __state.Add(new(location, original));
                location.DetectionRadius = GangTuning.Radius(original, AmbushRadius.Value);
            }
        }
        catch (Exception error) { ModSupport.WarnOnce("gang:ambush", $"Additional gang ambush reach unavailable: {error.Message}"); }
    }

    private static Exception? AfterAmbushCheck(Exception? __exception, List<RadiusSnapshot>? __state)
    {
        // Restore even if a native exception occurs, or another mod skips the
        // original. No persistent edits to scene objects or activity lists.
        if (__state is not null)
            foreach (var saved in __state)
            {
                try { if (saved.Location) saved.Location.DetectionRadius = saved.Radius; }
                catch (Exception error) { ModSupport.WarnOnce("gang:restore", $"Gang detection radius restore failed: {error.Message}"); }
            }
        return __exception;
    }

    private sealed record RadiusSnapshot(CartelAmbushLocation Location, float Radius);
}
