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
            while (!VICE.Enforcement.Multiplayer.EnforcementReplication.TemplateReady)
            {
                if (!registered) yield break;
                VICE.Enforcement.Multiplayer.EnforcementReplication.PrepareTemplate();
                yield return Wait01;
            }
            copBaseClone = VICE.Enforcement.Multiplayer.EnforcementReplication.Template;
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
            
            // Do not remove native network behaviours or manually initialize every
            // clone under the same ObjectId. FishNet owns initialization and IDs.
            newNob.ResetState();
            newNob.SceneId = 0;
            newNob.ParentNetworkObject = null;
            newNob.RuntimeParentNetworkObject = null;
            newNob.RuntimeParentTransform = null;
            newNob.IsNested = false;
            newNob.SetIsNetworked(true);
            npc.ApplyNPCData(cloneData);

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
            VICE.Enforcement.Multiplayer.EnforcementReplication.Track(npc);
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
        private static void Postfix(PoliceOfficer __instance)
        {
            CopInitHelper.RegisterCloneGuid(__instance);
            VICE.Enforcement.Multiplayer.EnforcementReplication.RestoreGuestIdentity(__instance);
        }
    }
}
