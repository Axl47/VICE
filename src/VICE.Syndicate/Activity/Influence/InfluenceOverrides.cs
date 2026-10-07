using VICE.Syndicate.Compatibility;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using MelonLoader;

using static VICE.Syndicate.SyndicateMod;
using static VICE.Syndicate.DebugModule;
using static VICE.Syndicate.CartelGathering;

using Il2CppScheduleOne.Graffiti;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs.Relation;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppFishNet;

namespace VICE.Syndicate
{
    public static class InfluenceOverrides
    {
        public static bool ShouldChangeInfluence(EMapRegion region)
        {
            bool changeInfluence = false;
            if (NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                changeInfluence = false;

            foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
            {
                if (unlmapReg == region && InstanceFinder.IsServer)
                {
                    changeInfluence = true;
                    break;
                }
            }
            if (region == EMapRegion.Northtown)
                changeInfluence = false;
            return changeInfluence;
        }

        public static IEnumerator OnDayChangeApplyPassiveGain()
        {
            foreach (MapRegionData region in Singleton<Map>.Instance.Regions)
            {
                yield return Wait05;
                if (ShouldChangeInfluence(region.Region))
                {
                    NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Influence.ChangeInfluence(region.Region, influenceConfig.passiveInfluenceGainPerDay);
                }
            }
            yield return null;
        }

        public static void OnDayPassChangePassive()
        {
            if (influenceConfig.passiveInfluenceGainPerDay == 0f) return;
            Log("On Day Pass Change Passive Influence");
            coros.Add(MelonCoroutines.Start(OnDayChangeApplyPassiveGain()));
        }
    }

    // Passive influence gain is now no longer default source and purely additive by mod (0.4.1f13)
    [Serializable]
    public class InfluenceConfig
    {
        // Mod added
        public float interceptFail = 0.025f;
        public float interceptSuccess = -0.050f;

        public float deadDropFail = 0.025f;
        public float deadDropSuccess = -0.050f;

        public float gatheringFail = 0.025f;
        public float gatheringSuccess = -0.080f;

        public float robberyPlayerEscape = 0.025f; // Player out of range
        public float robberyGoonEscapeSuccess = 0.025f; // goon escapes to dealer succesfully
        public float robberyGoonDead = -0.080f; // Player kills before goon kills dealer
        public float robberyGoonEscapeDead = -0.050f; // Player kills while goon is escaping

        public float sabotageBombDefused = -0.150f;
        public float sabotageGoonKilled = -0.050f;
        public float sabotageBombExploded = 0.200f;

        // Truced interaction
        public float cartelDealerPersuaded = -0.100f;
        public float trucedGreetingChallenge = -0.150f; // Player greets all 3 gather goons in short timeframe

        // used to be in source
        public float passiveInfluenceGainPerDay = 0.025f;

        // Game defaults here
        public float cartelDealerDied = -0.100f;
        public float ambushDefeated = -0.100f;
        public float graffitiInfluenceReduction = -0.050f;
        public float customerUnlockInfluenceChange = -0.075f;
    }

    // Patch the cartel dealer on died to have modifiable influence
    [HarmonyPatch(typeof(CartelDealer), "DiedOrKnockedOut")]
    public static class CartelDealer_Died_Patch
    {
        public static bool Prefix(CartelDealer __instance)
        {
            if (!InstanceFinder.IsServer)
                return false;
            // Hostile status
            if (NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
            {
                return false;
            }


            NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Influence.ChangeInfluence(__instance.Region, influenceConfig.cartelDealerDied);
            return false;
        }
    }

    // Custom patch to monitor ambush so that there is no need to do opcodes or other shenanigans for the nested ienumerator
    [HarmonyPatch(typeof(Ambush), "SpawnAmbush")]
    public static class Ambush_SpawnAmbush_Patch
    {
        public static bool Prefix(Ambush __instance, Player target, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3> potentialSpawnPoints)
        {
            Log("Prefix Ambush");
            List<CartelGoon> currentlySpawned = [.. NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.spawnedGoons];
            coros.Add(MelonCoroutines.Start(OverMonitorAmbush(potentialSpawnPoints.ToArray(), currentlySpawned, __instance.Region)));
            Log("Continue Ambush");
            return true;
        }

        public static IEnumerator OverMonitorAmbush(Vector3[] potentialSpawnPoints, List<CartelGoon> currentlySpawned, EMapRegion region)
        {
            List<CartelGoon> ambushSpawned = new();
            // Detect the spawned goons
            // Theoretically its possible that this could detect for example a Spray Graffiti spawned goon
            // OR a Cartel Dealer death spawned defender goon, if the events triggered within this timeframe...
            // very improbable but possible, todo fix in the future how...
            yield return Wait01;
            if (!registered) yield break;
            if (NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.spawnedGoons.Count != currentlySpawned.Count)
            {
                foreach (CartelGoon goon in NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.GoonPool.spawnedGoons)
                {
                    if (!goon.IsGoonSpawned || goon.Health.IsDead || goon.Health.IsKnockedOut)
                    {
                        Log("Found invalid goon not in ambush");
                        continue;
                    }
                    bool hasCombatFlag = false;
                    if (goon.Behaviour.CombatBehaviour.Active && goon.Behaviour.CombatBehaviour.Target != null)
                    {
                        Log("Found active combat goon with target to player: " + goon.name);
                        hasCombatFlag = true;
                    }

                    bool isSabotager = false;
                    if (SabotageEvent.sabotager != null && goon == SabotageEvent.sabotager)
                        isSabotager = true;

                    if (!currentlySpawned.Contains(goon) && 
                        !ambushSpawned.Contains(goon) && 
                        !spawnedGatherGoons.Contains(goon) &&
                        !isSabotager && hasCombatFlag)
                    {
                        ambushSpawned.Add(goon);
                    }
                }
            }

            // if nothing was found ambush didnt run
            if (ambushSpawned.Count == 0)
            {
                Log("Ambush spawned nothing!!");
                yield break;
            }
            Log($"Detected ambush spawned: {ambushSpawned.Count}");
            foreach (CartelGoon goon in ambushSpawned)
                Log($"  {goon.name}");
            // Monitor same state as the original enumerator if there are spawned ambushers
            float maxAmbushElapsed = (float)Ambush.CANCEL_AMBUSH_AFTER_MINS;
            float elapsed = 0f;
            List<CartelGoon> deadGoons = new();
            int spawnedCount = ambushSpawned.Count;

            while (registered && elapsed < maxAmbushElapsed)
            {
                if (ambushSpawned.Count == 0 || deadGoons.Count == spawnedCount) break;

                foreach (CartelGoon goon in ambushSpawned)
                {
                    yield return Wait05;
                    elapsed += 0.5f;
                    if (deadGoons.Contains(goon)) continue;

                    if (goon.Health.IsDead || goon.Health.IsKnockedOut)
                    {
                        deadGoons.Add(goon);
                    }
                }

                foreach (CartelGoon goon in deadGoons)
                {
                    yield return Wait05;
                    elapsed += 0.5f;
                    if (ambushSpawned.Contains(goon))
                        ambushSpawned.Remove(goon);
                }
            }

            yield return Wait01;

            if ((ambushSpawned.Count == 0 || deadGoons.Count == spawnedCount) && InstanceFinder.IsServer)
            {
                Log("Ambush defeated flip influence");
                // flip the original influence and apply the mod one
                float change = -(Ambush.AMBUSH_DEFEATED_INFLUENCE_CHANGE) + influenceConfig.ambushDefeated;
                if (change != 0f)
                    NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Influence.ChangeInfluence(region, change);
            }

            ambushSpawned.Clear();
            deadGoons.Clear();
        }

    }


    // Patch graffiti influence reward function as blocking, so its basically same as in source but bound to config with influence
    [HarmonyPatch(typeof(WorldSpraySurface), "Reward")]
    public static class WorldSpraySurface_Reward_Patch
    {
        public static bool Prefix(WorldSpraySurface __instance)
        {
            NetworkSingleton<LevelManager>.Instance.AddXP(50);

            if (!InstanceFinder.IsServer)
                return false;
            
            // If cartel is hostile OR if cartel is truced and allied extensions are enabled -> change influence after graffiti
            if (NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile || (currentConfig.alliedExtensions && NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced))
            {
                NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Influence.ChangeInfluence(__instance.Region, influenceConfig.graffitiInfluenceReduction);
            }
            return false;
        }
    }

    // Patch OnCustomerUnlocked to allow config applied influence changes
    [HarmonyPatch(typeof(Customer), "OnCustomerUnlocked")]
    public static class Customer_OnCustomerUnlocked_Patch
    {
        public static bool Prefix(Customer __instance, NPCRelationData.EUnlockType unlockType, bool notify)
        {
            //Log("Unlock invoke", "OnCustomerUnlocked");
            // based on source the influence is guarded as follows
            if (!notify || !NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.InstanceExists) return true;

            bool isHostile = NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile;
            bool isTruced = NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced;

            if (isHostile || isTruced)
            {
                if (isTruced && !currentConfig.alliedExtensions)
                {
                    return true;
                }
            }
            else
                return true;
            Log("Unlock pass", "OnCustomerUnlocked");
            // original function is guaranteed to change influence, prefix overrides that change
            if (isHostile)
            {
                // Since customers can be unlocked and locked with the steal back customer feature
                // the influence awarded from re unlocking is half that of config customerUnlockInfluenceChange
                if (currentConfig.stealBackCustomers) // config value to enable steal back is true
                {
                    List<StealBackCustomer.StolenNPC> currentStolen = new(StealBackCustomer.stolenNPCs);
                    StealBackCustomer.StolenNPC stolen = null;
                    for (int i = 0; i < currentStolen.Count; i++)
                    {
                        if (currentStolen[i].npc == __instance.NPC)
                        {
                            stolen = currentStolen[i];
                            break;
                        }
                    }
                    if (stolen != null)
                    {

                        float change = -(Customer.CUSTOMER_UNLOCKED_CARTEL_INFLUENCE_CHANGE) + influenceConfig.customerUnlockInfluenceChange * 0.5f;
                        if (change != 0f && __instance.NPC.Region != EMapRegion.Northtown)
                            NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Influence.ChangeInfluence(__instance.NPC.Region, change);
                    }
                }
                else // Unlocked customer for the first time
                {
                    Log("First time unloock", "OnCustomerUnlocked");
                    // flip the original influence and apply the mod one
                    float change = -(Customer.CUSTOMER_UNLOCKED_CARTEL_INFLUENCE_CHANGE) + influenceConfig.customerUnlockInfluenceChange;
                    if (change != 0f && __instance.NPC.Region != EMapRegion.Northtown)
                        NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Influence.ChangeInfluence(__instance.NPC.Region, change);
                }
            }
            // Original function doesnt change influence so just apply the config
            else if (isTruced)
            {
                Log("First time unloock truce", "OnCustomerUnlocked");
                if (__instance.NPC.Region != EMapRegion.Northtown)
                    NetworkSingleton<Il2CppScheduleOne.Cartel.Cartel>.Instance.Influence.ChangeInfluence(__instance.NPC.Region, influenceConfig.customerUnlockInfluenceChange);
            }



            return true;
        }
    }


}
