using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Audio;
using S1LSD.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.S1LSDComparison;

// Opt-in, menu-only verification. The probe travels through the ORIGINAL music
// bus and is measured at the FINAL listener; never used in normal gameplay.
internal sealed class ComparisonAudioCheck : IDisposable
{
    private readonly ComparisonAudio Audio = new();
    private readonly Il2CppStructArray<float> Probe = new(2048);
    private readonly AudioTargets Peak;
    private readonly List<(UnityEngine.Audio.AudioMixer Mixer, IntPtr Output)> Originals = new();
    private readonly float[] Dry = new float[5], Tail = new float[5];
    private GameObject? Root;
    private AudioSource? Source;
    private AudioClip? Clip;
    private double Cue;
    private int Phase = -1, AmbienceFrames;
    private bool Complete, Disposed;
    internal ComparisonAudioCheck(ComparisonSession session)
    {
        session.Start(360); Peak = session.Audio(); session.Stop();
    }
    internal bool Tick()
    {
        if (Complete || Disposed) return true;
        try {
            var now = AudioSettings.dspTime;
            if (Phase < 0) Initialize(now);
            if (Phase < 5) {
                var t = ComparisonAudioBank.Neutral;
                if (Phase == 1) { t.EchoOn = true; t.EchoWet = Peak.EchoWet; t.EchoDecay = Peak.EchoDecay;
                    t.EchoDelayMs = Peak.EchoDelayMs; t.EchoDry = Peak.EchoDry; }
                if (Phase == 2) { t.ReverbOn = true; t.ReverbRoomMb = Peak.ReverbRoomMb; t.ReverbRoomHfMb = Peak.ReverbRoomHfMb;
                    t.ReverbDecaySeconds = Peak.ReverbDecaySeconds; t.ReverbLevelMb = Peak.ReverbLevelMb; t.ReverbDryMb = Peak.ReverbDryMb; }
                if (Phase == 4) { t.LowPassOn = true; t.LowPassHz = Peak.LowPassHz; }
                Audio.Tick(t, 0.1f, 3, ambience: false, diagnostic: true);
                if (now >= Cue && now <= Cue + 0.75) {
                    AudioListener.GetOutputData(Probe, 0);
                    var position = now - Cue;
                    var signal = ToneAmplitude(Phase >= 3 ? 18000 : 4000);
                    if (position <= 0.14) Dry[Phase] = Math.Max(Dry[Phase], signal);
                    if (position is >= 0.18 and <= 0.75) Tail[Phase] = Math.Max(Tail[Phase], signal);
                }
                if (now > Cue + 1.5) {
                    Phase++;
                    if (Phase < 5) { SetTone(Phase >= 3 ? 18000 : 4000); Cue = now + 0.5; Source!.PlayScheduled(Cue); }
                }
                return false;
            }
            // Also exercise chorus, original procedural synthesis, all uploads,
            // smoothing and actual looping sources through the leased buses.
            Audio.Tick(Peak, 0.1f, 3);
            AmbienceFrames++;
            if (AmbienceFrames < 240) return false;
            var routes = Audio.RouteCount; var layers = Audio.PlayingLayers;
            var echo = Dry[0] > 0.0001f && Dry[1] > 0.0001f && Tail[1] > Math.Max(0.0001f, Tail[0] * 3);
            var reverb = Dry[2] > 0.0001f && Tail[2] > Math.Max(0.00002f, Tail[0] * 2);
            var lowpass = Dry[3] > 0.0001f && Dry[4] > 0 && Dry[4] < Dry[3] * 0.85f;
            Audio.Dispose();
            var restored = Originals.All(r => r.Mixer && (r.Mixer.outputAudioMixerGroup ? r.Mixer.outputAudioMixerGroup.Pointer : IntPtr.Zero) == r.Output);
            var pass = echo && reverb && lowpass && routes > 0 && layers >= 2 && restored;
            MelonLoader.MelonLogger.Msg($"S1LSD comparison FINAL-LISTENER AUDIO CHECK {(pass ? "PASSED" : "FAILED")}: echo={echo} dry {Dry[0]:F6}/{Dry[1]:F6}, {Peak.EchoDelayMs:F0}ms tail {Tail[0]:F6}/{Tail[1]:F6}; reverb={reverb} tail {Tail[2]:F6}; lowpass={lowpass} 18kHz {Dry[3]:F6}/{Dry[4]:F6}; chorus wet {Peak.ChorusWet:F3} native readback; buses={routes}, procedural playing={layers}, original routes restored={restored}.");
            Complete = true; Dispose(); return true;
        } catch (Exception error) {
            Complete = true; MelonLoader.MelonLogger.Error($"S1LSD comparison audio diagnostic FAILED: {error.GetBaseException()}");
            Dispose(); return true;
        }
    }
    private void Initialize(double now)
    {
        var manager = AudioManager.Instance;
        if (!manager || !manager.MusicMixer || AudioSettings.outputSampleRate < 40000) throw new InvalidOperationException("Native music bus / 40kHz+ output device required for diagnostic tones.");
        foreach (var group in new[] { manager.MainGameMixer, manager.MusicMixer, manager.MenuMixer }) if (group && group.audioMixer) {
            var mixer = group.audioMixer;
            Originals.Add((mixer, mixer.outputAudioMixerGroup ? mixer.outputAudioMixerGroup.Pointer : IntPtr.Zero));
        }
        Root = new GameObject("S1LSD comparison diagnostic") { hideFlags = HideFlags.DontSave };
        Source = Root.AddComponent<AudioSource>(); Source.playOnAwake = false; Source.spatialBlend = 0;
        Source.volume = 0.12f; Source.priority = 0; Source.outputAudioMixerGroup = manager.MusicMixer;
        Phase = 0; SetTone(4000); Cue = now + 0.5; Source.PlayScheduled(Cue);
    }
    private void SetTone(int hz)
    {
        Source!.Stop(); Source.clip = null!; if (Clip) Object.Destroy(Clip);
        var rate = AudioSettings.outputSampleRate; var count = (int)(rate * 0.05);
        Clip = AudioClip.Create("Comparison diagnostic tone", count, 1, rate, false);
        var samples = new Il2CppStructArray<float>(count);
        for (var i = 0; i < count; i++) samples[i] = Mathf.Sin(2 * Mathf.PI * hz * i / rate) * Mathf.Sin(Mathf.PI * i / count);
        if (!Clip.SetData(samples, 0)) throw new InvalidOperationException("Probe clip could not upload.");
        Source.clip = Clip;
    }
    private float ToneAmplitude(int hz)
    {
        double real = 0, imaginary = 0; var step = 2 * Math.PI * hz / AudioSettings.outputSampleRate;
        for (var i = 0; i < Probe.Length; i++) { real += Probe[i] * Math.Cos(step * i); imaginary += Probe[i] * Math.Sin(step * i); }
        return (float)(2 * Math.Sqrt(real * real + imaginary * imaginary) / Probe.Length);
    }
    public void Dispose()
    {
        if (Disposed) return;
        Audio.Dispose();
        if (Source) { Source!.Stop(); Source.clip = null!; }
        if (Root) Object.Destroy(Root); if (Clip) Object.Destroy(Clip);
        Root = null; Source = null; Clip = null; Disposed = true;
    }
}
