using MelonLoader;
using PersonalMods.LSDGameplay;
using PersonalMods.LSDPreview;
using VICE.Core;

[assembly: MelonInfo(typeof(VICE.Substances.SubstancesMod), "VICE Substances", "0.1.0", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: HarmonyDontPatchAll]

namespace VICE.Substances;

public sealed class SubstancesMod : MelonMod
{
    private readonly WorkshopFeature workshop = new();
    private readonly PreviewFeature preview = new();
    private bool active;

    public override void OnInitializeMelon()
    {
        if (!ModuleCompatibility.CanInitialize("VICE Substances", "Personal.LSDGameplay", "Personal.LSDPreview")) return;
        try
        {
            // Preserve predecessor order: workshop registration precedes the local consumption effect.
            workshop.Initialize(HarmonyInstance);
            preview.Initialize(HarmonyInstance);
            active = true;
        }
        catch
        {
            try { preview.OnDeinitializeMelon(); }
            finally { try { workshop.OnDeinitializeMelon(); } finally { HarmonyInstance.UnpatchSelf(); } }
            throw;
        }
    }

    public override void OnUpdate() { if (active) { workshop.OnUpdate(); preview.OnUpdate(); } }
    public override void OnLateUpdate() { if (active) preview.OnLateUpdate(); }
    public override void OnSceneWasLoaded(int buildIndex, string sceneName) { if (active) preview.OnSceneWasLoaded(buildIndex, sceneName); }
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (!active) return;
        workshop.OnSceneWasUnloaded(buildIndex, sceneName);
        preview.OnSceneWasUnloaded(buildIndex, sceneName);
    }
    public override void OnApplicationQuit()
    {
        if (!active) return;
        try { workshop.OnApplicationQuit(); } finally { preview.OnApplicationQuit(); }
    }
    public override void OnDeinitializeMelon()
    {
        if (!active) return;
        active = false;
        try { preview.OnDeinitializeMelon(); }
        finally { try { workshop.OnDeinitializeMelon(); } finally { HarmonyInstance.UnpatchSelf(); } }
    }
}
