using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VICE.Enforcement.Information;

internal enum Pressure { Quiet, Attention, Heavy }
internal sealed class PropertyReportState
{
    public Pressure Current { get; set; }
    public Pressure Reported { get; set; }
    public int Heat { get; set; }
    public long ChangedAt { get; set; }
}
internal sealed class ReportMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
}
internal sealed class ReportState
{
    public int Version { get; set; } = 1;
    public long Minute { get; set; }
    public long NextReport { get; set; }
    public Dictionary<string, PropertyReportState> Properties { get; set; } = new();
    public List<ReportMessage> History { get; set; } = new();
}

// Pure host policy. Pending work is Current != Reported, so a transient rise
// that returns to the last reported band cancels itself without a stale warning.
internal sealed class PressureReports(ReportState state, int cooldown)
{
    internal ReportState State { get; } = state;
    private int rawMinute;
    private bool clockStarted;
    internal void Start(int minute, IEnumerable<(string Code, int Heat)> properties)
    {
        rawMinute = minute; clockStarted = true;
        var current = properties.ToArray();
        var codes = current.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var code in State.Properties.Keys.Where(code => !codes.Contains(code)).ToArray()) State.Properties.Remove(code);
        foreach (var (code, heat) in current)
        {
            if (!State.Properties.TryGetValue(code, out var previous) || previous.Heat != heat)
                Baseline(code, heat); // New save/property, or heat rolled back independently.
        }
    }
    internal void Advance(int minute)
    {
        if (clockStarted) State.Minute += Math.Max(0L, (long)minute - rawMinute);
        rawMinute = minute; clockStarted = true;
    }
    internal void Baseline(string code, int heat)
    {
        var band = Band(Pressure.Quiet, heat);
        State.Properties[code] = new() { Current = band, Reported = band, Heat = heat, ChangedAt = State.Minute };
    }
    internal void Observe(string code, int heat)
    {
        if (!State.Properties.TryGetValue(code, out var property)) { Baseline(code, heat); return; }
        property.Heat = heat;
        var next = Band(property.Current, heat);
        if (next == property.Current) return;
        property.Current = next; property.ChangedAt = State.Minute;
    }
    internal ReportMessage? Take(IReadOnlyDictionary<string, string> names, bool raidActive)
    {
        if (raidActive || State.Minute < State.NextReport) return null;
        var candidate = State.Properties.Where(p => names.ContainsKey(p.Key) && p.Value.Current != p.Value.Reported)
            .OrderByDescending(p => p.Value.Current > p.Value.Reported)
            .ThenByDescending(p => p.Value.Current).ThenBy(p => p.Value.ChangedAt).ThenBy(p => p.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate.Value is null) return null;
        var property = candidate.Value;
        var name = names[candidate.Key];
        var text = property.Current switch {
            Pressure.Quiet => $"Things are quiet around {name} again. Keep it that way.",
            Pressure.Heavy => $"There's a lot of attention around {name}. Be careful over there.",
            _ when property.Reported == Pressure.Heavy => $"The attention around {name} has eased, but it hasn't gone quiet.",
            _ => $"{name} is getting attention. Keep a low profile."
        };
        property.Reported = property.Current;
        State.NextReport = State.Minute + Math.Clamp(cooldown, 30, 1440);
        var message = new ReportMessage { Text = text };
        State.History.Add(message);
        if (State.History.Count > 20) State.History.RemoveAt(0);
        return message;
    }
    internal static Pressure Band(Pressure previous, int heat) => previous switch {
        Pressure.Heavy when heat > 10 => Pressure.Heavy,
        _ when heat >= 14 => Pressure.Heavy,
        Pressure.Attention or Pressure.Heavy when heat > 3 => Pressure.Attention,
        _ when heat >= 6 => Pressure.Attention,
        _ => Pressure.Quiet
    };
}

internal static class ReportStore
{
    // Exact org hash avoids the upstream sanitized-name collisions; slot stays explicit.
    internal static string FileName(int slot, string organization) => $"{slot}_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(organization)))}.json";
    internal static ReportState Read(string path)
    {
        if (!File.Exists(path)) return new();
        var state = JsonSerializer.Deserialize<ReportState>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty report state.");
        if (state.Version != 1 || state.Minute < 0 || state.NextReport < 0 || state.Properties is null || state.History is null
            || state.Properties.Count > 128 || state.History.Count > 20
            || state.Properties.Any(p => p.Value is null || !Enum.IsDefined(typeof(Pressure), p.Value.Current) || !Enum.IsDefined(typeof(Pressure), p.Value.Reported))
            || state.History.Any(m => m is null || !Guid.TryParseExact(m.Id, "N", out _) || m.Text.Length > 512))
            throw new InvalidDataException("Invalid report state.");
        return state;
    }
    internal static void Write(string path, ReportState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(state));
        File.Move(path + ".tmp", path, true);
    }
}
