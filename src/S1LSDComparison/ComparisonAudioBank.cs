using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using S1LSD.Core;
using UnityEngine;
using UnityEngine.Audio;
using AudioMixer = UnityEngine.Audio.AudioMixer;

namespace PersonalMods.S1LSDComparison;

internal sealed class ComparisonAudioBank : IDisposable
{
    private Il2CppAssetBundle? Bundle;
    private readonly AudioMixer?[] Mixers = new AudioMixer?[3];
    private bool Disposed;

    internal AudioMixer Load(int slot)
    {
        if (Disposed || slot < 0 || slot >= Mixers.Length) throw new InvalidOperationException("Invalid comparison audio slot.");
        if (Mixers[slot]) return Mixers[slot]!;
        if (Bundle is null) {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Comparison.Audio.bundle")
                ?? throw new InvalidOperationException("Comparison audio resource missing.");
            using var data = new MemoryStream(); stream.CopyTo(data);
            Bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(data.ToArray()));
            if (Bundle is null) throw new InvalidOperationException("Comparison audio bundle could not load.");
        }
        var mixer = Bundle.LoadAsset<AudioMixer>($"assets/comparison/audio{slot}.mixer");
        if (!mixer) throw new InvalidOperationException("Comparison audio mixer unavailable.");
        Apply(mixer, Neutral);
        Set(mixer, "ReverbDecayHFRatio", 0.5f); Set(mixer, "ReverbReflections", -2602);
        Set(mixer, "ReverbReflectDelay", 0.007f); Set(mixer, "ReverbReverbDelay", 0.011f);
        Set(mixer, "ReverbDiffusion", 85); Set(mixer, "ReverbDensity", 80);
        Set(mixer, "ReverbHFReference", 5000); Set(mixer, "ReverbRoomLF", 0); Set(mixer, "ReverbLFReference", 250);
        Set(mixer, "EchoMaxchannels", 0); Set(mixer, "ChorusDelay", 22); Set(mixer, "ChorusFeedback", 0);
        Set(mixer, "LowpassResonance", 1);
        Mixers[slot] = mixer;
        return mixer;
    }

    internal static AudioTargets Neutral => new() { ReverbRoomMb = -10000, ReverbDecaySeconds = 1.2f,
        ReverbLevelMb = -10000, EchoDelayMs = 180, EchoDry = 1, ChorusDry = 1, LowPassHz = 22000 };

    internal static void Apply(AudioMixer mixer, in AudioTargets t)
    {
        Set(mixer, "ReverbRoom", t.ReverbRoomMb); Set(mixer, "ReverbRoomHF", t.ReverbRoomHfMb);
        Set(mixer, "ReverbDecayTime", Math.Clamp(t.ReverbDecaySeconds, 0.1f, 20));
        Set(mixer, "ReverbReverb", t.ReverbLevelMb); Set(mixer, "ReverbDryLevel", t.ReverbDryMb);
        Set(mixer, "EchoDelay", Math.Clamp(t.EchoDelayMs, 1, 5000)); Set(mixer, "EchoDecay", t.EchoDecay);
        Set(mixer, "EchoDrymix", t.EchoDry); Set(mixer, "EchoWetmix", t.EchoWet);
        Set(mixer, "ChorusDrymix", t.ChorusDry); Set(mixer, "ChorusWetmixtap1", t.ChorusWet);
        Set(mixer, "ChorusWetmixtap2", t.ChorusWet * 0.6f); Set(mixer, "ChorusWetmixtap3", 0);
        Set(mixer, "ChorusDepth", t.ChorusDepth); Set(mixer, "ChorusRate", t.ChorusRateHz);
        Set(mixer, "LowpassCutofffreq", Math.Clamp(t.LowPassHz, 10, 22000));
    }

    internal static void Set(AudioMixer mixer, string name, float value)
    {
        if (!float.IsFinite(value) || !mixer.SetFloat(name, value) || !mixer.GetFloat(name, out var actual)
            || Math.Abs(actual - value) > 0.001f)
            throw new InvalidOperationException($"Comparison mixer could not apply {name}={value}.");
    }

    internal static AudioMixerGroup Input(AudioMixer mixer)
    {
        var groups = mixer.FindMatchingGroups("Master");
        if (groups is null || groups.Length != 1 || !groups[0]) throw new InvalidOperationException("Missing unique comparison audio input.");
        return groups[0];
    }

    public void Dispose()
    {
        if (Disposed) return; Disposed = true;
        foreach (var mixer in Mixers) if (mixer) mixer!.outputAudioMixerGroup = null!;
        Bundle?.Unload(true); Bundle = null; Array.Clear(Mixers, 0, Mixers.Length);
    }
}
