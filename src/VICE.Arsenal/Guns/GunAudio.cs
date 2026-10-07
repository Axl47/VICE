using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.Core.Audio;
using UnityEngine;

namespace VICE.Arsenal.Guns;

internal static class GunAudio
{
    private sealed record Sound(AudioSource Source, AudioSourceController? Controller, GunOptions Options, float BaseVolume);
    private static readonly Dictionary<int, Sound> Sounds = new();
    private static float NextRefresh;

    internal static void Configure(GameObject root, GunOptions options, bool avatar)
    {
        foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
        {
            var key = source.GetInstanceID();
            if (Sounds.ContainsKey(key)) continue;
            var controller = source.GetComponent<AudioSourceController>();
            // Legacy DefaultVolume/AudioType were renamed in the beta and otherwise
            // deserialize to unrelated defaults. Recover the bundle's intended levels.
            var baseVolume = avatar ? 0.35f : source.gameObject.name == "Fire Sound" ? 0.8f
                : source.gameObject.name == "Reload Sound" && options.Id == "ak47" ? 0.35f : 0.12f;
            if (controller)
            {
                controller._audioSource = source;
                controller._audioType = EAudioType.FX;
                controller._defaultBaseVolume = baseVolume;
                controller._defaultBasePitch = 1;
                controller._pitchMultiplier = 1;
                controller._minRandomPitch = 0.9f;
                controller._maxRandomPitch = 1;
                controller.SetBasePitch(1);
            }
            var sound = new Sound(source, controller, options, baseVolume);
            Sounds[key] = sound;
            Apply(sound);
        }
    }

    private static void Apply(Sound sound)
    {
        var gain = GunOptions.Safe(sound.Options.SoundVolumeMultiplier.Value, sound.Options.Id == "ak47" ? 0.4f : 0.25f, 0, 1);
        var manager = AudioManager.Instance;
        if (sound.Controller is { } controller && controller)
        {
            controller._volumeMultiplier = gain;
            controller.SetBaseVolume(sound.BaseVolume); // Native master × FX volume, including mute.
        }
        else
            sound.Source.volume = sound.BaseVolume * gain * (manager ? manager.GetVolume(EAudioType.FX, true) : 1);
        if (manager && manager.MainGameMixer) sound.Source.outputAudioMixerGroup = manager.MainGameMixer;
    }

    internal static void Refresh()
    {
        if (Time.realtimeSinceStartup < NextRefresh) return;
        NextRefresh = Time.realtimeSinceStartup + 0.25f;
        foreach (var pair in Sounds.ToArray())
        {
            if (!pair.Value.Source) { Sounds.Remove(pair.Key); continue; }
            try { Apply(pair.Value); }
            catch (Exception error) { ModSupport.WarnOnce($"guns:audio:{pair.Value.Options.Id}", $"Weapon audio settings unavailable: {error.Message}"); }
        }
    }
}
