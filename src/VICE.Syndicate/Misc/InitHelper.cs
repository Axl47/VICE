using VICE.Syndicate.Compatibility;

using UnityEngine.AI;
using System.Collections;
using UnityEngine;

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
        // After instantiating from non-prefab objects NPC+NetworkObject has alot of unassigned fields
        // This function populates them and prepares the object so that it works in runtime
        // In mono backend most of the required functions in NetworkObject are locked behind internal keyword so 
        // mono has to do the reflection
        // In Il2Cpp backend most of the functions can be directly called since they are unstripped
        public static IEnumerator InitiateClone(NetworkObject newNob, NetworkManager netManager, NPCData dataPreset = null)
        {
            NPC npc = newNob.GetComponent<NPC>();

            // Populate unassigned fields
            newNob.transform.Find("Avatar").gameObject.SetActive(true);
            newNob.transform.Find("Avatar/BodyContainer").gameObject.SetActive(true);
            newNob.GetComponent<NavMeshAgent>().enabled = true;
            newNob.gameObject.SetActive(true);
            yield return Wait01;
            yield return frameEnd;

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

            if (dataPreset != null)
            {
                npc.ApplyNPCData(dataPreset);
            }

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
        }
    }
}