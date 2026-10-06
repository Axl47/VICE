using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Observe the native SFX pool during a preview, then schedule one quieter
// copy of newly observed clips. No native playback method is detoured. Owned
// returns never enter the game's pool, so they cannot trigger another echo.
internal sealed class PreviewAudio : IDisposable
{
    private sealed class Voice(AudioSource source)
    {
        internal readonly AudioSource Source = source;
        internal float DryVolume;
        internal double Start;
        internal bool Heard;
    }

    private readonly Voice?[] Pool = new Voice?[EchoVoices.Capacity];
    private readonly EchoVoices Reservations = new();
    private readonly SoundStarts Starts = new();
    private readonly Il2CppStructArray<float> Probe = new(512);
    private GameObject? Root;
    private float Gain, PeakGain, PeakOutput;
    private double NextProbe;
    private int Seen, Scheduled, Heard, Dropped, Ignored, Reports;
    private bool Disposed, Faulted, LoggedSchedule, LoggedOutput, Primed, LoggedPool;

    private void ObserveNativeSounds(double now)
    {
        var manager = SFXManager.Instance;
        if (!manager) return;
        var sounds = manager._soundsInUse;
        if (sounds is null) return;
        if (!LoggedPool)
        {
            LoggedPool = true;
            MelonLoader.MelonLogger.Msg("LSD echo observing the native footsteps/impact sound pool; no playback hooks are installed.");
        }
        Starts.BeginFrame();
        var count = Math.Min(sounds.Count, SoundStarts.Capacity);
        for (var index = 0; index < count; index++)
        {
            var controller = sounds[index];
            var source = controller ? controller._audioSource : null;
            if (source is null || !source) continue;
            var clip = source.clip;
            // SFXManager reserves these sources for one-shot pool sounds.
            // AudioSource.loop does not describe a PlayOneShot voice.
            var playing = source.isPlaying;
            var started = Starts.Observe(source.GetInstanceID(), clip ? clip.GetInstanceID() : 0, playing, source.timeSamples);
            if (Primed && started)
            {
                Capture(source, now);
                if (Faulted) return;
            }
        }
        Starts.EndFrame();
        // Establish a baseline at first activation and after pause/mute. Never
        // replay a sound that was already playing when observation resumed.
        Primed = true;
    }

    private static bool CanHear()
    {
        if (AudioListener.pause || AudioListener.volume <= 0 || AudioSettings.outputSampleRate <= 0
            || Time.timeScale <= 0 || (PauseMenu.Instance && PauseMenu.Instance.IsPaused)) return false;
        var manager = AudioManager.Instance;
        return !manager || manager.MasterVolume > 0;
    }

    private void Capture(AudioSource dry, double observedTime)
    {
        if (Disposed || Faulted) return;
        try
        {
            Seen++;
            if (Gain <= 0.0001f || !CanHear()) { Ignored++; return; }
            var clip = dry ? dry.clip : null;
            // Only SFXManager's active pool is observed; music, ambient loops
            // and unrelated sources never enter this path.
            if (!dry || !dry.enabled || !dry.gameObject.activeInHierarchy || dry.mute || dry.volume <= 0
                || !dry.isPlaying || clip is null || !clip || clip.samples <= 0
                || clip.frequency <= 0 || clip.length > 30)
            { Ignored++; return; }
            var now = AudioSettings.dspTime;
            var seconds = clip.samples / (double)clip.frequency;
            if (!Reservations.TryReserve(observedTime, now, seconds, dry.pitch, out var slot)) { Dropped++; return; }
            try
            {
                var voice = Pool[slot.Index];
                if (voice is null || !voice.Source) Pool[slot.Index] = voice = CreateVoice(slot.Index);
                var source = voice.Source;
                source.Stop();
                source.clip = clip;
                source.loop = false;
                source.pitch = dry.pitch;
                source.timeSamples = 0;
                source.outputAudioMixerGroup = dry.outputAudioMixerGroup;
                source.spatialBlend = dry.spatialBlend;
                source.panStereo = dry.panStereo;
                source.minDistance = dry.minDistance;
                source.maxDistance = dry.maxDistance;
                source.rolloffMode = dry.rolloffMode;
                if (dry.rolloffMode == AudioRolloffMode.Custom)
                {
                    var curve = dry.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                    if (curve is not null) source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
                }
                source.spread = dry.spread;
                source.reverbZoneMix = dry.reverbZoneMix;
                source.bypassEffects = dry.bypassEffects;
                source.bypassListenerEffects = dry.bypassListenerEffects;
                source.bypassReverbZones = dry.bypassReverbZones;
                source.priority = dry.priority;
                source.transform.position = dry.transform.position;
                voice.DryVolume = dry.volume;
                voice.Start = slot.Start;
                voice.Heard = false;
                source.volume = voice.DryVolume * Gain;
                // Use Unity's audio clock, not a coroutine or Update countdown.
                // Slots stay reserved across the delay and until playback ends.
                if (slot.Start < AudioSettings.dspTime + EchoVoices.MinimumLeadSeconds)
                {
                    Reservations.Release(slot.Index);
                    source.volume = 0;
                    Dropped++;
                    return;
                }
                source.PlayScheduled(slot.Start);
                Scheduled++;
                if (!LoggedSchedule)
                {
                    LoggedSchedule = true;
                    var mixer = dry.outputAudioMixerGroup;
                    MelonLoader.MelonLogger.Msg($"LSD echo scheduled game clip '{clip.name}' at +220 ms from observation: dry volume {dry.volume:F3}, pitch {dry.pitch:F2}, mixer '{(mixer ? mixer.name : "listener")}', return {Gain:P1}.");
                }
            }
            catch { Reservations.Release(slot.Index); throw; }
        }
        catch (Exception error) { Fail(error); }
    }

    private Voice CreateVoice(int index)
    {
        if (Root is null || !Root) Root = new GameObject("Personal LSD delayed sound pool") { hideFlags = HideFlags.DontSave };
        var child = new GameObject($"LSD echo voice {index}") { hideFlags = HideFlags.DontSave };
        child.transform.SetParent(Root.transform, false);
        var source = child.AddComponent(Il2CppType.Of<AudioSource>()).TryCast<AudioSource>();
        if (source is null || !source) throw new InvalidOperationException("The native audio source is unavailable.");
        source.playOnAwake = false;
        source.dopplerLevel = 0;
        source.ignoreListenerVolume = source.ignoreListenerPause = false;
        source.volume = 0;
        source.GetOutputData(Probe, 0); // warm the diagnostic analysis buffer before playback
        return new Voice(source);
    }

    internal void Tick(float elapsed, float amount)
    {
        if (Disposed || Faulted) return;
        try
        {
            if (amount <= 0.001f || !CanHear()) { StopAndClear(); return; }
            var now = AudioSettings.dspTime;
            if (!Reservations.Advance(now)) StopAndClear();
            Gain = AudioBlend.Sample(elapsed, amount);
            PeakGain = Math.Max(PeakGain, Gain);
            ObserveNativeSounds(now);
            if (Faulted) return;
            var probe = now >= NextProbe;
            if (probe) NextProbe = now + 0.05;
            for (var index = 0; index < Pool.Length; index++)
            {
                var voice = Pool[index];
                if (voice is null || !voice.Source) { Reservations.Release(index); continue; }
                if (!Reservations.Busy(index, now))
                {
                    if (voice.DryVolume > 0)
                    {
                        voice.Source.volume = 0;
                        voice.Source.Stop();
                        voice.Source.clip = null!;
                        voice.DryVolume = 0;
                    }
                    continue;
                }
                voice.Source.volume = voice.DryVolume * Gain;
                if (!probe || now < voice.Start || !voice.Source.isPlaying) continue;
                voice.Source.GetOutputData(Probe, 0);
                var peak = 0f;
                for (var sample = 0; sample < Probe.Length; sample++) peak = Math.Max(peak, Math.Abs(Probe[sample]));
                PeakOutput = Math.Max(PeakOutput, peak);
                if (peak > 0.00001f && !voice.Heard)
                {
                    voice.Heard = true;
                    Heard++;
                    if (!LoggedOutput)
                    {
                        LoggedOutput = true;
                        MelonLoader.MelonLogger.Msg($"LSD echo played a delayed game clip with nonzero output (peak {peak:F5}, return {Gain:P1}).");
                    }
                }
            }
            var checkpoint = Reports == 0 ? 10 : Reports == 1 ? 30 : 60;
            if (Reports < 3 && elapsed >= checkpoint)
            {
                Reports++;
                MelonLoader.MelonLogger.Msg($"LSD echo check at {elapsed:F0}s: observed starts {Seen}, scheduled {Scheduled}, nonzero voices {Heard}, ignored {Ignored}, dropped {Dropped}, current return {Gain:P1}, peak return {PeakGain:P1}, output peak {PeakOutput:F5}.");
                if (elapsed >= 30 && (Scheduled == 0 || Heard == 0))
                    ModSupport.WarnOnce("lsd:audio:signal", "LSD echo has not verified delayed sound output. Keep the echo check lines for diagnosis.");
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private void StopAndClear()
    {
        Gain = 0;
        NextProbe = 0;
        Reservations.Reset();
        Starts.Reset();
        Primed = false;
        foreach (var voice in Pool)
            if (voice is not null && voice.Source)
            {
                voice.Source.volume = 0;
                voice.Source.Stop();
                voice.Source.clip = null!;
                voice.DryVolume = 0;
            }
    }

    private void Fail(Exception error)
    {
        Faulted = true;
        Gain = 0;
        try
        {
            if (Root is not null && Root) Root.SetActive(false);
            StopAndClear();
        }
        catch (Exception cleanupError)
        {
            ModSupport.WarnOnce("lsd:audio:stop", $"LSD audio cleanup failed: {cleanupError.GetBaseException().Message}");
        }
        Reservations.Reset();
        ModSupport.WarnOnce("lsd:audio", $"LSD delayed sound return stopped: {error.GetBaseException().Message}");
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        try { StopAndClear(); }
        finally
        {
            if (Root is not null && Root) { Root.SetActive(false); Object.Destroy(Root); }
            Root = null;
            Array.Clear(Pool);
        }
    }
}
