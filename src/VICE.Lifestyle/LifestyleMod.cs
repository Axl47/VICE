using MelonLoader;
using VICE.Lifestyle.Casino;
using VICE.Lifestyle.Television;
using VICE.Core;
using VICE.Lifestyle.Amenities;

[assembly: MelonInfo(typeof(VICE.Lifestyle.LifestyleMod), "VICE Lifestyle", "0.2.1", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: HarmonyDontPatchAll]

namespace VICE.Lifestyle;

public sealed class LifestyleMod : MelonMod
{
    private readonly CasinoFeature casino = new();
    private readonly TelevisionFeature television = new();
    private readonly AmenitiesFeature amenities = new();
    private bool active;

    public override void OnInitializeMelon()
    {
        active = ModuleCompatibility.CanInitialize("VICE Lifestyle", "Personal.Casino", "Personal.TV", "Personal.TVProof");
        if (!active) return;
        casino.Initialize(HarmonyInstance);
        television.Initialize(HarmonyInstance);
        amenities.Initialize(HarmonyInstance);
    }

    public override void OnUpdate() { if (active) { television.OnUpdate(); amenities.OnUpdate(); } }

    public override void OnDeinitializeMelon()
    {
        if (!active) return;
        television.OnDeinitializeMelon();
        amenities.OnDeinitializeMelon();
        HarmonyInstance.UnpatchSelf();
        active = false;
    }
}
