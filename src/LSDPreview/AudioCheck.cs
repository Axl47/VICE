using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Audio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Explicit menu-only diagnostic. A short owned tone enters the original music
// bus. Measure its delayed return in FINAL listener output, comparing echo off
// and on. No samples are captured/replayed by the normal preview audio path.
internal sealed class AudioCheck : IDisposable
{
    private readonly PreviewAudio Audio = new();
    private readonly Il2CppStructArray<float> Probe = new(2048);
    private GameObject? Root;
    private AudioSource? Source;
    private AudioClip? Clip;
    private double Began, Cue;
    private int Phase;
    private float DryOff, DryOn, TailOff, TailOn;
    private bool Disposed;
    private readonly List<(UnityEngine.Audio.AudioMixer Mixer, IntPtr Output)> OriginalRoutes = new();
    private const int ToneFrequency = 4000;

    internal bool Tick()
    {
        if (Disposed) return true;
        try
        {
            var now = AudioSettings.dspTime;
            if (Source is null) Initialize(now);
            var wet = Phase == 1 ? 0.60f * 0.35f : 0;
            Audio.Tick(30, wet);
            if (now >= Cue && now <= Cue + 0.46)
            {
                AudioListener.GetOutputData(Probe, 0);
                var tone = ToneAmplitude();
                var position = now - Cue;
                if (position <= 0.15)
                {
                    if (Phase == 0) DryOff = Math.Max(DryOff, tone);
                    else DryOn = Math.Max(DryOn, tone);
                }
                else if (position is >= 0.18 and <= 0.46)
                {
                    if (Phase == 0) TailOff = Math.Max(TailOff, tone);
                    else TailOn = Math.Max(TailOn, tone);
                }
            }
            if (Phase == 0 && now - Began >= 1.3)
            {
                Phase = 1;
                Audio.Tick(30, 0.60f * 0.35f);
                Cue = now + 0.2;
                Source!.PlayScheduled(Cue);
            }
            if (Phase == 1 && now - Cue > 0.7)
            {
                var pass = DryOff > 0.0001f && DryOn > 0.0001f && TailOn > Math.Max(0.0001f, TailOff * 3);
                MelonLoader.MelonLogger.Msg($"LSD final-listener echo check {(pass ? "PASSED" : "FAILED")}: native music bus, default wet; dry off/on {DryOff:F6}/{DryOn:F6}, 220ms tail off/on {TailOff:F6}/{TailOn:F6}. This measures the final mix, not a copied source.");
                if (!pass) ModSupport.WarnOnce("lsd:audio-check:signal", "The default echo was not distinguished in final listener output. Do not treat a source-output check as audible validation.");
                Audio.Dispose();
                var restored = OriginalRoutes.All(route => route.Mixer && (route.Mixer.outputAudioMixerGroup
                    ? route.Mixer.outputAudioMixerGroup.Pointer : IntPtr.Zero) == route.Output);
                MelonLoader.MelonLogger.Msg($"LSD audio cleanup check: {OriginalRoutes.Count} original native bus routes restored {restored}.");
                Dispose();
                return true;
            }
            return false;
        }
        catch (Exception error)
        {
            ModSupport.WarnOnce("lsd:audio-check", error.GetBaseException().ToString());
            Dispose();
            return true;
        }
    }

    private void Initialize(double now)
    {
        var manager = AudioManager.Instance;
        if (!manager || !manager.MusicMixer) throw new InvalidOperationException("The native music bus is unavailable.");
        foreach (var group in new[] { manager.MainGameMixer, manager.MusicMixer, manager.MenuMixer })
        {
            if (!group || !group.audioMixer) continue;
            var mixer = group.audioMixer;
            OriginalRoutes.Add((mixer, mixer.outputAudioMixerGroup ? mixer.outputAudioMixerGroup.Pointer : IntPtr.Zero));
        }
        var rate = AudioSettings.outputSampleRate;
        if (rate < 8000) throw new InvalidOperationException("There is no active audio output device.");
        Root = new GameObject("Personal LSD diagnostic tone") { hideFlags = HideFlags.DontSave };
        Source = Root.AddComponent(Il2CppType.Of<AudioSource>()).TryCast<AudioSource>();
        if (Source is null) throw new InvalidOperationException("The diagnostic audio source is unavailable.");
        Source.playOnAwake = false;
        Source.spatialBlend = 0;
        Source.volume = 0.12f;
        Source.priority = 0;
        Source.outputAudioMixerGroup = manager.MusicMixer;
        var count = (int)(rate * 0.035);
        Clip = AudioClip.Create("Owned LSD echo diagnostic", count, 1, rate, false);
        var samples = new Il2CppStructArray<float>(count);
        for (var i = 0; i < count; i++)
            samples[i] = Mathf.Sin(2 * Mathf.PI * ToneFrequency * i / rate) * Mathf.Sin(Mathf.PI * i / count);
        if (!Clip.SetData(samples, 0)) throw new InvalidOperationException("Unity could not create the diagnostic tone.");
        Source.clip = Clip;
        Began = now;
        Cue = now + 0.3;
        Source.PlayScheduled(Cue);
    }

    private float ToneAmplitude()
    {
        double real = 0, imaginary = 0;
        var step = 2 * Math.PI * ToneFrequency / AudioSettings.outputSampleRate;
        for (var i = 0; i < Probe.Length; i++)
        {
            real += Probe[i] * Math.Cos(step * i);
            imaginary += Probe[i] * Math.Sin(step * i);
        }
        return (float)(2 * Math.Sqrt(real * real + imaginary * imaginary) / Probe.Length);
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Audio.Dispose();
        if (Source) { Source!.Stop(); Source.clip = null!; }
        if (Root) Object.Destroy(Root);
        if (Clip) Object.Destroy(Clip);
        Root = null;
        Source = null;
        Clip = null;
    }
}
