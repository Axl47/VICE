using Il2CppInterop.Runtime;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// An owned listener filter: native sources, pitch, volume and existing filters
// are untouched. Echo is an artistic cue, not a universal reported experience.
internal sealed class PreviewAudio : IDisposable
{
    private readonly Camera Camera;
    private AudioListener? Listener;
    private AudioEchoFilter? Echo;
    private float NextSearch;
    private bool Disposed, Faulted, LoggedWetSignal;

    private static Il2CppSystem.Type ListenerType => Il2CppType.Of<AudioListener>();
    private static Il2CppSystem.Type EchoType => Il2CppType.Of<AudioEchoFilter>();

    internal PreviewAudio(Camera camera) => Camera = camera;

    internal void Tick(float elapsed, float amount)
    {
        if (Disposed || Faulted) return;
        try
        {
            if (amount <= 0.001f) { Mute(); return; }
            if (!Usable(Listener))
            {
                Release();
                if (elapsed < NextSearch) return;
                NextSearch = elapsed + 2;
                Listener = FindListener();
                if (!Usable(Listener))
                    ModSupport.WarnOnce("lsd:no-listener", "LSD audio is waiting for an active AudioListener.");
            }
            if (!Usable(Listener)) return;
            if (Echo is null || !Echo)
            {
                // This game can lack IL2CPP instantiations for generic audio
                // component lookups. Use native Type overloads throughout.
                if (Listener!.GetComponent(EchoType))
                {
                    ModSupport.WarnOnce("lsd:native-echo", "LSD audio leaves an existing listener echo filter in control.");
                    return;
                }
                Echo = Listener.gameObject.AddComponent(EchoType)?.TryCast<AudioEchoFilter>();
                if (Echo is null || !Echo) throw new InvalidOperationException("Unity did not create the preview's echo filter.");
                Echo.enabled = false;
                Echo.hideFlags = HideFlags.DontSave;
                Echo.dryMix = 1;
                Echo.wetMix = 0;
                Echo.delay = 210;
                Echo.decayRatio = 0.18f;
                MelonLoader.MelonLogger.Msg($"LSD echo filter attached to active listener '{Listener.gameObject.name}' (210 ms delay; dry mix 1).");
            }
            Echo.wetMix = ColorBreathing.EchoWetMix(elapsed, amount);
            Echo.enabled = true;
            if (!LoggedWetSignal && Echo.wetMix > 0.005f)
            {
                LoggedWetSignal = true;
                MelonLoader.MelonLogger.Msg($"LSD audio echo is enabled at {Echo.wetMix:P1} wet mix on '{Listener!.gameObject.name}'.");
            }
        }
        catch (Exception error)
        {
            Faulted = true;
            Release();
            ModSupport.WarnOnce("lsd:audio", $"LSD audio layer stopped: {error.GetBaseException().Message}");
        }
    }

    private AudioListener? FindListener()
    {
        if (Camera)
        {
            var listener = InRig(Camera);
            if (Usable(listener)) return listener;
            var node = Camera.transform.parent;
            for (var depth = 0; node && depth < 2; node = node.parent, depth++)
            {
                listener = node.GetComponent(ListenerType)?.TryCast<AudioListener>();
                if (Usable(listener)) return listener;
            }
        }
        // Transport may swap the rendering camera while retaining the player's
        // listener on the regular camera.
        var native = PlayerCamera.Instance;
        if (native)
        {
            var fallback = native.Camera ? InRig(native.Camera) : null;
            if (Usable(fallback)) return fallback;
            fallback = native.CameraContainer ? InRig(native.CameraContainer) : null;
            if (Usable(fallback)) return fallback;
        }
        // An alternate audio rig may live outside the camera hierarchy. Search
        // by listener type only when binding, never once per rendered frame.
        foreach (var candidate in Object.FindObjectsOfType(ListenerType))
        {
            var listener = candidate?.TryCast<AudioListener>();
            if (Usable(listener)) return listener;
        }
        return null;
    }

    private static AudioListener? InRig(Component rig)
    {
        var direct = rig.GetComponent(ListenerType)?.TryCast<AudioListener>();
        if (Usable(direct)) return direct;
        foreach (var component in rig.GetComponentsInChildren(ListenerType, false))
        {
            var listener = component?.TryCast<AudioListener>();
            if (Usable(listener)) return listener;
        }
        return null;
    }

    private static bool Usable(AudioListener? listener)
        => listener is not null && listener && listener.enabled && listener.gameObject.activeInHierarchy;

    private void Mute()
    {
        if (Echo is null || !Echo) return;
        Echo.wetMix = 0;
        Echo.enabled = false;
    }

    private void Release()
    {
        Mute();
        if (Echo is not null && Echo) Object.Destroy(Echo);
        Echo = null;
        Listener = null;
    }

    public void Dispose() { if (Disposed) return; Disposed = true; Release(); }
}
