using VICE.Syndicate.Compatibility;

using UnityEngine.AI;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using Il2Cpp;
using Il2CppScheduleOne.Police;

using static VICE.Syndicate.DebugModule;
using static VICE.Syndicate.SyndicateMod;

using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Framework;
using Behaviour = Il2CppScheduleOne.NPCs.Behaviour.Behaviour;
using Il2CppFishNet.Object;
using Il2CppFishNet.Managing;
using Il2CppScheduleOne.Dialogue;


namespace VICE.Syndicate
{
    public static class NPCInitHelper
    {
        private static readonly Dictionary<IntPtr, Il2CppSystem.Guid> CloneGuids = new();
        // After instantiating from non-prefab objects NPC+NetworkObject has alot of unassigned fields
        // This function populates them and prepares the object so that it works in runtime
        // In mono backend most of the required functions in NetworkObject are locked behind internal keyword so 
        // mono has to do the reflection
        // In Il2Cpp backend most of the functions can be directly called since they are unstripped
        public static IEnumerator InitiateClone(NetworkObject newNob, NetworkManager netManager, NPCData dataPreset = null)
        {
            NPC npc = newNob.GetComponent<NPC>();
            var sourceData = dataPreset ?? npc.NPCData ?? npc._defaultNPCData?.GetRuntimeData();
            if (sourceData is null) throw new InvalidOperationException($"Syndicate clone '{newNob.name}' has no source NPC data.");
            var cloneData = sourceData.GetDeepCopy();
            var guid = GUIDManager.GenerateUniqueGUID();
            CloneGuids.Add(npc.Pointer, guid);
            npc.BakedGUID = guid.ToString();
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
            // Wait for Awake to wire the native components before detaching and
            // applying data; an early assignment makes Health deinit too soon.
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
                Log("Refresh network behaviours");
                Log("Invoke updateNetworkBehMethod");
                byte componentIndex = 0;
                newNob.UpdateNetworkBehaviours(newNob, ref componentIndex);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            npc.ApplyNPCData(cloneData);

            // Invoke pre init to populate the necessary component references
            try
            {
                Log("Run preinit");
                newNob.Preinitialize_Internal(netManager, 150, null, true);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            // Invoke init to clear self + related behs + init npc
            try
            {
                Log("Run init");
                newNob.Initialize(true, true);
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


            // for some reason the interactable object keeps bugging out
            // maybe due to some collider being in wrong laYer or something is unassigned
            // to fix, increase sphere size here
            DialogueController dg = newNob.GetComponentInChildren<DialogueController>();
            Transform sphere = dg._interactable.transform.Find("Sphere");
            CapsuleCollider cc = sphere.GetComponent<CapsuleCollider>();
            cc.height = 1.85f;
            cc.radius = 0.5f;
            npc._NPCData_k__BackingField = cloneData.GetDeepCopy();
            RegisterCloneGuid(npc);
        }

        public static void RegisterCloneGuid(NPC npc)
        {
            if (!CloneGuids.TryGetValue(npc.Pointer, out var guid)) return;
            npc.BakedGUID = guid.ToString();
            npc.SetGUID(guid);
        }
    }

    [HarmonyPatch(typeof(NPC), nameof(NPC.Start))]
    internal static class RuntimeNpcStartPatch
    {
        private static void Postfix(NPC __instance) => NPCInitHelper.RegisterCloneGuid(__instance);
    }

    // The beta compiler inlines base Start into the officer override.
    [HarmonyPatch(typeof(PoliceOfficer), nameof(PoliceOfficer.Start))]
    internal static class RuntimeCopStartPatch
    {
        private static void Postfix(PoliceOfficer __instance) => NPCInitHelper.RegisterCloneGuid(__instance);
    }
}
