using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Audio;
using UnityEngine.Audio;

namespace PersonalMods.LSDPreview;

// This build strips AudioEchoFilter's native class. Blend the game's existing
// mixer snapshots instead of attempting to add an unavailable Unity filter.
internal sealed class PreviewAudio : IDisposable
{
    private static IntPtr NativeManager;
    private static bool NativeDistorted, ObserverReady;
    private static uint NativeRevision;
    private readonly AudioSnapshotLease Lease = new();
    private AudioManager? Manager;
    private AudioMixer? Mixer;
    private Il2CppReferenceArray<AudioMixerSnapshot>? Snapshots;
    private readonly Il2CppStructArray<float> Weights = new(2);
    private float NextUpdate;
    private bool Disposed, Faulted, LoggedSignal;

    internal static void ObserveNativeAudio(HarmonyLib.Harmony harmony)
        => ObserverReady = ModSupport.Patch(harmony, typeof(AudioManager), nameof(AudioManager.SetDistorted),
            typeof(PreviewAudio), postfix: nameof(NativeTransition), parameters: new[] { typeof(bool), typeof(float) });

    // Observe only: leave every native call intact, including mushroom/product
    // audio. Installed before a save loads, so its transitions are tracked.
    private static void NativeTransition(AudioManager __instance, bool distorted)
    {
        NativeManager = __instance.Pointer;
        NativeDistorted = distorted;
        unchecked { NativeRevision++; }
    }

    internal void Tick(float elapsed, float amount)
    {
        if (Disposed || Faulted || !ObserverReady) return;
        try
        {
            if (amount <= 0.001f) { Restore(); return; }
            var manager = AudioManager.Instance;
            if (!manager) return;
            if (Manager is null || !Manager || Manager.Pointer != manager.Pointer)
            {
                Restore();
                Bind(manager);
            }
            if (Mixer is null || !Mixer || Snapshots is null) return;
            // Native intoxication takes precedence. Its transition also makes
            // our previous write ineligible for restoration.
            if (NativeManager == manager.Pointer && NativeDistorted) { Lease.Release(); return; }
            if (elapsed < NextUpdate && Lease.CanRestore(NativeRevision)) return;
            NextUpdate = elapsed + 0.25f;
            var mix = AudioBlend.Sample(elapsed, amount);
            Weights[0] = 1 - mix;
            Weights[1] = mix;
            Mixer.TransitionToSnapshots(Snapshots, Weights, 0.3f);
            Lease.Written(NativeRevision);
            if (!LoggedSignal && mix > 0.03f)
            {
                LoggedSignal = true;
                MelonLoader.MelonLogger.Msg($"LSD audio is blending the native distorted mixer snapshot at {mix:P1}; no echo filter is required.");
            }
        }
        catch (Exception error)
        {
            Faulted = true;
            Restore();
            ModSupport.WarnOnce("lsd:audio", $"LSD audio layer stopped: {error.GetBaseException().Message}");
        }
    }

    private void Bind(AudioManager manager)
    {
        Manager = manager;
        var normal = manager._defaultSnapshot;
        var distorted = manager._distortedSnapshot;
        if (!normal || !distorted || !normal.audioMixer || normal.audioMixer.Pointer != distorted.audioMixer.Pointer)
            throw new InvalidOperationException("The game's normal and distorted audio snapshots are unavailable or use different mixers.");
        Mixer = normal.audioMixer;
        Snapshots = new Il2CppReferenceArray<AudioMixerSnapshot>(new[] { normal, distorted });
        NextUpdate = 0;
        MelonLoader.MelonLogger.Msg($"LSD audio bound native snapshots '{normal.name}' and '{distorted.name}' on '{Mixer.name}'.");
    }

    private void Restore()
    {
        try
        {
            if (Manager is not null && Manager && Lease.CanRestore(NativeRevision))
            {
                var distorted = NativeManager == Manager.Pointer && NativeDistorted;
                var snapshot = distorted ? Manager._distortedSnapshot : Manager._defaultSnapshot;
                if (snapshot) snapshot.TransitionTo(0.1f);
            }
        }
        finally { Lease.Release(); }
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Restore();
        Manager = null;
        Mixer = null;
        Snapshots = null;
    }
}
