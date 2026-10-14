namespace VICE.Enforcement.Multiplayer;

internal sealed class ColorValue
{
    public string Id { get; set; } = "";
    public float[] Value { get; set; } = new float[4];
}
internal sealed class WornObject
{
    public string Id { get; set; } = "";
    public List<ColorValue> Colors { get; set; } = new();
}
internal sealed class OfficerState
{
    public int ObjectId { get; set; }
    public string Guid { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Naked { get; set; } = "";
    public List<WornObject> Outfit { get; set; } = new();
    public bool Belt { get; set; }
    public bool Visible { get; set; }
    public string AppearanceRevision { get; set; } = "";
}
internal sealed class CameraState
{
    public string Id { get; set; } = "";
    public bool Active { get; set; }
    public bool Broken { get; set; }
    public bool Sighted { get; set; }
    public float Progress { get; set; }
    public float[] Target { get; set; } = new float[3];
}
internal sealed class CameraNotice
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
    public string Player { get; set; } = "";
    public string[] Labels { get; set; } = Array.Empty<string>();
    public bool RefreshPursuit { get; set; }
    public int Connection { get; set; }
}
internal sealed class WorldState
{
    public int Version { get; set; } = 1;
    public string Session { get; set; } = "";
    public float NoticeSpeed { get; set; }
    public List<OfficerState> Officers { get; set; } = new();
    public List<CameraState> Cameras { get; set; } = new();
    public List<CameraNotice> Notices { get; set; } = new();
}
internal sealed class ClientObservation
{
    public float Visibility { get; set; }
    public string[] Labels { get; set; } = Array.Empty<string>();
    public string BreakCamera { get; set; } = "";
    public string[] AcknowledgedNotices { get; set; } = Array.Empty<string>();
}
internal static class ReplicationRules
{
    internal static readonly HashSet<string> Labels = new(StringComparer.Ordinal) { "Suspicious", "DisobeyingCurfew", "Vandalizing", "PettyCrime", "DrugDealing", "Wanted", "Pickpocketing", "DischargingWeapon", "Brandishing" };
    internal static string[] NormalizeLabels(IEnumerable<string> values) => values.Where(Labels.Contains).Distinct(StringComparer.Ordinal).Take(Labels.Count).ToArray();
    internal static bool InView(float distance, float angle, float range, bool omni) => float.IsFinite(distance) && float.IsFinite(angle) && distance >= 0 && distance <= range && (omni || angle <= 55);
    internal static float RayFraction(int sample, int count) => count > 1 ? (float)sample / (count - 1) : 0;
    internal static bool Valid(WorldState state) => state.Version == 1 && System.Guid.TryParseExact(state.Session, "N", out _)
        && state.Officers.Count <= 128 && state.Cameras.Count <= 64 && state.Notices.Count <= 64
        && state.Officers.All(o => o.ObjectId >= 0 && o.Id.StartsWith("NACop_", StringComparison.Ordinal) && System.Guid.TryParse(o.Guid, out _)
            && o.Naked.Length <= 32000 && o.Outfit.Count <= 32 && o.Outfit.All(w => w.Id.Length <= 256 && w.Colors.Count <= 32 && w.Colors.All(c => c.Value.Length == 4 && c.Value.All(float.IsFinite))))
        && state.Cameras.All(c => c.Id.Length <= 512 && c.Target.Length == 3 && c.Target.All(float.IsFinite) && float.IsFinite(c.Progress))
        && state.Notices.All(n => System.Guid.TryParseExact(n.Id, "N", out _) && n.Labels.Length <= Labels.Count && n.Labels.All(Labels.Contains));
}
