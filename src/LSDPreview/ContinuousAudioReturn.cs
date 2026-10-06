using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Audio;
using EAudioType = Il2CppScheduleOne.Core.Audio.EAudioType;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Long tracks and ambient loops never enter SFXManager's footstep/impact pool.
// Discover controllers infrequently while F8 is active, then use their real
// clip cursors. Never alter a game's source or install a native detour.
internal sealed class ContinuousAudioReturn : IDisposable
{
    private sealed class Voice(AudioSource source)
    {
        internal readonly AudioSource Source = source;
        internal AudioSource? Dry;
        internal int ClipId;
        internal double Started, NextSync;
        internal bool Heard;
    }

    private const int Capacity = 6;
    private readonly Voice?[] Voices = new Voice?[Capacity];
    private readonly List<AudioSource> Candidates = new();
    private readonly List<AudioSource> Selected = new();
    private readonly HashSet<int> Observed = new();
    private readonly Il2CppStructArray<float> Probe = new(512);
    private GameObject? Root;
    private double NextDiscovery, NextSelection, NextProbe, LastClock;
    private bool Disposed, Faulted, LoggedDiscovery;
    private int Scheduled, Heard;
    internal int Active { get; private set; }
    internal float PeakOutput { get; private set; }

    internal void Tick(double now, float gain)
    {
        if (Disposed || Faulted) return;
        try
        {
            if (now < LastClock) Clear();
            LastClock = now;
            if (now >= NextDiscovery) Discover(now);
            if (now >= NextSelection)
            {
                NextSelection = now + 0.5;
                Selected.Clear();
                Selected.AddRange(Candidates.Where(IsPlaying).OrderByDescending(Audibility).Take(Capacity));
            }
            Observed.Clear();
            foreach (var dry in Selected)
            {
                if (!dry || !dry.enabled || !dry.gameObject.activeInHierarchy || !dry.isPlaying
                    || dry.mute || dry.volume <= 0 || dry.pitch < 0.1f || dry.pitch > 3) continue;
                var clip = dry.clip;
                if (!clip || clip.samples <= 0 || clip.frequency <= 0) continue;
                var id = dry.GetInstanceID();
                var voice = Voices.FirstOrDefault(v => v is not null && v.Dry && v.Dry!.GetInstanceID() == id);
                voice ??= Voices.FirstOrDefault(v => v is null || !v.Dry);
                if (voice is null)
                {
                    var index = Array.FindIndex(Voices, v => v is null);
                    if (index < 0) continue;
                    if (!Root) Root = new GameObject("Personal LSD continuous audio returns") { hideFlags = HideFlags.DontSave };
                    voice = Voices[index] = new Voice(AudioReturnSource.Create(Root!, $"LSD music return {index}"));
                }
                // A free existing voice is reused without stealing another track.
                Observed.Add(id);
                var source = voice.Source;
                var changed = !voice.Dry || voice.Dry!.GetInstanceID() != id
                    || voice.ClipId != clip.GetInstanceID() || source.loop != dry.loop
                    || Math.Abs(source.pitch - dry.pitch) > 0.005f;
                var restart = changed || (now > voice.Started + 0.1 && !source.isPlaying);
                if (!restart && now >= voice.NextSync && now > voice.Started + 0.5)
                {
                    voice.NextSync = now + 1;
                    if (DelayedClip.TryPosition(dry.timeSamples, clip.samples, clip.frequency, dry.pitch,
                        dry.loop, 0, out var expected))
                        restart = DelayedClip.Distance(source.timeSamples, expected, clip.samples, dry.loop)
                            > clip.frequency * dry.pitch * 0.12;
                }
                if (restart)
                {
                    source.Stop();
                    if (!DelayedClip.TryPosition(dry.timeSamples, clip.samples, clip.frequency, dry.pitch,
                        dry.loop, DelayedClip.Lead, out var cursor)) { Release(voice); continue; }
                    AudioReturnSource.Copy(dry, source);
                    source.clip = clip;
                    source.loop = dry.loop;
                    source.pitch = dry.pitch;
                    source.timeSamples = cursor;
                    source.volume = dry.volume * gain;
                    voice.Dry = dry;
                    voice.ClipId = clip.GetInstanceID();
                    voice.Started = AudioSettings.dspTime + DelayedClip.Lead;
                    voice.NextSync = voice.Started + 1;
                    voice.Heard = false;
                    source.PlayScheduled(voice.Started);
                    Scheduled++;
                    if (Scheduled <= Capacity)
                        MelonLoader.MelonLogger.Msg($"LSD music return: '{clip.name}' on '{dry.gameObject.name}', {clip.length:F1}s, loop {dry.loop}, delayed {DelayedClip.Delay * 1000:F0} ms, return {gain:P1}.");
                }
                source.volume = dry.volume * gain;
                source.transform.position = dry.transform.position;
                source.outputAudioMixerGroup = dry.outputAudioMixerGroup;
            }
            var probe = now >= NextProbe;
            if (probe) NextProbe = now + 0.25;
            Active = 0;
            foreach (var voice in Voices)
            {
                if (voice is null || !voice.Source) continue;
                if (!voice.Dry || !Observed.Contains(voice.Dry!.GetInstanceID())) { Release(voice); continue; }
                Active++;
                if (!probe || now < voice.Started || !voice.Source.isPlaying) continue;
                voice.Source.GetOutputData(Probe, 0);
                var peak = 0f;
                for (var index = 0; index < Probe.Length; index++) peak = Math.Max(peak, Math.Abs(Probe[index]));
                PeakOutput = Math.Max(PeakOutput, peak);
                if (peak <= 0.00001f || voice.Heard) continue;
                voice.Heard = true;
                Heard++;
                if (Heard <= Capacity)
                    MelonLoader.MelonLogger.Msg($"LSD music return verified nonzero output: '{voice.Source.clip.name}', peak {peak:F5}, return {gain:P1}.");
            }
        }
        catch (Exception error)
        {
            Faulted = true;
            if (Root) Root!.SetActive(false);
            Clear();
            ModSupport.WarnOnce("lsd:music", $"LSD continuous music return stopped: {error.GetBaseException().Message}");
        }
    }

    private void Discover(double now)
    {
        NextDiscovery = now + 8;
        Candidates.Clear();
        var poolIds = new HashSet<int>();
        var sfx = SFXManager.Instance;
        if (sfx && sfx._soundPool is not null)
            foreach (var controller in sfx._soundPool)
                if (controller && controller._audioSource) poolIds.Add(controller._audioSource.GetInstanceID());
        var discoveryStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var controllers = Object.FindObjectsByType(Il2CppType.Of<AudioSourceController>(), FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var item in controllers)
        {
            var controller = item.TryCast<AudioSourceController>();
            if (controller is null || !controller || controller._audioType is not (EAudioType.Music or EAudioType.Ambient)) continue;
            var source = controller ? controller!._audioSource : null;
            if (!source || !source!.clip || (!source.loop && source.clip.length <= 30)
                || poolIds.Contains(source.GetInstanceID())) continue;
            if (Candidates.Count == 256) break;
            Candidates.Add(source);
        }
        if (!LoggedDiscovery)
        {
            LoggedDiscovery = true;
            var milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - discoveryStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            MelonLoader.MelonLogger.Msg($"LSD music observation found {Candidates.Count} native music/ambient long-track/loop sources in {milliseconds:F2}ms; refreshed every eight seconds only during a preview.");
        }
    }

    private static bool IsPlaying(AudioSource source) => source && source.enabled && source.gameObject.activeInHierarchy
        && source.isPlaying && !source.mute && !source.isVirtual && source.volume > 0;

    private static float Audibility(AudioSource source)
    {
        var listener = Il2CppScheduleOne.PlayerScripts.PlayerCamera.Instance;
        // Prefer nearby spatial music when many shops loop simultaneously.
        // Unity applies the exact copied attenuation curve to the return.
        if (!listener || source.spatialBlend <= 0) return source.volume;
        var distance = Vector3.Distance(listener.transform.position, source.transform.position);
        var attenuation = distance >= source.maxDistance ? 0 : Math.Min(1, source.minDistance / Math.Max(0.01f, distance));
        return source.volume * (1 - source.spatialBlend + source.spatialBlend * attenuation);
    }

    private static void Release(Voice voice)
    {
        voice.Source.volume = 0;
        voice.Source.Stop();
        voice.Source.clip = null!;
        voice.Dry = null;
        voice.ClipId = 0;
    }

    internal void Clear()
    {
        foreach (var voice in Voices) if (voice is not null && voice.Source) Release(voice);
        Active = 0;
        NextSelection = NextProbe = LastClock = 0;
        Selected.Clear();
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        try { Clear(); }
        finally
        {
            if (Root) { Root!.SetActive(false); Object.Destroy(Root); }
            Root = null;
            Candidates.Clear();
            Array.Clear(Voices);
        }
    }
}
