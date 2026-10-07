using System.Reflection;
using MelonLoader;

namespace PersonalMods.S1LSDComparison;

// Suspend the personal effect only during a comparison. Never write its config.
internal sealed class BaselineLease : IDisposable
{
    private MelonMod? Mod;

    internal BaselineLease()
    {
        var mod = MelonMod.RegisteredMelons.FirstOrDefault(m =>
            m.GetType().FullName == "PersonalMods.LSDPreview.LSDPreviewMod");
        if (mod is null) return;
        var type = mod.GetType();
        var cleanup = type.GetMethod("Cleanup", BindingFlags.Instance | BindingFlags.NonPublic);
        var update = type.GetMethod(nameof(MelonMod.OnUpdate));
        if (cleanup is null || update is null || !MelonEvents.OnUpdate.CheckIfSubscribed(update, mod))
            throw new InvalidOperationException("Could not suspend the installed personal LSD preview safely.");
        // Suspend only its update callback. Preferences stay unchanged even if
        // another mod saves the global preferences while comparison is active.
        MelonEvents.OnUpdate.Unsubscribe(mod.OnUpdate);
        Mod = mod;
        try { cleanup.Invoke(mod, null); }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        var mod = Mod;
        Mod = null;
        if (mod is not null) {
            MelonEvents.OnUpdate.Subscribe(mod.OnUpdate, mod.Priority);
            MelonLoader.MelonLogger.Msg("S1LSD comparison restored the personal preview callback; preferences unchanged.");
        }
    }
}
