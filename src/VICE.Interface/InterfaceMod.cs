using MelonLoader;
using VICE.Interface.Hud;
using VICE.Core;

[assembly: MelonInfo(typeof(VICE.Interface.InterfaceMod), "VICE Interface", "0.1.1", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: HarmonyDontPatchAll]

namespace VICE.Interface;

public sealed class InterfaceMod : MelonMod
{
    private readonly HudFeature hud = new();
    private bool active;

    public override void OnInitializeMelon()
    {
        active = ModuleCompatibility.CanInitialize("VICE Interface", "Personal.UIEnhancements");
        if (active) hud.Initialize(HarmonyInstance);
    }

    public override void OnUpdate() { if (active) hud.OnUpdate(); }
    public override void OnLateUpdate() { if (active) hud.OnLateUpdate(); }
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) { if (active) hud.OnSceneWasUnloaded(buildIndex, sceneName); }
    public override void OnApplicationQuit() { if (active) hud.OnApplicationQuit(); }

    public override void OnDeinitializeMelon()
    {
        if (!active) return;
        hud.OnDeinitializeMelon();
        HarmonyInstance.UnpatchSelf();
        active = false;
    }
}
