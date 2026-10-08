using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.Networking;

// FishNet can spawn the donor's registered prefab on peers. Runtime children
// added to a local furniture definition are not part of that network prefab.
// Restore local presentation from the matching definition without replacing the
// network object, GUID, parent property, or authoritative placement state.
internal static class ReplicatedFurnitureVisuals
{
    private const string VisualName = "FurnitureVisual";

    internal static bool Ensure(BuildableItem item)
    {
        if (!item || item.ItemInstance is null) return false;
        if (item.transform.Find(VisualName)) return false;
        var definition = Il2CppScheduleOne.Registry.GetItem(item.ItemInstance.ID)?.TryCast<BuildableItemDefinition>();
        var template = definition?.BuiltItem;
        var source = template ? template!.transform.Find(VisualName) : null;
        if (!source || !template!.BoundingCollider)
            throw new InvalidOperationException("Local furniture visuals are unavailable for " + item.ItemInstance.ID + ".");

        var originalRenderers = item.GetComponentsInChildren<Renderer>(true);
        var visual = Object.Instantiate(source!.gameObject, item.transform);
        try
        {
            visual.name = VisualName;
            visual.transform.localPosition = source.localPosition;
            visual.transform.localRotation = source.localRotation;
            visual.transform.localScale = source.localScale;
            var meshes = new Il2CppSystem.Collections.Generic.List<MeshRenderer>();
            var outlines = new Il2CppSystem.Collections.Generic.List<GameObject>();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) outlines.Add(renderer.gameObject);
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true)) meshes.Add(renderer);
            if (meshes.Count == 0) throw new InvalidOperationException("The local furniture model has no meshes.");

            if (!item.BoundingCollider) item.BoundingCollider = item.gameObject.AddComponent<BoxCollider>();
            item.BoundingCollider.center = template.BoundingCollider.center;
            item.BoundingCollider.size = template.BoundingCollider.size;
            item.GameObjectsToCull = new Il2CppReferenceArray<GameObject>(new[] { visual });
            item.MeshesToCull = meshes;
            item.OutlineRenderers = outlines;
            if (item.MidAirCenterPoint && template.MidAirCenterPoint)
                item.MidAirCenterPoint.localPosition = template.MidAirCenterPoint.localPosition;
            foreach (var renderer in originalRenderers) if (renderer) renderer.enabled = false;
            visual.SetActive(!item.IsCulled);
            return true;
        }
        catch { visual.SetActive(false); Object.Destroy(visual); throw; }
    }
}
