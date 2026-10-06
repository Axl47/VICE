using Il2CppInterop.Runtime;
using UnityEngine;

namespace PersonalMods.LSDPreview;

internal static class AudioReturnSource
{
    internal static AudioSource Create(GameObject parent, string name)
    {
        var child = new GameObject(name) { hideFlags = HideFlags.DontSave };
        child.transform.SetParent(parent.transform, false);
        var source = child.AddComponent(Il2CppType.Of<AudioSource>()).TryCast<AudioSource>();
        if (source is null || !source) throw new InvalidOperationException("The native audio source is unavailable.");
        source.playOnAwake = false;
        source.dopplerLevel = 0;
        source.ignoreListenerVolume = source.ignoreListenerPause = false;
        source.volume = 0;
        return source;
    }

    internal static void Copy(AudioSource dry, AudioSource wet)
    {
        wet.outputAudioMixerGroup = dry.outputAudioMixerGroup;
        wet.spatialBlend = dry.spatialBlend;
        wet.panStereo = dry.panStereo;
        wet.minDistance = dry.minDistance;
        wet.maxDistance = dry.maxDistance;
        wet.rolloffMode = dry.rolloffMode;
        if (dry.rolloffMode == AudioRolloffMode.Custom)
        {
            var curve = dry.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
            if (curve is not null) wet.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
        }
        wet.spread = dry.spread;
        wet.reverbZoneMix = dry.reverbZoneMix;
        wet.bypassEffects = dry.bypassEffects;
        wet.bypassListenerEffects = dry.bypassListenerEffects;
        wet.bypassReverbZones = dry.bypassReverbZones;
        wet.priority = dry.priority;
        wet.transform.position = dry.transform.position;
    }
}
