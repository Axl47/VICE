using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Mesh snapshots of native characters, without copying a single NPC component.
internal sealed class WorldHallucinations : IDisposable
{
    private sealed class Figure
    {
        internal GameObject Root = null!;
        internal List<Material> Materials = new();
        internal float Born, Lifetime, DissolveAt = float.PositiveInfinity;
    }
    private readonly Camera Camera;
    private readonly System.Random Random = new();
    private readonly List<Figure> Figures = new();
    private readonly int Layer;
    private GameObject? Root;
    private CharacterGhosts? Template;
    private float NextFigure, NextTemplate;
    private bool Disposed;

    internal WorldHallucinations(Camera camera)
    {
        Camera = camera;
        Layer = (camera.cullingMask & (1 << 2)) != 0 ? 2
            : Enumerable.Range(0, 32).First(i => (camera.cullingMask & (1 << i)) != 0 && i != 5);
        Root = new GameObject("Personal LSD character hallucinations") { hideFlags = HideFlags.DontSave, layer = Layer };
        Root.SetActive(false);
        NextFigure = 3;
    }

    internal void Tick(float elapsed, float amount, float strength)
    {
        if (Disposed || Root is null || !Root || !Camera) return;
        Root.SetActive(amount > 0.01f);
        if (amount <= 0.01f) return;
        var origin = Camera.transform.position;
        if (Template is null && elapsed >= NextTemplate)
        {
            NextTemplate = elapsed + 5;
            try
            {
                Template = CharacterGhosts.Capture(origin, PreviewFeature.GhostModelNPC.Value);
                MelonLoader.MelonLogger.Msg("LSD hallucination appearance captured from " + Template.SourceName + ".");
            }
            catch (Exception error) { ModSupport.WarnOnce("lsd:character-template", $"LSD character hallucinations are waiting for a model: {error.Message}"); }
        }
        for (var i = Figures.Count - 1; i >= 0; i--)
        {
            var figure = Figures[i];
            var age = elapsed - figure.Born;
            if (!figure.Root || age >= figure.Lifetime || elapsed - figure.DissolveAt >= 1.5f)
            { RemoveFigure(i); continue; }
            var offset = figure.Root.transform.position + Vector3.up * 1.4f - origin;
            var dot = Vector3.Dot(Camera.transform.forward, offset.normalized);
            if (dot > 0.93f && float.IsPositiveInfinity(figure.DissolveAt)) figure.DissolveAt = elapsed;
            var fade = TripTimeline.Smooth(age) * TripTimeline.Smooth((figure.Lifetime - age) / 2);
            var dissolve = 1 - TripTimeline.Smooth((elapsed - figure.DissolveAt) / 1.5f);
            var alpha = TripTimeline.Smooth((amount - 0.15f) / 0.85f) * fade * dissolve
                * TripTimeline.PeripheralVisibility(dot) * 0.5f * strength;
            foreach (var material in figure.Materials)
            {
                if (!material) continue;
                var color = material.color;
                color.a = alpha;
                material.color = color;
            }
            var facing = origin - figure.Root.transform.position;
            facing.y = 0;
            if (facing.sqrMagnitude > 0.01f) figure.Root.transform.rotation = Quaternion.LookRotation(facing);
            figure.Root.transform.localScale = Vector3.one * (1 + 0.01f * Mathf.Sin(elapsed * 0.6f + i));
        }
        if (Template is not null && amount > 0.25f && elapsed >= NextFigure)
        {
            NextFigure = elapsed + 5;
            if (Figures.Count < 2) TrySpawnFigure(elapsed, origin);
        }
    }

    internal void Hide() { if (Root is not null && Root) Root.SetActive(false); }

    private void TrySpawnFigure(float elapsed, Vector3 origin)
    {
        if (Template is null || Root is null || !Root) return;
        var heading = Camera.transform.forward;
        heading.y = 0;
        if (heading.sqrMagnitude < 0.01f) return;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var angle = (40 + (float)Random.NextDouble() * 28) * (Random.Next(2) == 0 ? -1 : 1);
            var direction = Quaternion.AngleAxis(angle, Vector3.up) * heading.normalized;
            var candidate = origin + direction * (10 + (float)Random.NextDouble() * 12);
            if (!Physics.Raycast(candidate + Vector3.up * 6, Vector3.down, out var ground, 18,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f) continue;
            var feet = ground.point + Vector3.up * 0.05f;
            if (Mathf.Abs(feet.y - origin.y) > 5 || Physics.Linecast(origin, feet + Vector3.up * 1.4f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if (Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.5f, 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            var figure = new Figure { Born = elapsed, Lifetime = 9 + (float)Random.NextDouble() * 5,
                Root = new GameObject("Hallucinated character") { hideFlags = HideFlags.DontSave, layer = Layer } };
            figure.Root.transform.SetParent(Root.transform, false);
            figure.Root.transform.position = feet;
            try { figure.Materials = Template.Build(figure.Root, Layer); }
            catch { figure.Root.SetActive(false); Object.Destroy(figure.Root); throw; }
            Figures.Add(figure);
            return;
        }
    }

    private void RemoveFigure(int index)
    {
        var figure = Figures[index];
        if (figure.Root) { figure.Root.SetActive(false); Object.Destroy(figure.Root); }
        foreach (var material in figure.Materials) if (material) Object.Destroy(material);
        Figures.RemoveAt(index);
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        if (Root is not null && Root) Root.SetActive(false);
        for (var i = Figures.Count - 1; i >= 0; i--) RemoveFigure(i);
        Template?.Dispose();
        Template = null;
        if (Root is not null && Root) Object.Destroy(Root);
        Root = null;
    }
}
