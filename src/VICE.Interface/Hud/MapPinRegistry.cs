using Il2CppScheduleOne.Map;
using UnityEngine;

namespace VICE.Interface.Hud;

internal static class MapPinRegistry
{
    private static readonly Dictionary<IntPtr, POI> Entries = new();
    private static POI[] Snapshot = Array.Empty<POI>();
    private static bool Dirty, Seeded;

    // A one-time seed also covers POIs enabled before our hooks were installed.
    // Subsequent runtime spawns/despawns use the native lifecycle notifications.
    internal static IEnumerable<POI> Current
    {
        get
        {
            if (!Seeded)
            {
                foreach (var poi in UnityEngine.Object.FindObjectsOfType<POI>()) Added(poi);
                Seeded = true;
            }
            if (Dirty) { Snapshot = Entries.Values.ToArray(); Dirty = false; }
            return Snapshot;
        }
    }

    internal static void Added(POI __instance)
    {
        if (!__instance) return;
        Entries[__instance.Pointer] = __instance;
        Dirty = true;
    }

    internal static void Removed(POI __instance)
    {
        if (__instance is not null && Entries.Remove(__instance.Pointer)) Dirty = true;
    }

    internal static void Clear()
    {
        Entries.Clear(); Snapshot = Array.Empty<POI>(); Dirty = Seeded = false;
    }
}
