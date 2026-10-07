using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using S1LSD.Config;
using S1LSD.Core;

namespace PersonalMods.S1LSDComparison;

// Pure state, shared with the standalone checks. No inventory, save or native
// game objects are reachable through the borrowed dose engine.
internal sealed class ComparisonSession
{
    internal static readonly int[] Doses = { 70, 140, 280, 360 };
    internal readonly LsdConfig Config;
    internal EffectController Effect { get; private set; }
    private VisualMixer Mixer = new(469);
    private readonly S1LSD.Core.AudioMixer SoundMixer;
    internal bool Classic { get; set; } = true;
    internal bool HoldPeak { get; set; } = true;
    internal float VisualTime { get; private set; }
    internal bool Active => Effect.IsActive;

    internal ComparisonSession(LsdConfig config)
    {
        Config = config;
        SoundMixer = new S1LSD.Core.AudioMixer(Config.Audio);
        // Neutral, repeatable comparison. No world anxiety/tolerance/economy.
        Config.BadTrip.Enabled = false;
        Config.BadTrip.UseSetting = false;
        Config.Phases.TripVariation = 0;
        Effect = new EffectController(Config, 469);
    }

    internal static ComparisonSession FromJson(string json)
    {
        var data = JObject.Parse(json);
        // The original embedded JSON places prose inside dictionaries. Its
        // loader removes these annotations before deserialization as well.
        foreach (var comment in data.Descendants().OfType<JProperty>().Where(p => p.Name == "_comment").ToArray())
            comment.Remove();
        return new ComparisonSession(data.ToObject<LsdConfig>()
            ?? throw new InvalidOperationException("The embedded S1LSD dose configuration is missing."));
    }

    internal void Start(int dose)
    {
        if (!Doses.Contains(dose)) throw new ArgumentOutOfRangeException(nameof(dose));
        Effect.Stop();
        Effect = new EffectController(Config, 469);
        Mixer = new VisualMixer(469);
        if (HoldPeak) Effect.Preview(dose, PhaseId.Peak);
        else Effect.Start(dose, 0, false);
        VisualTime = Effect.ElapsedSeconds;
    }

    internal void Tick(float delta)
    {
        if (!Active) return;
        if (!float.IsFinite(delta) || delta <= 0) return;
        delta = Math.Min(delta, 0.1f);
        VisualTime += delta;
        Effect.Tick(delta);
        if (HoldPeak && Active) Effect.HoldPreviewPhase(PhaseId.Peak);
    }

    internal VisualInputs Inputs(float gaze = 1, float saccade = 0, float darkness = 0.5f)
    {
        Config.Doses.TryGetValue(Effect.Dose.ToString(), out var dose);
        return new VisualInputs {
            Channels = Effect.Channels, Dose = Effect.Dose, Breath = Effect.Breath,
            Classic = Classic, ShaderActive = true, Gaze = gaze, SaccadeBurst = saccade,
            Darkness = darkness, Palette = PsychedelicPalette.Natural,
            ColorVibrancy = 1, Time = VisualTime,
            CrawlBlendPeak = dose?.GetBlend(Classic) ?? 0,
            CrawlAmplitudePeak = dose?.GetAmplitude(Classic) ?? 0,
            CrawlNoiseScale = dose?.GetNoiseScale(Classic) ?? 0
        };
    }

    internal ShaderTargets Shader(in VisualInputs input) => Mixer.MixShader(input);
    internal VisualTargets Grade(in VisualInputs input) => Mixer.Mix(input);
    internal AudioTargets Audio(float dilation = 0) => SoundMixer.Mix(new AudioInputs {
        Channels = Effect.Channels, Breath = Effect.Breath, Phase = Effect.Phase,
        PhaseProgress = Effect.PhaseProgress(Effect.Phase), BadTripBlend = Effect.BadTripBlend,
        ClosedEyes = 0, TimeDilation = Math.Clamp(dilation, 0, 1),
        Unease = Active ? Effect.Mind.Unease : 0, HeartBpm = Effect.Mind.HeartBpm,
        Gentle = Config.Quality.GentleMode, Active = Active
    });
    internal void Stop() { Effect.Stop(); VisualTime = 0; }
}
