using Il2CppScheduleOne.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace PersonalMods.LSDPreview;

// Process the actual final native buses, including pooled one-shots and shop
// music. No source discovery, clip duplication, PCM capture or playback hooks.
internal sealed class PreviewAudio : IDisposable
{
    private sealed class Route(AudioMixer native, AudioMixerGroup? previous, AudioMixer echo, AudioMixerGroup input, int slot)
    {
        internal readonly AudioMixer Native = native, Echo = echo;
        internal readonly AudioMixerGroup? Previous = previous;
        internal readonly AudioMixerGroup Input = input;
        internal readonly int Slot = slot;
    }
    private readonly EchoMixerBank Bank = new();
    private readonly List<Route> Routes = new();
    private bool Disposed, Faulted, Reported;
    private float NextCheck;

    internal void Tick(float elapsed, float amount, float closedEyes = 0)
    {
        if (Disposed || Faulted) return;
        try
        {
            if (amount <= 0 || AudioListener.pause || AudioListener.volume <= 0)
            { ReleaseRoutes(); return; }
            if (Time.realtimeSinceStartup >= NextCheck)
            {
                NextCheck = Time.realtimeSinceStartup + 0.5f;
                BindNativeBuses();
            }
            var wet = AudioBlend.Sample(elapsed, amount, closedEyes);
            foreach (var route in Routes) EchoMixerBank.Set(route.Echo, "EchoWet", wet);
            if (!Reported && elapsed >= 30 && Routes.Count > 0)
            {
                Reported = true;
                MelonLoader.MelonLogger.Msg($"LSD direct bus echo: {Routes.Count} native buses routed, original music/ambience/SFX processed at 220ms delay, wet {wet:P1}, dry 100%; no replayed sources.");
            }
        }
        catch (Exception error)
        {
            Faulted = true;
            ReleaseRoutes();
            ModSupport.WarnOnce("lsd:audio", $"LSD direct bus echo unavailable: {error.GetBaseException().Message}");
        }
    }

    private void BindNativeBuses()
    {
        var manager = AudioManager.Instance;
        if (!manager) return;
        var buses = new List<AudioMixer>();
        foreach (var group in new[] { manager.MainGameMixer, manager.MusicMixer, manager.MenuMixer })
        {
            var mixer = group ? group.audioMixer : null;
            if (mixer && !buses.Any(b => b.Pointer == mixer!.Pointer)) buses.Add(mixer!);
        }
        for (var i = Routes.Count - 1; i >= 0; i--)
        {
            var route = Routes[i];
            if (!route.Native || !buses.Any(b => b.Pointer == route.Native.Pointer)
                || !Same(route.Native.outputAudioMixerGroup, route.Input))
            { Release(route); Routes.RemoveAt(i); }
        }
        foreach (var native in buses)
        {
            if (Routes.Any(r => r.Native.Pointer == native.Pointer)) continue;
            var previous = native.outputAudioMixerGroup;
            // If this bus feeds another native bus that is already processed,
            // echo only the terminal bus so nested routing never doubles it.
            if (previous && buses.Any(b => b.Pointer == previous!.audioMixer.Pointer)) continue;
            var slot = Enumerable.Range(0, 3).First(i => !Routes.Any(r => r.Slot == i));
            var echo = Bank.Load(slot);
            var input = EchoMixerBank.Input(echo);
            echo.outputAudioMixerGroup = previous!;
            var route = new Route(native, previous, echo, input, slot);
            // Keep the lease before the first native write so failures restore it.
            Routes.Add(route);
            native.outputAudioMixerGroup = input;
            if (!Same(native.outputAudioMixerGroup, input)) throw new InvalidOperationException("Unity rejected an echo bus route.");
            MelonLoader.MelonLogger.Msg($"LSD echo attached after native mixer '{native.name}', feeding '{(previous ? previous!.name : "listener")}'.");
        }
    }

    private static bool Same(AudioMixerGroup? first, AudioMixerGroup? second)
        => (first ? first!.Pointer : IntPtr.Zero) == (second ? second!.Pointer : IntPtr.Zero);

    private static void Release(Route route)
    {
        if (route.Echo) route.Echo.SetFloat("EchoWet", 0);
        // Restore only the connection still owned by this preview. Other
        // components are allowed to replace a route while the preview runs.
        if (route.Native && Same(route.Native.outputAudioMixerGroup, route.Input))
        {
            route.Native.outputAudioMixerGroup = route.Previous!;
            if (!Same(route.Native.outputAudioMixerGroup, route.Previous))
                throw new InvalidOperationException("Unity did not restore the original audio bus route.");
        }
        if (route.Echo) route.Echo.outputAudioMixerGroup = null!;
    }

    private void ReleaseRoutes()
    {
        foreach (var route in Routes) Release(route);
        Routes.Clear();
        NextCheck = 0;
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        ReleaseRoutes();
        Bank.Dispose();
    }
}
