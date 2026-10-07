using MelonLoader;
using VICE.Arsenal.Guns;
using VICE.Core;

[assembly: MelonInfo(typeof(VICE.Arsenal.ArsenalMod), "VICE Arsenal", "0.1.0", "Axl47; original weapon assets by Voidane")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: HarmonyDontPatchAll]

namespace VICE.Arsenal;

public sealed class ArsenalMod : MelonMod
{
    private readonly GunsFeature guns = new();
    private bool active;

    public override void OnInitializeMelon()
    {
        active = ModuleCompatibility.CanInitialize("VICE Arsenal", "Personal.MoreGuns", "MoreGuns");
        if (active) guns.Initialize(HarmonyInstance);
    }

    public override void OnLateInitializeMelon() { if (active) guns.OnLateInitializeMelon(); }
    public override void OnSceneWasLoaded(int buildIndex, string sceneName) { if (active) guns.OnSceneWasLoaded(buildIndex, sceneName); }
    public override void OnUpdate() { if (active) guns.OnUpdate(); }
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) { if (active) guns.OnSceneWasUnloaded(buildIndex, sceneName); }

    public override void OnDeinitializeMelon()
    {
        if (!active) return;
        guns.OnSceneWasUnloaded(0, "Main");
        HarmonyInstance.UnpatchSelf();
        active = false;
    }
}
