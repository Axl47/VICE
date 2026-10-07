using System.Reflection;
using PersonalMods.S1LSDComparison;
using S1LSD.Core;

using var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Comparison.Defaults.json")!);
var json = reader.ReadToEnd();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Near(float value, float expected, string message) => Check(Math.Abs(value - expected) < 0.00001f, $"{message}: {value} / {expected}");
var doses = new[] { 70, 140, 280, 360 };
var durations = new[] { 210, 330, 510, 660 };
var pixels = new[] { 1f, 2.1f, 5.8f, 11f };
var subtlePixels = new[] { 0.65f, 1.3f, 3.9f, 7.8f };
var taus = new[] { 0.15f, 0.25f, 0.38f, 0.45f };
for (var index = 0; index < doses.Length; index++) {
    var session = ComparisonSession.FromJson(json);
    Check(!session.Active && !session.Config.BadTrip.Enabled, "Comparison must begin inactive, with bad-trip episodes disabled.");
    session.Start(doses[index]);
    Near(session.Effect.DurationSeconds, durations[index], "Original tier duration");
    Check(session.Effect.Phase == PhaseId.Peak && session.Effect.Tolerance == 0, "Immediate peak / no tolerance.");
    var elapsed = session.Effect.ElapsedSeconds;
    var startVisual = session.VisualTime;
    var breaths = new List<float>();
    var fields = new List<float>();
    for (var frame = 0; frame < 600; frame++) {
        session.Tick(0.1f); breaths.Add(session.Effect.Breath);
        var input = session.Inputs(); var field = session.Shader(input); fields.Add(field.Breathe);
        Check(float.IsFinite(field.Crawl) && float.IsFinite(field.HueHighs) && float.IsFinite(field.PatternLattice), "Finite render targets.");
        Check(session.Effect.Phase == PhaseId.Peak, "Held peak must not drift into plateau.");
    }
    Near(session.Effect.ElapsedSeconds, elapsed, "Held peak preserves phase position");
    Check(session.VisualTime > startVisual + 59 && breaths.Max() - breaths.Min() > 0.8f
        && fields.Max() - fields.Min() > 0.001f, "Peak hold must preserve breathing and moving shader time.");

    // Golden original peak fixtures exercise all four distinct dose mappings,
    // including the low tier having no geometry and style-specific crawl.
    var values = new float[17];
    foreach (var pair in session.Config.Doses[doses[index].ToString()].Peaks)
        if (ChannelCount.TryResolve(pair.Key, out var channel)) values[(int)channel] = pair.Value;
    var peak = new VisualInputs { Channels = new ChannelSnapshot(values), Dose = doses[index], Classic = true,
        Breath = 0.75f, ColorVibrancy = 1, Gaze = 1, Darkness = 0.5f, Time = 20 };
    var mixer = new VisualMixer(469);
    var target = mixer.MixShader(peak);
    Near(target.Crawl, pixels[index] / 1080 / 1.2f, "Original classic crawl pixels");
    Near(target.Breathe, 0.001f * (index + 1), "Original breathing amplitude");
    Near(target.TrailTau, taus[index], "Original trail lifetime");
    Check(index == 0 ? target.Patterns == 0 : target.Patterns > 0, "Geometry must follow the original tier threshold.");
    Near(target.PatternLattice + target.PatternTunnel + target.PatternSpiral + target.PatternCobweb, 1, "Normalized geometric mix");
    peak.Classic = false;
    Near(mixer.MixShader(peak).Crawl, subtlePixels[index] / 1080 / 1.2f, "Original subtle crawl pixels");

    session.HoldPeak = false; session.Start(doses[index]);
    Check(session.Effect.ElapsedSeconds == 0 && session.Effect.Phase == PhaseId.Onset, "Full timeline starts at onset.");
    for (var frame = 0; frame < durations[index] * 10 + 20; frame++) session.Tick(0.1f);
    Check(!session.Active, "Full timeline must end and clear channels.");
    Check(session.Effect.Channels.ToArray().All(v => v == 0), "Stopped channels must be zero.");
    session.Start(70); session.Stop(); Check(!session.Active, "Stop must clear the preview immediately.");
}
var invalid = ComparisonSession.FromJson(json);
try { invalid.Start(999); throw new Exception("Unsupported dose accepted."); }
catch (ArgumentOutOfRangeException) { }
Console.WriteLine("PASS: all four original dose/style shader fixtures, durations, breathing while holding peak, finite targets, timeline completion, stop and unsupported-dose handling.");
