using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vision;
using MelonLoader;
using PersonalMods;
using UnityEngine;

[assembly: MelonInfo(typeof(PersonalMods.BetterPolice.BetterPoliceMod), "Personal Better Police", "0.1.0", "Axl47; based on Frostbyt3 / Babyhamsta")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.BetterPolice;

public sealed class BetterPoliceMod : MelonMod
{
    private static MelonPreferences_Entry<bool> Enabled = null!, Flanking = null!, RadioNearby = null!;
    private static MelonPreferences_Entry<float> ChaseMultiplier = null!, FarChaseMultiplier = null!, DistanceMaximum = null!, VisionRange = null!, HorizontalFov = null!, VerticalFov = null!, NoticeTime = null!, ArrestMultiplier = null!, NearAccuracy = null!, FarAccuracy = null!, AimTime = null!, RadioDistance = null!;
    private static MelonPreferences_Entry<int> MaximumBackup = null!;
    private static readonly Dictionary<IntPtr, (NPCSpeedController Controller, float Original)> Speeds = new();
    private static readonly Dictionary<IntPtr, (VisionCone Cone, float Range)> Cones = new();
    private static readonly Dictionary<IntPtr, (VisionCone.StateContainer State, float Time)> Notices = new();
    private static readonly Dictionary<IntPtr, (AvatarRangedWeapon Weapon, float Near, float Far, float AimMin, float AimMax)> Weapons = new();
    private static readonly Dictionary<IntPtr, float> NextFlank = new();
    private static readonly Dictionary<string, float> NextRadio = new();

    public override void OnInitializeMelon()
    {
        var config = MelonPreferences.CreateCategory("PersonalBetterPolice");
        Enabled = ModSupport.Entry(config, "Enabled", true, "Enable tougher police.");
        ChaseMultiplier = ModSupport.Entry(config, "ChaseSpeedMultiplier", 1.3f, "Multiply each officer's original movement speed during a pursuit.");
        FarChaseMultiplier = ModSupport.Entry(config, "DistantChaseSpeedMultiplier", 2f, "Maximum chase multiplier as the player gets farther away.");
        DistanceMaximum = ModSupport.Entry(config, "DistanceForMaximumBoost", 100f, "Distance in meters where the maximum boost applies.");
        VisionRange = ModSupport.Entry(config, "VisionRangeMultiplier", 10f, "Police vision range multiplier, matching the old personal config.");
        HorizontalFov = ModSupport.Entry(config, "HorizontalFOV", 165f, "Police horizontal vision field in degrees.");
        VerticalFov = ModSupport.Entry(config, "VerticalFOV", 80f, "Police vertical vision field in degrees.");
        NoticeTime = ModSupport.Entry(config, "NoticeTimeMultiplier", 0.75f, "Multiply original notice time; lower values detect crime sooner.");
        ArrestMultiplier = ModSupport.Entry(config, "ArrestTickMultiplier", 1.5f, "Speed up the game's arrest progress tick.");
        NearAccuracy = ModSupport.Entry(config, "MinimumCloseRangeHitChance", 0.95f, "Minimum police weapon hit probability at close range.");
        FarAccuracy = ModSupport.Entry(config, "MinimumFarRangeHitChance", 0.8f, "Minimum police weapon hit probability at long range.");
        AimTime = ModSupport.Entry(config, "AimTimeMultiplier", 0.65f, "Multiply original police aiming delay.");
        Flanking = ModSupport.Entry(config, "Flanking", true, "Use reachable lead/intercept destinations during visible chases.");
        RadioNearby = ModSupport.Entry(config, "RadioNearby", true, "Call already active nearby police into a chase, on the host.");
        RadioDistance = ModSupport.Entry(config, "RadioDistance", 100f, "Maximum distance of backup officers from the pursued player.");
        MaximumBackup = ModSupport.Entry(config, "MaximumBackupOfficers", 10, "Maximum nearby officers called per radio request (0 to 20).");
        var self = typeof(BetterPoliceMod);
        ModSupport.Patch(HarmonyInstance, typeof(PoliceOfficer), "Activate", self, postfix: nameof(OfficerReady));
        ModSupport.Patch(HarmonyInstance, typeof(PoliceOfficer), "UpdateVision", self, postfix: nameof(OfficerReady));
        ModSupport.Patch(HarmonyInstance, typeof(VisionCone), "IsPointWithinSight", self, postfix: nameof(WidenSight));
        ModSupport.Patch(HarmonyInstance, typeof(PursuitBehaviour), "Activate", self, postfix: nameof(PursuitTick));
        ModSupport.Patch(HarmonyInstance, typeof(PursuitBehaviour), "OnActiveTick", self, postfix: nameof(PursuitTick));
        ModSupport.Patch(HarmonyInstance, typeof(PursuitBehaviour), "UpdateArrest", self, prefix: nameof(ArrestTick));
        ModSupport.Patch(HarmonyInstance, typeof(PursuitBehaviour), "EndCombat", self, postfix: nameof(PursuitEnded));
        ModSupport.Patch(HarmonyInstance, typeof(PursuitBehaviour), "Disable", self, postfix: nameof(PursuitEnded));
        ModSupport.Patch(HarmonyInstance, typeof(PoliceOfficer), "Deactivate", self, prefix: nameof(OfficerLeaving));
    }

    private static float Bound(float value, float minimum, float maximum, float fallback)
        => float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static void OfficerReady(PoliceOfficer __instance)
    {
        if (!Enabled.Value || !__instance || !__instance.Awareness || !__instance.Awareness.VisionCone) return;
        try
        {
            var cone = __instance.Awareness.VisionCone;
            if (!Cones.ContainsKey(cone.Pointer)) Cones.Add(cone.Pointer, (cone, cone.RangeMultiplier));
            cone.RangeMultiplier = Bound(VisionRange.Value, 0.1f, 20f, 10f);
            // Default states are templates. Changing both templates and their copies would multiply twice.
            if (cone.stateSettings is not null)
                foreach (var states in cone.stateSettings.Values)
                    foreach (var state in states.Values) ShortenNotice(state);
        }
        catch (Exception error) { ModSupport.WarnOnce("police:vision", $"Police vision adjustment failed: {error.Message}"); }
    }

    private static void ShortenNotice(VisionCone.StateContainer state)
    {
        if (state is null) return;
        if (!Notices.TryGetValue(state.Pointer, out var original))
        {
            original = (state, state.NoticeTimeMultiplier);
            Notices.Add(state.Pointer, original);
        }
        state.NoticeTimeMultiplier = Math.Max(0.1f, original.Time * Bound(NoticeTime.Value, 0.1f, 2f, 0.75f));
    }

    private static void WidenSight(VisionCone __instance, Vector3 point, bool ignoreLoS, LandVehicle vehicleToIgnore, ref bool __result)
    {
        if (__result || !Enabled.Value || !__instance || !__instance.npc || __instance.npc.TryCast<PoliceOfficer>() is null || !__instance.VisionOrigin) return;
        try
        {
            var delta = point - __instance.VisionOrigin.position;
            if (delta.sqrMagnitude > __instance.effectiveRange * __instance.effectiveRange) return;
            var local = __instance.VisionOrigin.InverseTransformDirection(delta.normalized);
            var horizontal = Math.Abs(Math.Atan2(local.x, local.z) * Mathf.Rad2Deg);
            var vertical = Math.Abs(Math.Asin(Math.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg);
            if (horizontal > Bound(HorizontalFov.Value, 90f, 180f, 165f) * 0.5f || vertical > Bound(VerticalFov.Value, 60f, 120f, 80f) * 0.5f) return;
            if (!ignoreLoS)
            {
                foreach (var hit in Physics.RaycastAll(__instance.VisionOrigin.position, delta.normalized, delta.magnitude,
                    __instance.VisibilityBlockingLayers, QueryTriggerInteraction.Ignore))
                {
                    if (vehicleToIgnore && hit.collider.GetComponentInParent<LandVehicle>() == vehicleToIgnore) continue;
                    if (hit.collider.GetComponentInParent<NPC>() == __instance.npc) continue;
                    return;
                }
            }
            __result = true;
        }
        catch (Exception error) { ModSupport.WarnOnce("police:sight", $"Additional police field of view unavailable: {error.Message}"); }
    }

    private static void PursuitTick(PursuitBehaviour __instance)
    {
        if (!Enabled.Value || !__instance || !__instance.Npc || __instance.Npc.TryCast<PoliceOfficer>() is null || !__instance.TargetPlayer || !__instance.Npc.Movement) return;
        try
        {
            var movement = __instance.Npc.Movement;
            var speed = movement.SpeedController;
            if (speed is null) return;
            if (!Speeds.TryGetValue(__instance.Npc.Pointer, out var original))
            {
                original = (speed, speed._speedMultiplier);
                Speeds.Add(__instance.Npc.Pointer, original);
            }
            var distance = Vector3.Distance(__instance.Npc.transform.position, __instance.TargetPlayer.transform.position);
            var near = Bound(ChaseMultiplier.Value, 1f, 4f, 1.3f);
            var far = Math.Max(near, Bound(FarChaseMultiplier.Value, 1f, 4f, 2f));
            var factor = Mathf.Lerp(near, far, Mathf.Clamp01(distance / Bound(DistanceMaximum.Value, 10f, 500f, 100f)));
            speed.SetSpeedMultiplier(original.Original * factor);
            TuneWeapon(__instance.currentWeapon?.TryCast<AvatarRangedWeapon>());
            if (__instance.IsServerInitialized)
            {
                Intercept(__instance, distance);
                RequestBackup(__instance);
            }
        }
        catch (Exception error) { ModSupport.WarnOnce("police:pursuit", $"Police pursuit adjustment failed: {error.Message}"); }
    }

    private static void TuneWeapon(AvatarRangedWeapon? weapon)
    {
        if (weapon is null || !weapon) return;
        if (!Weapons.TryGetValue(weapon.Pointer, out var original))
        {
            original = (weapon, weapon.HitChance_MinRange, weapon.HitChance_MaxRange, weapon.AimTime_Min, weapon.AimTime_Max);
            Weapons.Add(weapon.Pointer, original);
        }
        weapon.HitChance_MinRange = Math.Max(original.Near, Bound(NearAccuracy.Value, 0f, 1f, 0.95f));
        weapon.HitChance_MaxRange = Math.Max(original.Far, Bound(FarAccuracy.Value, 0f, 1f, 0.8f));
        var aim = Bound(AimTime.Value, 0.05f, 2f, 0.65f);
        weapon.AimTime_Min = Math.Max(0.05f, original.AimMin * aim);
        weapon.AimTime_Max = Math.Max(weapon.AimTime_Min, original.AimMax * aim);
    }

    private static void ArrestTick(PursuitBehaviour __instance, ref float tick)
    {
        if (Enabled.Value && __instance.Npc && __instance.Npc.TryCast<PoliceOfficer>() is not null) tick *= Bound(ArrestMultiplier.Value, 1f, 5f, 1.5f);
    }

    private static void Intercept(PursuitBehaviour pursuit, float distance)
    {
        if (!Flanking.Value || distance < 12f || !pursuit.IsTargetImmediatelyVisible || pursuit.Npc.Movement.IsOnLadder) return;
        if (NextFlank.TryGetValue(pursuit.Pointer, out var next) && Time.time < next) return;
        NextFlank[pursuit.Pointer] = Time.time + 3f;
        var velocity = pursuit.TargetPlayer.Velocity;
        velocity.y = 0;
        if (velocity.sqrMagnitude < 1f) return;
        var side = (pursuit.Npc.Pointer.ToInt64() & 16) == 0 ? -1f : 1f;
        var target = pursuit.TargetPlayer.transform.position + Vector3.ClampMagnitude(velocity * 1.5f, 12f)
            + Vector3.Cross(Vector3.up, velocity.normalized) * side * 2f;
        var movement = pursuit.Npc.Movement;
        if (movement.CanGetTo(target, 1.5f)) movement.SetDestination(target);
    }

    private static void RequestBackup(PursuitBehaviour pursuit)
    {
        if (!RadioNearby.Value || !pursuit.TargetPlayer.CrimeData || (int)pursuit.TargetPlayer.CrimeData.CurrentPursuitLevel == 0) return;
        var code = pursuit.TargetPlayer.PlayerCode;
        if (NextRadio.TryGetValue(code, out var next) && Time.time < next) return;
        NextRadio[code] = Time.time + 30f; // Set before network calls; their Activate hooks can call us again.
        var candidates = new List<PoliceOfficer>();
        foreach (var officer in PoliceOfficer.Officers)
        {
            if (!officer || !officer.IsActive || officer.IgnorePlayers || !officer.PursuitBehaviour || officer.PursuitBehaviour.Active) continue;
            if (Vector3.Distance(officer.transform.position, pursuit.TargetPlayer.transform.position) <= Bound(RadioDistance.Value, 10f, 500f, 100f)) candidates.Add(officer);
        }
        candidates.Sort((a, b) => Vector3.SqrMagnitude(a.transform.position - pursuit.TargetPlayer.transform.position)
            .CompareTo(Vector3.SqrMagnitude(b.transform.position - pursuit.TargetPlayer.transform.position)));
        foreach (var officer in candidates.Take(Math.Clamp(MaximumBackup.Value, 0, 20))) officer.BeginFootPursuit_Networked(code, false);
    }

    private static void PursuitEnded(PursuitBehaviour __instance)
    {
        if (!__instance || !__instance.Npc) return;
        RestoreSpeed(__instance.Npc.Pointer);
        NextFlank.Remove(__instance.Pointer);
    }

    private static void OfficerLeaving(PoliceOfficer __instance)
    {
        if (__instance) RestoreSpeed(__instance.Pointer);
    }

    private static void RestoreSpeed(IntPtr id)
    {
        if (Speeds.Remove(id, out var saved)) saved.Controller.SetSpeedMultiplier(saved.Original);
    }

    private static void RestoreAll()
    {
        foreach (var saved in Speeds.Values) { try { saved.Controller.SetSpeedMultiplier(saved.Original); } catch { } }
        foreach (var saved in Cones.Values)
        {
            if (!saved.Cone) continue;
            saved.Cone.RangeMultiplier = saved.Range;
        }
        foreach (var saved in Notices.Values) { try { saved.State.NoticeTimeMultiplier = saved.Time; } catch { } }
        foreach (var saved in Weapons.Values)
        {
            if (!saved.Weapon) continue;
            saved.Weapon.HitChance_MinRange = saved.Near; saved.Weapon.HitChance_MaxRange = saved.Far;
            saved.Weapon.AimTime_Min = saved.AimMin; saved.Weapon.AimTime_Max = saved.AimMax;
        }
        Speeds.Clear(); Cones.Clear(); Notices.Clear(); Weapons.Clear(); NextFlank.Clear(); NextRadio.Clear();
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) { if (sceneName == "Main") RestoreAll(); }
    public override void OnApplicationQuit() => RestoreAll();
}
