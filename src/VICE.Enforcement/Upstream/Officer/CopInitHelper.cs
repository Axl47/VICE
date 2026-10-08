using MelonLoader;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using HarmonyLib;

using static NACops.NACops;
using static NACops.DebugModule;
using static NACops.AvatarUtility;

#if MONO
using System.Reflection;
using ScheduleOne.AvatarFramework;
using ScheduleOne.NPCs;
using ScheduleOne.Police;
using ScheduleOne.Dialogue;
using ScheduleOne.Map;
using ScheduleOne.NPCs.Framework;
using Behaviour = ScheduleOne.NPCs.Behaviour.Behaviour;
using FishNet.Managing;
using FishNet.Object;
#else
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs.Framework;
using Behaviour = Il2CppScheduleOne.NPCs.Behaviour.Behaviour;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Object;
using Il2Cpp;
#endif

namespace NACops
{

    public static class CopInitHelper
    {
        private static readonly Dictionary<IntPtr, Il2CppSystem.Guid> CloneGuids = new();
#if MONO
        public static List<PoliceOfficer> generatedOfficerPool = new();
#else
        public static Il2CppSystem.Collections.Generic.List<PoliceOfficer> generatedOfficerPool = new();
#endif
        public static GameObject copBaseClone;
        public static PoliceOfficer investigator;
        public static int investigatorID;
        public static PoliceOfficer buyBustCop;
        public static int buyBustCopID;
        public static IEnumerator ReplicateCopNPC()
        {
            Log("Replicating COP NPC");
            // Other VICE modules keep a live template. Cloning/reactivating one
            // of those templates would restart its native identity lifecycle.
            PoliceOfficer officer = PoliceOfficer.Officers.ToArray().FirstOrDefault(o => o && o.NPCData is not null
                && o.NPCData.BasicInfo.ID.StartsWith("officer", StringComparison.OrdinalIgnoreCase)
                && o.NPCData.BasicInfo.ID != "officerPrefab");

            if (officer == null)
            {
                Log("No officer found");
                yield break;
            }
            var copyAppearance = officer.Avatar.Appearance.AppliedNakedAppearance?.Clone();
            var copyOutfit = officer.NPCData.Appearance.DefaultOutfit;
            var sourceData = officer.NPCData.GetDeepCopy();
            var sourceGuid = officer.GUID;
            GameObject obj = officer.gameObject;
            obj.SetActive(false);

            GameObject clone = UnityEngine.Object.Instantiate(obj);
            copBaseClone = clone;

            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;

            NPC npc = clone.GetComponent<NPC>();
            NetworkObject newNob = clone.GetComponent<NetworkObject>();
            PoliceOfficer offc = clone.GetComponent<PoliceOfficer>();
            offc.AutoDeactivate = false; // Prevent from returning to station and from being added to officer pool

            clone.name = $"RuntimeOfficer";
            yield return MelonCoroutines.Start(InitiateClone(newNob, networkManager, sourceData));

            npc.NPCData.BasicInfo.ID = "officerPrefab";

            if (!NPCManager.NPCRegistry.Contains(npc))
                NPCManager.NPCRegistry.Add(npc);

            if (copyAppearance is not null) npc.Avatar.Appearance.ApplyNakedAppearance(copyAppearance);
            if (copyOutfit) npc.Avatar.Appearance.ApplyOutfit(copyOutfit);
            offc.PursuitBehaviour.arrestingEnabled = false;

            try
            {
                networkManager.ServerManager.Spawn(newNob);
            }
            catch (Exception ex)
            {
                Log($"Failed to spawn officer {ex}");
            }

            offc.Behaviour.ScheduleManager.DisableSchedule();
            offc.Movement.PauseMovement();
            if (PoliceOfficer.Officers.Contains(offc))
                PoliceOfficer.Officers.Remove(offc);
            
            // for some reason the interactable object keeps bugging out
            // maybe due to some collider being in wrong laYer or something is unassigned
            // to fix, increase sphere size here
            DialogueController_Police dg = offc.GetComponentInChildren<DialogueController_Police>();
            Transform sphere = dg._interactable.transform.Find("Sphere");
            CapsuleCollider cc = sphere.GetComponent<CapsuleCollider>();
            cc.height = 1.85f;
            cc.radius = 0.5f;

            offc.Awareness.SetAwarenessActive(false);

            obj.SetActive(true);
            // Reactivating the source can restore its GUID on the clone. Finalize
            // both registrations after spawning and restoring the source.
            officer.SetGUID(sourceGuid);
            RegisterCloneGuid(npc);
            Log("Finished replicating COP NPC");
            yield break;
        }

        public static IEnumerator InitiateClone(NetworkObject newNob, NetworkManager netManager, NPCData dataPreset = null)
        {
            NPC npc = newNob.GetComponent<NPC>();
            // A Unity clone can retain its source NPCData reference and baked GUID.
            // Give it independent data before changing names, inventory or appearance.
            // Awake clears the runtime backing field on a Unity clone. Use the
            // source data or the native prefab's data, never that empty field.
            var sourceData = dataPreset ?? npc.NPCData ?? npc._defaultNPCData?.GetRuntimeData();
            if (sourceData is null) throw new InvalidOperationException($"Officer clone '{newNob.name}' has no source NPC data.");
            var cloneData = sourceData.GetDeepCopy();
            var cloneGuid = GUIDManager.GenerateUniqueGUID();
            CloneGuids.Add(npc.Pointer, cloneGuid);
            npc.BakedGUID = cloneGuid.ToString();
            // Leave the runtime backing field empty until Awake below. Filling
            // it early makes InitializeNPC deinitialize uninitialized Health.
            SaveSubscriptions.Add(() =>
            {
                CloneGuids.Remove(npc.Pointer);
                if (!npc) return;
                NPCManager.NPCRegistry.Remove(npc);
                var officer = npc.TryCast<PoliceOfficer>();
                if (officer is not null) PoliceOfficer.Officers.Remove(officer);
                if (GUIDManager.IsGUIDAlreadyRegistered(npc.GUID) && GUIDManager.GetObject<NPC>(npc.GUID)?.Pointer == npc.Pointer)
                    GUIDManager.DeregisterObject(npc.Cast<Il2CppScheduleOne.IGUIDRegisterable>());
                UnityEngine.Object.Destroy(npc.gameObject);
            });

            // Populate unassigned fields
            newNob.transform.Find("Avatar").gameObject.SetActive(true);
            newNob.transform.Find("Avatar/BodyContainer").gameObject.SetActive(true);
            newNob.GetComponent<NavMeshAgent>().enabled = true;
            newNob.gameObject.SetActive(true);
            yield return Wait01;
            yield return frameEnd;

            // Awake has now wired the native components. Detach the data before
            // applying clone-specific appearance, identity or inventory changes.
            npc._NPCData_k__BackingField = cloneData;

            newNob.transform.Find("Avatar").gameObject.SetActive(false);
            newNob.transform.Find("Avatar/BodyContainer").gameObject.SetActive(false);
            newNob.GetComponent<NavMeshAgent>().enabled = false;
            if (newNob.gameObject.activeSelf)
                newNob.gameObject.SetActive(false);
            
            // Remove CustomerAttendDealBehaviour since calling disable on it gives nullreference exceptions if in behaviour stack
            Behaviour temp = npc.Behaviour.GetBehaviour("Customer attend deal");
            if (temp)
            {
                UnityEngine.Object.Destroy(temp.gameObject);
                // Refresh the stack
                npc.Behaviour.OnValidate();
            }

            try
            {
#if MONO
                MethodInfo updateNetworkBehMethod = AccessTools.Method(typeof(NetworkObject), "UpdateNetworkBehaviours", new[] {
                    typeof(NetworkObject),
                    typeof(byte).MakeByRefType(), // ref byte componentIndex 
                });

                if (updateNetworkBehMethod == null)
                    Log("updateNetworkBehMethod not found.");
                else
                    updateNetworkBehMethod.Invoke(newNob, new object[] { newNob, (byte)0 });
#else
                byte componentIndex = 0;
                newNob.UpdateNetworkBehaviours(newNob, ref componentIndex);
#endif
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            npc.ApplyNPCData(cloneData);

            // Invoke pre init to populate the necessary component references
            try
            {
#if MONO
                MethodInfo preInitializeMethod = AccessTools.Method(typeof(NetworkObject), "Preinitialize_Internal", new[] {
                    typeof(FishNet.Managing.NetworkManager),
                    typeof(int),
                    typeof(FishNet.Connection.NetworkConnection),
                    typeof(bool)
                });

                if (preInitializeMethod == null)
                    Log("Method Preinitialize_Internal not found.");
                else
                    preInitializeMethod.Invoke(newNob, new object[] { netManager, 150, null, true });
#else
                newNob.Preinitialize_Internal(netManager, 150, null, true);
#endif
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            // Invoke init to clear self + related behs + init npc
            try
            {
#if MONO
                MethodInfo initializeMethod = AccessTools.Method(typeof(NetworkObject), "Initialize", new[] {
                    typeof(bool),
                    typeof(bool)
                });

                if (initializeMethod == null)
                    Log("NetworkObject.Initialize internal method not found.");
                else
                    initializeMethod.Invoke(newNob, new object[] { true, true });
#else
                newNob.Initialize(true, true);
#endif
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            try
            {
                newNob.SetIsNetworked(false);
            }
            catch (Exception ex)
            {
                Log($"Failed to set network object networking to false: {ex}");
            }

            // Initialization may reload the prefab's NPC data. Detach it again
            // before callers assign the clone's identity and inventory settings.
            npc._NPCData_k__BackingField = cloneData.GetDeepCopy();
            npc.Awareness.SetAwarenessActive(false);
        }

        public static void RegisterCloneGuid(NPC npc)
        {
            if (!CloneGuids.TryGetValue(npc.Pointer, out var guid)) return;
            // Native Start writes the inherited GUID directly. Restore our owned
            // identity after Start, and after FishNet finishes spawning.
            npc.BakedGUID = guid.ToString();
            npc.SetGUID(guid);
        }
        
        // Extend the base amount of officers available for daily routines
        public static IEnumerator SpawnOfficersRuntime()
        {
            PoliceStation station = PoliceStation.PoliceStations[0];
            for (int i = 0; i < PoliceOfficer.Officers.Count; i++)
                if (PoliceOfficer.Officers[i].NPCData?.BasicInfo.ID != "officerPrefab")
                    generatedOfficerPool.Add(PoliceOfficer.Officers[i]);

            for (int i = 0; i < officerConfig.ModAddedOfficersCount; i++)
            {
                Log($"Spawning {i}");
                GameObject copNet = UnityEngine.Object.Instantiate<GameObject>(copBaseClone);
                NetworkObject newNob = copNet.GetComponent<NetworkObject>();
                PoliceOfficer offc = copNet.gameObject.GetComponent<PoliceOfficer>();
                offc.AutoDeactivate = false; 

                copNet.name = $"NACop_{i}";
                yield return MelonCoroutines.Start(InitiateClone(newNob, networkManager));

                NPC myNpc = copNet.gameObject.GetComponent<NPC>();
                myNpc.NPCData.BasicInfo.ID = $"NACop_{i}";
                myNpc.NPCData.BasicInfo.FirstName = "Officer";
                myNpc.NPCData.BasicInfo.LastName = "";
                myNpc.NPCData.Inventory.CanBePickpocketed = true;
                myNpc.NPCData.WeatherBehaviour.UseUmbrellaChance = 0f;
                myNpc.Actions._canUseUmbrella = false;

                if (!NPCManager.NPCRegistry.Contains(myNpc))
                    NPCManager.NPCRegistry.Add(myNpc);

                networkManager.ServerManager.Spawn(copNet);
                RegisterCloneGuid(myNpc);
                copNet.gameObject.SetActive(true);

                DialogueController controller = offc.DialogueHandler.GetComponent<DialogueController>();
                controller.Choices.Clear();

                copNet.transform.Find("Avatar").gameObject.SetActive(true);

                AvatarSettings createdSettings = null;
                SetRandomAvatar(offc, ref createdSettings);
                SetVOEmitter(offc);
                yield return GenerateImpostor(offc, createdSettings);

                copNet.GetComponent<NavMeshAgent>().enabled = true;
                offc.AutoDeactivate = true;
                offc.Awareness.SetAwarenessActive(true);
                offc.Movement.Warp(station.SpawnPoint);
                station.NPCEnteredBuilding(offc, station.Doors[0]);
                generatedOfficerPool.Add(offc);

            }
            Log("Extended cops array");
            yield break;
        }

        // Create a reserved cop npc for investigator
        public static IEnumerator CreateInvestigator()
        {
            GameObject copNet = UnityEngine.Object.Instantiate<GameObject>(copBaseClone);
            NetworkObject newNob = copNet.GetComponent<NetworkObject>();
            PoliceOfficer offc = copNet.gameObject.GetComponent<PoliceOfficer>();
            offc.AutoDeactivate = false;
            offc.ChatterEnabled = false;
            offc.BodySearchChance = 0f;

            copNet.name = $"NACop_Investigator";
            yield return MelonCoroutines.Start(InitiateClone(newNob, networkManager));

            NPC myNpc = copNet.gameObject.GetComponent<NPC>();
            myNpc.NPCData.BasicInfo.ID = $"NACop_Investigator";
            myNpc.NPCData.BasicInfo.FirstName = "Investigator";
            myNpc.NPCData.BasicInfo.LastName = "";
            myNpc.NPCData.Inventory.CanBePickpocketed = false;
            myNpc.NPCData.WeatherBehaviour.UseUmbrellaChance = 0f;
            myNpc.Actions._canUseUmbrella = false;

            //myNpc.transform.parent = NPCManager.Instance.NPCContainer;

            if (!NPCManager.NPCRegistry.Contains(myNpc))
                NPCManager.NPCRegistry.Add(myNpc);

            networkManager.ServerManager.Spawn(copNet);
            RegisterCloneGuid(myNpc);

            DialogueController controller = offc.DialogueHandler.GetComponent<DialogueController>();
            controller.Choices.Clear();

            // stay inactive until investigation starts
            // copNet.gameObject.SetActive(true);
            // copNet.transform.Find("Avatar").gameObject.SetActive(true);
            // copNet.GetComponent<NavMeshAgent>().enabled = true;
            investigator = offc;
            investigatorID = investigator.transform.root.gameObject.GetInstanceID();
            investigator.Behaviour.ScheduleManager.DisableSchedule();
            if (PoliceOfficer.Officers.Contains(investigator))
                PoliceOfficer.Officers.Remove(investigator);

            offc.Awareness.SetAwarenessActive(false);

            Log("Done spawning Investigator");
            yield break;
        }

        // Create a reserved cop npc for buy bust
        public static IEnumerator CreateBuyBustCop()
        {
            GameObject copNet = UnityEngine.Object.Instantiate<GameObject>(copBaseClone);
            NetworkObject newNob = copNet.GetComponent<NetworkObject>();
            PoliceOfficer offc = copNet.gameObject.GetComponent<PoliceOfficer>();
            offc.AutoDeactivate = false;

            copNet.name = $"NACop_BuyBust";
            yield return MelonCoroutines.Start(InitiateClone(newNob, networkManager));

            NPC myNpc = copNet.gameObject.GetComponent<NPC>();
            myNpc.NPCData.BasicInfo.ID = $"NACop_BuyBust";
            myNpc.NPCData.BasicInfo.FirstName = "Officer";
            myNpc.NPCData.BasicInfo.LastName = "";
            myNpc.NPCData.Inventory.CanBePickpocketed = false;
            myNpc.NPCData.WeatherBehaviour.UseUmbrellaChance = 0f;
            myNpc.Actions._canUseUmbrella = false;

            //myNpc.transform.parent = NPCManager.Instance.NPCContainer;

            if (!NPCManager.NPCRegistry.Contains(myNpc))
                NPCManager.NPCRegistry.Add(myNpc);

            networkManager.ServerManager.Spawn(copNet);
            RegisterCloneGuid(myNpc);

            DialogueController controller = offc.DialogueHandler.GetComponent<DialogueController>();
            controller.Choices.Clear();

            // stay inactive until buy bust starts
            copNet.gameObject.SetActive(true);
            copNet.transform.Find("Avatar").gameObject.SetActive(true);

            AvatarSettings createdSettings = null;
            SetRandomAvatar(offc, ref createdSettings);
            SetVOEmitter(offc);
            yield return GenerateImpostor(offc, createdSettings);

            copNet.gameObject.SetActive(false);
            copNet.transform.Find("Avatar").gameObject.SetActive(false);

            buyBustCop = offc;
            buyBustCopID = buyBustCop.transform.root.gameObject.GetInstanceID();
            buyBustCop.Behaviour.ScheduleManager.DisableSchedule();
            if (PoliceOfficer.Officers.Contains(offc))
                PoliceOfficer.Officers.Remove(offc);

            offc.Awareness.SetAwarenessActive(false);

            Log("Done spawning buy bust officer");
            yield break;
        }
    }

    [HarmonyPatch(typeof(PoliceOfficer), nameof(PoliceOfficer.Start))]
    internal static class RuntimeOfficerStartPatch
    {
        private static void Postfix(PoliceOfficer __instance) => CopInitHelper.RegisterCloneGuid(__instance);
    }
}
