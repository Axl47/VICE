using Il2CppScheduleOne.NPCs;
using UnityEngine;
using UnityEngine.AI;
namespace VICE.Syndicate.Compatibility;
internal static class BetaNpc
{
    internal static void SendTextMessage(this NPC npc, string text) => npc.MSGConversation?.SendMessage(new Il2CppScheduleOne.Messaging.Message(text, Il2CppScheduleOne.Messaging.Message.ESenderType.Other, true), true, true);
    internal const string PauseId = "vice:syndicate:pause";
    internal static void PauseMovement(this NPCMovement movement) => movement.SpeedController.AddSpeedControl(PauseId, 0f, 1000, SpeedControl.EType.Normalized);
    internal static void ResumeMovement(this NPCMovement movement) => movement.SpeedController.RemoveSpeedControl(PauseId);
    internal static void WarpToNearestValidLocation(this NPCMovement movement)
    {
        if (NavMesh.SamplePosition(movement.transform.position, out var hit, 10f, movement.NavMeshAreaMask)) movement.Warp(hit.position);
    }
    internal static void SetCurrentHealth(this NPCHealth health, float value)
    {
        // Configure max health through the beta controller, then replicate current health.
        var data = health._npc.NPCData.Health;
        data.MaxHealth = Mathf.Max(data.MaxHealth, value);
        health.SetHealthData(data);
        health.sync___set_value__currentHealth(Mathf.Clamp(value, 0f, health.MaxHealth), health.IsServerInitialized);
    }
}
