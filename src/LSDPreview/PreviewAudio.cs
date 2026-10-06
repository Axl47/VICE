using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Audio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Replay actual listener output through one owned, quiet source. The game
// strips AudioEchoFilter; no native filter, snapshot or source is modified.
internal sealed class PreviewAudio : IDisposable
{
    private readonly Il2CppStructArray<float> Left = new(EchoClock.HistoryFrames), Right = new(EchoClock.HistoryFrames);
    private readonly Il2CppStructArray<float> Packet = new(EchoClock.PacketFrames * 2), Probe = new(EchoClock.PacketFrames);
    private readonly EchoClock Clock = new();
    private readonly Func<int, float> ReadLeft, ReadRight;
    private readonly Action<int, float> WritePacket;
    private GameObject? Root;
    private AudioSource? Source;
    private AudioClip? Clip;
    private Il2CppStructArray<float>? Silence;
    private int Rate, LoopFrames, DelayFrames;
    private bool Running, Disposed, Faulted, Captured, LoggedPlayback;
    private float NextProbe;

    internal PreviewAudio()
    {
        ReadLeft = i => Left[i];
        ReadRight = i => Right[i];
        WritePacket = (i, sample) => Packet[i] = sample;
    }

    internal void Tick(float elapsed, float amount)
    {
        if (Disposed || Faulted) return;
        try
        {
            var manager = AudioManager.Instance;
            if (amount <= 0.001f || AudioListener.pause || AudioListener.volume <= 0
                || (manager && manager.MasterVolume <= 0) || AudioSettings.outputSampleRate <= 0)
            { StopAndClear(); return; }
            if (Rate != AudioSettings.outputSampleRate || Source is null || !Source || Clip is null || !Clip)
            {
                ReleasePlayback();
                Bind();
            }
            if (Source is null || !Source || Clip is null || !Clip) return;
            if (!Running)
            {
                Source.timeSamples = 0;
                Source.Play();
                Running = true;
                Clock.Reset();
            }
            Source.volume = AudioBlend.Sample(elapsed, amount);
            var frames = Clock.Take(AudioSettings.dspTime, Rate);
            var captureWindow = frames + Clock.PendingFrames;
            if (frames < 0 || captureWindow > DelayFrames - EchoClock.PacketFrames)
            { StopAndClear(); return; } // discard a stall, never write behind the playback cursor
            if (frames == 0) return;
            AudioListener.GetOutputData(Left, 0);
            if (AudioSettings.speakerMode == AudioSpeakerMode.Mono) // the return is stereo, mono input duplicates equally
                for (var i = EchoClock.HistoryFrames - captureWindow; i < EchoClock.HistoryFrames; i++) Right[i] = Left[i];
            else AudioListener.GetOutputData(Right, 1);
            var playback = Source.timeSamples;
            var peak = 0f;
            for (var start = 0; start < frames; start += EchoClock.PacketFrames)
            {
                EchoBuffer.CopyStereo(ReadLeft, ReadRight, WritePacket,
                    EchoClock.HistoryFrames - captureWindow + start, EchoClock.PacketFrames);
                if (!Clip.SetData(Packet, EchoBuffer.Destination(playback, DelayFrames, captureWindow, start, LoopFrames)))
                    throw new InvalidOperationException("Unity rejected the echo's PCM data.");
                if (!Captured)
                    for (var i = 0; i < Packet.Length; i++) peak = Math.Max(peak, Math.Abs(Packet[i]));
            }
            if (!Captured && peak > 0.001f)
            {
                Captured = true;
                MelonLoader.MelonLogger.Msg($"LSD echo captured nonzero game output (peak {peak:F4}); delayed PCM is being written to its owned clip.");
            }
            if (Captured && !LoggedPlayback && elapsed >= NextProbe)
            {
                NextProbe = elapsed + 0.5f;
                Source.GetOutputData(Probe, 0);
                var outputPeak = 0f;
                for (var i = 0; i < Probe.Length; i++) outputPeak = Math.Max(outputPeak, Math.Abs(Probe[i]));
                if (outputPeak > 0.00001f && playback > 0)
                {
                    LoggedPlayback = true;
                    MelonLoader.MelonLogger.Msg($"LSD echo source is playing nonzero output (peak {outputPeak:F5}, return gain {Source.volume:P1}, nominal delay 220 ms).");
                }
            }
        }
        catch (Exception error)
        {
            Faulted = true;
            ReleasePlayback();
            ModSupport.WarnOnce("lsd:audio", $"LSD echo stopped: {error.GetBaseException().Message}");
        }
    }

    private void Bind()
    {
        Rate = AudioSettings.outputSampleRate;
        if (Rate < 8000 || Rate > 192000) throw new InvalidOperationException($"Unsupported audio sample rate: {Rate}.");
        LoopFrames = Rate * 2;
        DelayFrames = (int)(Rate * EchoBuffer.DelaySeconds);
        Silence = new Il2CppStructArray<float>(LoopFrames * 2);
        Clip = AudioClip.Create("Personal LSD echo PCM", LoopFrames, 2, Rate, false);
        if (!Clip || !Clip.SetData(Silence, 0)) throw new InvalidOperationException("The owned PCM clip could not be initialized.");
        Clip.hideFlags = HideFlags.DontSave;
        Root = new GameObject("Personal LSD audio return") { hideFlags = HideFlags.DontSave };
        Source = Root.AddComponent(Il2CppType.Of<AudioSource>()).TryCast<AudioSource>();
        if (Source is null || !Source) throw new InvalidOperationException("The native audio source is unavailable.");
        Source.playOnAwake = false;
        Source.loop = true;
        Source.clip = Clip;
        Source.spatialBlend = 0;
        Source.dopplerLevel = 0;
        Source.priority = 32;
        Source.volume = 0;
        Source.bypassEffects = Source.bypassListenerEffects = Source.bypassReverbZones = true;
        // Captured output has already passed through the game's volume/mix
        // controls. Do not send the return through that mixer a second time.
        Source.outputAudioMixerGroup = null;
        Source.ignoreListenerVolume = Source.ignoreListenerPause = false;
        MelonLoader.MelonLogger.Msg($"LSD echo initialized actual listener-output capture at {Rate} Hz with a 220 ms delayed stereo return.");
    }

    private void StopAndClear()
    {
        if (Source is not null && Source)
        {
            Source.volume = 0;
            if (Running) Source.Stop();
        }
        if (Running && Clip is not null && Clip && Silence is not null) Clip.SetData(Silence, 0);
        Running = false;
        Clock.Reset();
    }

    private void ReleasePlayback()
    {
        StopAndClear();
        if (Root is not null && Root) { Root.SetActive(false); Object.Destroy(Root); }
        if (Clip is not null && Clip) Object.Destroy(Clip);
        Source = null;
        Clip = null;
        Root = null;
        Silence = null;
        Rate = 0;
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        ReleasePlayback();
    }
}
