using Il2CppScheduleOne.NPCs;

namespace VICE.Enforcement.Compatibility;

internal static class Movement
{
    internal const string PauseId = "vice:enforcement:pause";
    internal static void PauseMovement(this NPCMovement movement)
        => movement.SpeedController.AddSpeedControl(PauseId, 0f, 1000, SpeedControl.EType.Normalized);
    internal static void ResumeMovement(this NPCMovement movement)
        => movement.SpeedController.RemoveSpeedControl(PauseId);
}
