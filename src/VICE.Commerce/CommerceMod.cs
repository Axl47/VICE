using MelonLoader;
using VICE.Commerce.Purchasing;
using VICE.Commerce.Trading;
using VICE.Commerce.Warehouse;
using VICE.Core;

[assembly: MelonInfo(typeof(VICE.Commerce.CommerceMod), "VICE Commerce", "0.1.1", "Axl47; trading based on zocke18r, SFox and Virtunerd")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: HarmonyDontPatchAll]

namespace VICE.Commerce;

public sealed class CommerceMod : MelonMod
{
    private readonly TradingFeature trading = new();
    private readonly PurchasingFeature purchasing = new();
    private readonly WarehouseFeature warehouse = new();
    private bool active;

    public override void OnInitializeMelon()
    {
        active = ModuleCompatibility.CanInitialize("VICE Commerce", "Personal.Highballer", "Personal.GasPurchases", "Personal.Warehouse",
            "HighBaller", "HighBaller_IL2CPP", "WareHouseAlwaysOpen");
        if (!active) return;
        trading.Initialize(HarmonyInstance);
        purchasing.Initialize(HarmonyInstance);
        warehouse.Initialize(HarmonyInstance);
    }

    public override void OnUpdate() { if (active) purchasing.OnUpdate(); }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (!active) return;
        trading.OnSceneWasUnloaded(buildIndex, sceneName);
        purchasing.OnSceneWasUnloaded(buildIndex, sceneName);
        warehouse.OnSceneWasUnloaded(buildIndex, sceneName);
    }

    public override void OnApplicationQuit() { if (active) purchasing.OnApplicationQuit(); }

    public override void OnDeinitializeMelon()
    {
        if (!active) return;
        OnSceneWasUnloaded(0, "Main");
        HarmonyInstance.UnpatchSelf();
        active = false;
    }
}
