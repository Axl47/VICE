using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

// Own mixer assets only. Reflection accesses Unity's internal authoring API;
// the player uses public AudioMixer parameters and never editor APIs.
public static class BuildComparisonAudio
{
    const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    public static void Build()
    {
        var assembly = typeof(Editor).Assembly;
        var controller = assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
        var effectType = assembly.GetType("UnityEditor.Audio.AudioMixerEffectController", true);
        var exposedType = assembly.GetType("UnityEditor.Audio.ExposedAudioParameter", true);
        var definitions = assembly.GetType("UnityEditor.Audio.MixerEffectDefinitions", true);
        definitions.GetMethod("Refresh", Public).Invoke(null, null);
        var names = new[] { "SFX Reverb", "Echo", "Chorus", "Lowpass" };
        Directory.CreateDirectory("Assets/Comparison");
        var paths = new List<string>();
        for (int slot = 0; slot < 3; slot++) {
            var path = $"Assets/Comparison/Audio{slot}.mixer";
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (!mixer) mixer = (AudioMixer)controller.GetMethod("CreateMixerControllerAtPath", Public).Invoke(null, new object[] { path });
            mixer.name = $"S1LSD Comparison Audio {slot}";
            mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
            var serialized = new SerializedObject(mixer);
            serialized.FindProperty("m_EnableSuspend").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var group = controller.GetProperty("masterGroup", Public).GetValue(mixer);
            var groupType = group.GetType();
            var effects = (Array)groupType.GetProperty("effects", Public).GetValue(group);
            var list = effects.Cast<object>().ToList();
            var exposed = new List<object>();
            foreach (var name in names) {
                var effect = list.FirstOrDefault(e => (string)effectType.GetProperty("effectName", Public).GetValue(e) == name);
                if (effect == null) {
                    effect = Activator.CreateInstance(effectType, new object[] { name });
                    effectType.GetMethod("PreallocateGUIDs", Public).Invoke(effect, null);
                    AssetDatabase.AddObjectToAsset((Object)effect, mixer);
                    groupType.GetMethod("InsertEffect", Public).Invoke(group, new object[] { effect, list.Count });
                    list.Add(effect);
                }
                var parameters = (Array)definitions.GetMethod("GetEffectParameters", Public).Invoke(null, new object[] { name });
                if (parameters.Length == 0) throw new InvalidOperationException("Missing effect: " + name);
                foreach (var parameter in parameters) {
                    var field = parameter.GetType();
                    var key = (string)field.GetField("name", Public).GetValue(parameter);
                    var alias = name.Replace("SFX ", "").Replace(" ", "") + new string(key.Where(char.IsLetterOrDigit).ToArray());
                    var value = Activator.CreateInstance(exposedType);
                    exposedType.GetField("name", Public).SetValue(value, alias);
                    exposedType.GetField("guid", Public).SetValue(value, effectType.GetMethod("GetGUIDForParameter", Public).Invoke(effect, new object[] { key }));
                    exposed.Add(value);
                    if (slot == 0) Debug.Log($"COMPARISON_AUDIO_PARAM {name}/{key} => {alias}; min={field.GetField("minRange", Public).GetValue(parameter)} max={field.GetField("maxRange", Public).GetValue(parameter)} default={field.GetField("defaultValue", Public).GetValue(parameter)}");
                }
            }
            var array = Array.CreateInstance(exposedType, exposed.Count);
            for (int i = 0; i < exposed.Count; i++) array.SetValue(exposed[i], i);
            controller.GetProperty("exposedParameters", Public).SetValue(mixer, array);
            EditorUtility.SetDirty(mixer); EditorUtility.SetDirty((Object)group);
            paths.Add(path);
        }
        AssetDatabase.SaveAssets();
        var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        var output = Path.Combine(repo, "src/S1LSDComparison/Assets");
        var builds = new[] { new AssetBundleBuild { assetBundleName = "comparison-audio.bundle", assetNames = paths.ToArray() } };
        if (BuildPipeline.BuildAssetBundles(output, builds, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableWriteTypeTree | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64) == null)
            throw new InvalidOperationException("Comparison audio build failed.");
        Debug.Log("COMPARISON_AUDIO_BUNDLE_BUILT");
    }
}
