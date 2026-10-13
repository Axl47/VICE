using HarmonyLib;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppFishNet.Managing;
using MelonLoader;
using PersonalMods.BetterPolice;
using NACops;
using UnityEngine;

[assembly: MelonInfo(typeof(VICE.Enforcement.EnforcementMod), "VICE.Enforcement", "0.1.5", "Axl47; based on XOWithSauce / Frostbyt3 / Babyhamsta")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: HarmonyDontPatchAll]

namespace VICE.Enforcement;

public sealed class EnforcementMod : global::NACops.NACops
{
    private readonly BetterPoliceMod police = new();
    private bool initialized;

    public override void OnInitializeMelon()
    {
        var conflicts = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name)
            .Where(name => name is "Personal.BetterPolice" or "BetterPolice" or "NACops" or "NACops-IL2Cpp").ToArray();
        if (conflicts.Length > 0)
        {
            MelonLogger.Error("VICE Enforcement cannot initialize alongside: " + string.Join(", ", conflicts));
            return;
        }
        Core.DefaultFiles.Seed(typeof(EnforcementMod).Assembly, "VICE.Enforcement.Defaults/",
            NACops.ModDataPaths.Root);
        base.OnInitializeMelon();
        officerConfig = ConfigLoader.LoadOfficerConfig();
        ApplyPersonalPolicy();
        thresholdConfig = ConfigLoader.LoadFrequencyConfig();
        raidConfig = ConfigLoader.LoadRaidConfig();
        surveillanceConfig = ConfigLoader.LoadSurveillanceConfig();
        if (!currentConfig.Enabled) return;
        // Disable automatic PatchAll so configurations exist before any hooks run.
        foreach (var type in typeof(EnforcementMod).Assembly.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false)))
        {
            if (type == typeof(global::NACops.PursuitBehaviour_UpdateArrest_Patch)) continue;
            HarmonyInstance.CreateClassProcessor(type).Patch();
        }
        police.Initialize(HarmonyInstance);
        Information.InformationContact.Initialize();
        initialized = true;
        MelonLogger.Msg("VICE Enforcement 0.1.5 initialized with VICE Core; host police authority and replicated officer/camera presentation.");
    }

    internal static void ApplyPersonalPolicy()
    {
        officerConfig.OverrideMovement = false;
        officerConfig.OverrideVision = false;
        officerConfig.OverrideArresting = false;
        officerConfig.OverrideCombatBeh = false;
    }

    // Poll native save readiness rather than depending on a scene build index.
    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        if (initialized && sceneName == "Main")
        {
            try { Multiplayer.EnforcementReplication.BeginScene(); }
            catch (Exception error) { ModSupport.WarnOnce("enforcement:prefab-scene", "Officer prefab preparation will retry: " + error.Message); }
        }
    }

    public override void OnUpdate()
    {
        if (!initialized) return;
        police.OnUpdate();
        Multiplayer.EnforcementReplication.Tick();
        Information.InformationContact.Tick();
        var load = LoadManager.Instance;
        if (!currentConfig.Enabled || !load || !load.IsGameLoaded || !Player.Local)
        {
            if (registered) EndSession();
            return;
        }
        if (registered) return;
        var network = Il2CppFishNet.InstanceFinder.NetworkManager;
        if (!network || !network.IsServer) return;
        registered = true;
        coros.Add(MelonCoroutines.Start(Setup()));
    }

    internal static void EndSession() => ExitPreTask();

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        police.OnSceneWasUnloaded(buildIndex, sceneName);
        if (sceneName == "Main") EndSession();
    }

    public override void OnApplicationQuit()
    {
        EndSession();
        if (initialized) police.OnApplicationQuit();
    }
}
