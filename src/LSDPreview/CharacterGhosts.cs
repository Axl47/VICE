using Avatar = Il2CppScheduleOne.AvatarFramework.Avatar;
using Il2CppScheduleOne.NPCs;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

internal sealed class CharacterGhosts : IDisposable
{
    private sealed class Part
    {
        internal Mesh Mesh = null!;
        internal Vector3 Position, Scale;
        internal Quaternion Rotation;
        internal Material[] Sources = Array.Empty<Material>();
        internal Color[] Colors = Array.Empty<Color>();
        internal bool OwnsMesh;
    }
    private readonly List<Part> Parts = new();
    private bool Disposed;
    internal string SourceName { get; private set; } = "";

    internal static CharacterGhosts Capture(Vector3 origin, string preferred)
    {
        var result = new CharacterGhosts();
        try
        {
            NPC? source = null;
            var distance = float.PositiveInfinity;
            foreach (var npc in NPCManager.NPCRegistry)
            {
                if (!npc || !npc.gameObject.activeInHierarchy || !npc.Avatar || !npc.Avatar.BodyContainer || npc.Avatar.IsCulled || npc.IsInVehicle) continue;
                if (!string.IsNullOrWhiteSpace(preferred) && !string.Equals(npc.ID, preferred, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(npc.FullName, preferred, StringComparison.OrdinalIgnoreCase)) continue;
                var candidate = Vector3.SqrMagnitude(npc.transform.position - origin);
                if (candidate >= distance) continue;
                source = npc;
                distance = candidate;
            }
            if (source is null || !source || !source.Avatar) throw new InvalidOperationException("No matching loaded character model is available yet.");
            result.SourceName = source.FullName;
            result.CaptureParts(source.Avatar);
            if (result.Parts.Count == 0) throw new InvalidOperationException("The chosen character has no usable visible meshes.");
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private void CaptureParts(Avatar avatar)
    {
        var vertexBudget = 50000;
        foreach (var renderer in avatar.BodyContainer.GetComponentsInChildren<Renderer>(true))
        {
            // Disabled LODs and native aura/particle helpers are not character geometry.
            if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || Parts.Count >= 20
                || IsEffectGeometry(renderer, avatar.BodyContainer)) continue;
            var skin = renderer.TryCast<SkinnedMeshRenderer>();
            if (skin is null && renderer.TryCast<MeshRenderer>() is null) continue;
            Mesh? mesh;
            var owns = false;
            if (skin is not null && skin && skin.sharedMesh && skin.sharedMesh.vertexCount > 0)
            {
                if (skin.sharedMesh.vertexCount > vertexBudget) continue;
                mesh = new Mesh { name = "LSD baked character part", hideFlags = HideFlags.DontSave };
                try { skin.BakeMesh(mesh, false); }
                catch { Object.Destroy(mesh); throw; }
                owns = true;
            }
            else
            {
                var filter = renderer.GetComponent<MeshFilter>();
                mesh = filter ? filter.sharedMesh : null;
                if (mesh is null || !mesh || mesh.vertexCount <= 0 || mesh.vertexCount > vertexBudget) continue;
            }
            var scale = renderer.transform.lossyScale;
            var sources = renderer.sharedMaterials.ToArray();
            var part = new Part { Mesh = mesh, OwnsMesh = owns,
                // The new root is unit-sized. Retain imported renderer scales
                // and use only translation/rotation to enter its local frame.
                Position = Quaternion.Inverse(avatar.transform.rotation) * (renderer.transform.position - avatar.transform.position),
                Rotation = Quaternion.Inverse(avatar.transform.rotation) * renderer.transform.rotation,
                Scale = scale,
                Sources = sources, Colors = sources.Select(SourceColor).ToArray() };
            Parts.Add(part);
            vertexBudget -= mesh.vertexCount;
        }
        FitToCharacterHeight();
    }

    private void FitToCharacterHeight()
    {
        if (Parts.Count == 0) return;
        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        foreach (var part in Parts)
        {
            var bounds = part.Mesh.bounds;
            var transform = Matrix4x4.TRS(part.Position, part.Rotation, part.Scale);
            for (var corner = 0; corner < 8; corner++)
            {
                var local = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var point = transform.MultiplyPoint3x4(local);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
        }
        var height = max.y - min.y;
        var fit = GhostSizing.Fit(height);
        if (fit <= 0) throw new InvalidOperationException("The captured character has invalid mesh bounds.");
        foreach (var part in Parts)
        {
            part.Position = (part.Position - new Vector3(0, min.y, 0)) * fit;
            part.Scale *= fit;
        }
        MelonLoader.MelonLogger.Msg($"LSD character size: '{SourceName}', captured {height:F2}m, fitted {height * fit:F2}m; native renderer scales preserved, feet grounded.");
    }

    internal List<Material> Build(GameObject parent, int layer)
    {
        if (Disposed) throw new ObjectDisposedException(nameof(CharacterGhosts));
        var materials = new List<Material>();
        try
        {
            foreach (var part in Parts)
            {
                var go = new GameObject("Hallucinated character mesh") { hideFlags = HideFlags.DontSave, layer = layer };
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition = part.Position;
                go.transform.localRotation = part.Rotation;
                go.transform.localScale = part.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                var targets = new Material[Math.Max(1, part.Mesh.subMeshCount)];
                for (var i = 0; i < targets.Length; i++)
                {
                    var source = i < part.Sources.Length ? part.Sources[i] : null;
                    targets[i] = MakeMaterial(source, i < part.Colors.Length ? part.Colors[i] : Color.white);
                    materials.Add(targets[i]);
                }
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = targets;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return materials;
        }
        catch { foreach (var material in materials) if (material) Object.Destroy(material); throw; }
    }

    private static bool IsEffectGeometry(Renderer renderer, Transform body)
    {
        if (renderer.GetComponentInParent<ParticleSystem>()) return true;
        for (var node = renderer.transform; node && node != body; node = node.parent)
        {
            var name = node.name;
            foreach (var token in new[] { "particle", "halo", "aura", "glow", "ring", "effect", "trail" })
                if (name.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        }
        foreach (var material in renderer.sharedMaterials)
        {
            if (!material || !material.shader) continue;
            var name = material.shader.name;
            if (name.Contains("Particle", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Halo", StringComparison.OrdinalIgnoreCase)
                || name.Contains("VFX", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static Color SourceColor(Material source)
    {
        if (!source) return Color.white;
        if (source.HasProperty("_BaseColor")) return source.GetColor("_BaseColor");
        if (source.HasProperty("_Color")) return source.GetColor("_Color");
        if (source.HasProperty("_SkinColor")) return source.GetColor("_SkinColor");
        return Color.white;
    }

    private static Material MakeMaterial(Material? source, Color tint)
    {
        var shader = Shader.Find("Sprites/Default");
        if (!shader || !shader.isSupported) throw new InvalidOperationException("The transparent ghost shader is unavailable.");
        Texture texture = Texture2D.whiteTexture;
        if (source is not null && source)
        {
            if (source.HasProperty("_BaseMap")) texture = source.GetTexture("_BaseMap") ?? texture;
            else if (source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex") ?? texture;
        }
        // Keep clothing/skin color distinctions instead of washing every part cyan.
        tint = Color.Lerp(tint, new Color(tint.grayscale, tint.grayscale, tint.grayscale), 0.2f);
        tint.a = 0;
        return new Material(shader) { name = "Personal LSD faded character", hideFlags = HideFlags.DontSave,
            mainTexture = texture, color = tint, renderQueue = 3000 };
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        foreach (var part in Parts) if (part.OwnsMesh && part.Mesh) Object.Destroy(part.Mesh);
        Parts.Clear();
    }
}
