using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Audio;

namespace PersonalMods.LSDPreview;

internal sealed class EchoMixerBank : IDisposable
{
    private Il2CppAssetBundle? Bundle;
    private readonly AudioMixer?[] Mixers = new AudioMixer?[4];
    private bool Disposed;

    internal AudioMixer Load(int slot)
    {
        if (Disposed || slot < 0 || slot >= Mixers.Length) throw new InvalidOperationException("Invalid owned echo mixer slot.");
        if (Mixers[slot]) return Mixers[slot]!;
        if (Bundle is null)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Personal.LSDPreview.Echo.bundle")
                ?? throw new InvalidOperationException("The owned echo mixer resource is missing.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            Bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(memory.ToArray()));
            if (Bundle is null) throw new InvalidOperationException("Unity could not load the owned echo mixers.");
        }
        var mixer = Bundle.LoadAsset<AudioMixer>($"assets/personal/lsdecho{slot}.mixer");
        if (!mixer) throw new InvalidOperationException("An owned echo mixer is unavailable.");
        Set(mixer, "EchoDelay", 220);
        Set(mixer, "EchoDecay", 0.18f);
        Set(mixer, "EchoDry", 1);
        Set(mixer, "EchoWet", 0);
        Mixers[slot] = mixer;
        return mixer;
    }

    internal static void Set(AudioMixer mixer, string name, float value)
    {
        if (!mixer.SetFloat(name, value) || !mixer.GetFloat(name, out var written) || Math.Abs(written - value) > 0.0001f)
            throw new InvalidOperationException($"The owned mixer could not apply '{name}'.");
    }

    internal static AudioMixerGroup Input(AudioMixer mixer)
    {
        var groups = mixer.FindMatchingGroups("Master");
        if (groups is null || groups.Length != 1 || !groups[0])
            throw new InvalidOperationException("The owned echo mixer has no unique master input.");
        return groups[0];
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        // The route owner restores native outputs before releasing this bank.
        foreach (var mixer in Mixers)
            if (mixer) { mixer!.SetFloat("EchoWet", 0); mixer.outputAudioMixerGroup = null!; }
        Bundle?.Unload(true);
        Bundle = null;
        Array.Clear(Mixers, 0, Mixers.Length);
    }
}
