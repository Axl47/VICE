using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.Core.Audio;
using S1LSD.Core;
using UnityEngine;
using UnityEngine.Audio;
using AudioMixer = UnityEngine.Audio.AudioMixer;
using Object = UnityEngine.Object;

namespace PersonalMods.S1LSDComparison;

// Adapt the original MIT audio targets/synthesis to owned final-bus DSP. No
// stripped filter components, source cloning, hooks, snapshots or game settings.
internal sealed class ComparisonAudio : IDisposable
{
    private sealed record Route(AudioMixer Native, AudioMixerGroup? Previous, AudioMixer Mixer, AudioMixerGroup Input, int Slot);
    private readonly ComparisonAudioBank Bank = new();
    private readonly ComparisonAudioState State = new();
    private readonly List<Route> Routes = new();
    private readonly AudioSource?[] Layers = new AudioSource?[6];
    private readonly AudioClip?[] Clips = new AudioClip?[6];
    private readonly CancellationTokenSource Cancel = new();
    private Task<float[][]>? Samples;
    private GameObject? Root;
    private float NextRoutes, NextLog;
    private int Uploaded;
    private bool Disposed, Faulted;
    internal int RouteCount => Routes.Count;
    internal int PlayingLayers => Layers.Count(s => s && s!.isPlaying);
    internal AudioTargets Current { get; private set; }

    internal void Tick(in AudioTargets targets, float dt, float transition, bool ambience = true, bool diagnostic = false)
    {
        if (Disposed || Faulted) return;
        try {
            Current = diagnostic ? targets : State.Step(targets, dt, transition);
            if (AudioListener.pause || AudioListener.volume <= 0) { StopLayers(); ReleaseRoutes(); return; }
            if (Time.realtimeSinceStartup >= NextRoutes) { NextRoutes = Time.realtimeSinceStartup + 0.5f; BindRoutes(); }
            foreach (var route in Routes) ComparisonAudioBank.Apply(route.Mixer, Current);
            if (ambience) ApplyAmbience(Current); else StopLayers();
            if (Time.realtimeSinceStartup >= NextLog && Routes.Count > 0) {
                NextLog = Time.realtimeSinceStartup + 10;
                MelonLoader.MelonLogger.Msg($"S1LSD comparison audio: {Routes.Count} final buses; echo {Current.EchoDelayMs:F0}ms wet {Current.EchoWet:F3}; reverb {Current.ReverbRoomMb:F0}mB/{Current.ReverbDecaySeconds:F2}s; chorus {Current.ChorusWet:F3}; cutoff {Current.LowPassHz:F0}Hz; {PlayingLayers} procedural loops playing.");
            }
        } catch (Exception error) {
            Faulted = true;
            try { Dispose(); } finally { MelonLoader.MelonLogger.Error($"S1LSD comparison audio stopped: {error.GetBaseException()}"); }
        }
    }

    private void BindRoutes()
    {
        var manager = AudioManager.Instance; if (!manager) return;
        var buses = new List<AudioMixer>();
        foreach (var group in new[] { manager.MainGameMixer, manager.MusicMixer, manager.MenuMixer }) {
            var mixer = group ? group.audioMixer : null;
            if (mixer && !buses.Any(b => b.Pointer == mixer!.Pointer)) buses.Add(mixer!);
        }
        for (var i = Routes.Count - 1; i >= 0; i--) {
            var route = Routes[i];
            if (!route.Native || !buses.Any(b => b.Pointer == route.Native.Pointer) || !Same(route.Native.outputAudioMixerGroup, route.Input)) {
                Release(route); Routes.RemoveAt(i);
            }
        }
        foreach (var native in buses) {
            if (Routes.Any(r => r.Native.Pointer == native.Pointer)) continue;
            var previous = native.outputAudioMixerGroup;
            if (previous && buses.Any(b => b.Pointer == previous!.audioMixer.Pointer)) continue;
            var slot = Enumerable.Range(0, 3).First(i => !Routes.Any(r => r.Slot == i));
            var mixer = Bank.Load(slot); var input = ComparisonAudioBank.Input(mixer);
            // Prime the owned effects before connecting the actual game output.
            ComparisonAudioBank.Apply(mixer, Current); mixer.outputAudioMixerGroup = previous!;
            Routes.Add(new Route(native, previous, mixer, input, slot)); native.outputAudioMixerGroup = input;
            if (!Same(native.outputAudioMixerGroup, input)) throw new InvalidOperationException("Native comparison audio route rejected.");
            MelonLoader.MelonLogger.Msg($"S1LSD comparison audio attached after '{native.name}'.");
        }
    }

    private void ApplyAmbience(in AudioTargets t)
    {
        if (Routes.Count == 0) return;
        if (Samples is null) {
            var token = Cancel.Token;
            Samples = Task.Run(() => {
                Func<float[]>[] synths = { () => ProceduralSynth.Drone(), () => ProceduralSynth.Shimmer(), () => ProceduralSynth.ComeUpPulse(),
                    () => ProceduralSynth.BadDrone(), () => ProceduralSynth.Heartbeat(), () => ProceduralSynth.Whisper() };
                var data = new float[6][];
                for (var i = 0; i < data.Length; i++) { token.ThrowIfCancellationRequested(); data[i] = synths[i](); }
                return data;
            }, token);
        }
        if (Samples.IsFaulted) throw Samples.Exception!.GetBaseException();
        if (!Samples.IsCompletedSuccessfully) return;
        if (!Root) Root = new GameObject("S1LSD comparison soundscape") { hideFlags = HideFlags.DontSave };
        // Only one native clip upload per frame; synthesis never blocks Update.
        if (Uploaded < 6) {
            var index = Uploaded;
            var names = new[] { "drone", "shimmer", "pulse", "badDrone", "heart", "whisper" };
            var data = Samples.Result[index];
            var clip = AudioClip.Create("S1LSD comparison " + names[index], data.Length, 1, ProceduralSynth.SampleRate, false);
            Clips[index] = clip;
            clip.hideFlags = HideFlags.DontSave;
            if (!clip.SetData(new Il2CppStructArray<float>(data), 0)) throw new InvalidOperationException("Comparison ambience upload failed.");
            var source = Root!.AddComponent<AudioSource>(); Layers[index] = source;
            source.playOnAwake = false; source.spatialBlend = 0; source.loop = true; source.priority = 200; source.volume = 0; source.clip = clip;
            Uploaded++;
        }
        var manager = AudioManager.Instance; if (!manager || !manager.MainGameMixer) { StopLayers(); return; }
        var volume = Mathf.Clamp01(manager.GetVolume(EAudioType.Ambient, true));
        var levels = new[] { t.Drone, t.Shimmer, t.ComeUpPulse, t.BadDrone, t.Heart, t.Whisper };
        for (var i = 0; i < Uploaded; i++) {
            var source = Layers[i]!; source.outputAudioMixerGroup = manager.MainGameMixer;
            source.volume = Mathf.Clamp01(levels[i] * volume); if (i == 4) source.pitch = Mathf.Clamp(t.HeartRate > 0 ? t.HeartRate : 1, 0.9f, 2.1f);
            if (source.volume > 0.0005f) { if (!source.isPlaying) source.Play(); }
            else if (source.isPlaying) source.Stop();
        }
    }

    private static bool Same(AudioMixerGroup? a, AudioMixerGroup? b) => (a ? a!.Pointer : IntPtr.Zero) == (b ? b!.Pointer : IntPtr.Zero);
    private void StopLayers() { foreach (var source in Layers) if (source) source!.Stop(); }
    private static void Release(Route route)
    {
        if (route.Mixer) ComparisonAudioBank.Apply(route.Mixer, ComparisonAudioBank.Neutral);
        if (route.Native && Same(route.Native.outputAudioMixerGroup, route.Input)) {
            route.Native.outputAudioMixerGroup = route.Previous!;
            if (!Same(route.Native.outputAudioMixerGroup, route.Previous)) throw new InvalidOperationException("Native comparison audio restoration failed.");
        }
        if (route.Mixer) route.Mixer.outputAudioMixerGroup = null!;
    }
    private void ReleaseRoutes()
    {
        foreach (var route in Routes.ToArray()) { Release(route); Routes.Remove(route); }
        NextRoutes = 0;
    }
    public void Dispose()
    {
        if (Disposed) return;
        // Do not unload a bank if restoration failed: leave its dry route alive.
        StopLayers(); ReleaseRoutes(); Bank.Dispose(); Cancel.Cancel();
        foreach (var source in Layers) if (source) source!.clip = null!;
        if (Root) Object.Destroy(Root); foreach (var clip in Clips) if (clip) Object.Destroy(clip);
        Array.Clear(Layers, 0, Layers.Length); Array.Clear(Clips, 0, Clips.Length); Root = null; Samples = null;
        Disposed = true;
    }
}
