using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalMods.LSDPreview;

// One view ray per frame; no world-wide enumeration, material copies or texture
// uploads. Shared native materials retain their shaders, UVs and full textures.
internal sealed class MaterialColorFlow : IDisposable
{
    private sealed class Entry
    {
        internal Material Material = null!;
        internal TintLease Lease = null!;
        internal int Property, Seed;
        internal float Born, Seen;
    }

    private static readonly Vector2[] Samples =
    {
        new(0.5f, 0.5f), new(0.18f, 0.22f), new(0.82f, 0.22f),
        new(0.18f, 0.5f), new(0.82f, 0.5f), new(0.5f, 0.22f),
        new(0.18f, 0.78f), new(0.82f, 0.78f), new(0.5f, 0.78f)
    };
    private const int Capacity = 48, UpdatesPerFrame = 8;
    private readonly Camera Camera;
    private readonly List<Entry> Entries = new();
    private int SampleIndex, UpdateIndex;
    private bool Disposed, Faulted;

    internal MaterialColorFlow(Camera camera) => Camera = camera;

    internal void Tick(float elapsed, float amount)
    {
        if (Disposed || Faulted || !Camera) return;
        try
        {
            if (amount <= 0.001f) { Restore(); return; }
            Discover(elapsed);
            var updates = Math.Min(UpdatesPerFrame, Entries.Count);
            for (var count = 0; count < updates && Entries.Count > 0; count++)
            {
                if (UpdateIndex >= Entries.Count) UpdateIndex = 0;
                var entry = Entries[UpdateIndex];
                if (!entry.Material || elapsed - entry.Seen >= 12)
                {
                    Restore(entry);
                    Entries.RemoveAt(UpdateIndex);
                    continue;
                }
                var current = Read(entry.Material, entry.Property);
                entry.Lease.Observe(current);
                var fade = TripTimeline.Smooth((elapsed - entry.Born) / 0.8f)
                    * (1 - TripTimeline.Smooth((elapsed - entry.Seen - 9) / 3));
                var tint = ColorBreathing.Sample(entry.Lease.Baseline, entry.Seed, elapsed, amount * fade);
                entry.Material.SetColor(entry.Property, new Color(tint.Red, tint.Green, tint.Blue, tint.Alpha));
                entry.Lease.Written(tint);
                UpdateIndex++;
            }
        }
        catch (Exception error)
        {
            Faulted = true;
            Restore();
            ModSupport.WarnOnce("lsd:material-colors", $"LSD material color breathing stopped: {error.Message}");
        }
    }

    private void Discover(float elapsed)
    {
        var uv = Samples[SampleIndex++ % Samples.Length];
        var ray = Camera.ViewportPointToRay(new Vector3(uv.x, uv.y, 0));
        var mask = Camera.cullingMask & Physics.DefaultRaycastLayers & ~(1 << 5);
        if (!Physics.Raycast(ray, out var hit, 80, mask, QueryTriggerInteraction.Ignore) || !hit.collider
            || hit.collider.GetComponentInParent<NPC>() || hit.collider.GetComponentInParent<Player>()) return;
        var renderer = FindRenderer(hit);
        if (renderer is null || !renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy
            || renderer.GetComponentInParent<NPC>() || renderer.GetComponentInParent<Player>()) return;
        var materials = renderer.sharedMaterials;
        // A sampled multi-material mesh cannot monopolize discovery this frame.
        var admitted = false;
        for (var slot = 0; slot < Math.Min(materials.Length, 8); slot++)
        {
            var material = materials[slot];
            if (!material) continue;
            var existing = Entries.Find(entry => entry.Material.Pointer == material.Pointer);
            if (existing is not null) { existing.Seen = elapsed; continue; }
            if (admitted || !Opaque(material)) continue;
            var property = ColorProperty(material);
            if (property is null) continue;
            var baseline = Read(material, property.Value);
            if (!float.IsFinite(baseline.Red) || !float.IsFinite(baseline.Green) || !float.IsFinite(baseline.Blue)
                || !float.IsFinite(baseline.Alpha)) continue;
            if (Entries.Count >= Capacity)
            {
                var oldest = Entries.MinBy(entry => entry.Seen)!;
                if (elapsed - oldest.Seen < 0.5f) continue;
                Restore(oldest);
                Entries.Remove(oldest);
            }
            Entries.Add(new Entry { Material = material, Property = property.Value,
                Lease = new TintLease(baseline), Seed = material.GetInstanceID(), Born = elapsed, Seen = elapsed });
            admitted = true;
        }
    }

    private static MeshRenderer? FindRenderer(RaycastHit hit)
    {
        var direct = hit.collider.GetComponent<MeshRenderer>();
        if (direct) return direct;
        // Some collider meshes are siblings of their rendered mesh. Inspect
        // only a small local hierarchy instead of walking the whole scene.
        var node = hit.collider.transform;
        for (var depth = 0; depth < 3 && node; depth++, node = node.parent)
        {
            var parent = node.GetComponent<MeshRenderer>();
            if (parent && parent.bounds.SqrDistance(hit.point) < 0.25f) return parent;
            if (node.childCount > 12) continue;
            for (var i = 0; i < node.childCount; i++)
            {
                var sibling = node.GetChild(i).GetComponent<MeshRenderer>();
                if (sibling && sibling.bounds.SqrDistance(hit.point) < 0.25f) return sibling;
            }
        }
        return null;
    }

    private static bool Opaque(Material material)
        => material.shader && material.shader.isSupported && material.renderQueue < 2450
            && !material.IsKeywordEnabled("_ALPHATEST_ON") && !material.IsKeywordEnabled("_ALPHABLEND_ON")
            && (!material.HasProperty("_Surface") || material.GetFloat("_Surface") == 0)
            && (!material.HasProperty("_AlphaClip") || material.GetFloat("_AlphaClip") == 0);

    private static int? ColorProperty(Material material)
    {
        if (material.HasProperty("_BaseColor")) return Shader.PropertyToID("_BaseColor");
        if (material.HasProperty("_Color")) return Shader.PropertyToID("_Color");
        var shader = material.shader;
        for (var i = 0; i < Math.Min(shader.GetPropertyCount(), 64); i++)
            if (shader.GetPropertyType(i) == ShaderPropertyType.Color
                && (shader.GetPropertyFlags(i) & ShaderPropertyFlags.MainColor) != 0)
                return Shader.PropertyToID(shader.GetPropertyName(i));
        return null;
    }

    private static SurfaceTint Read(Material material, int property)
    {
        var color = material.GetColor(property);
        return new SurfaceTint(color.r, color.g, color.b, color.a);
    }

    private static void Restore(Entry entry)
    {
        if (!entry.Material || !entry.Lease.CanRestore(Read(entry.Material, entry.Property))) return;
        var tint = entry.Lease.Baseline;
        entry.Material.SetColor(entry.Property, new Color(tint.Red, tint.Green, tint.Blue, tint.Alpha));
    }

    private void Restore()
    {
        foreach (var entry in Entries)
        {
            try { Restore(entry); }
            catch (Exception error) { ModSupport.WarnOnce("lsd:color-restore", $"LSD material color restore failed: {error.Message}"); }
        }
        Entries.Clear();
        UpdateIndex = 0;
    }

    public void Dispose() { if (Disposed) return; Disposed = true; Restore(); }
}
