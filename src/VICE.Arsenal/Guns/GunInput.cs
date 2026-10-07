using Il2CppScheduleOne;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using UnityEngine;

namespace VICE.Arsenal.Guns;

internal static class GunInput
{
    private sealed class FireState
    {
        internal readonly SpinUpState Spin = new();
        internal Animation? Animation;
        internal AudioSourceController? WindupSound, ShutdownSound;
        internal bool Spinning;
    }
    private static readonly Dictionary<IntPtr, FireState> States = new();
    [ThreadStatic] private static CustomGun? FiringGun;

    internal static void Initialize(HarmonyLib.Harmony harmony)
    {
        var self = typeof(GunInput);
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "UpdateInput", self, postfix: nameof(AfterInput));
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "Fire", self, prefix: nameof(BeforeFire), finalizer: nameof(FireFinished));
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "Equip", self, postfix: nameof(Equipped));
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "Unequip", self, prefix: nameof(Unequipped));
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "Reload", self, prefix: nameof(Reloading));
        ModSupport.Patch(harmony, typeof(PlayerCamera), "JoltCamera", self, prefix: nameof(CameraJolt));
        ModSupport.Patch(harmony, typeof(PlayerCamera), "StartCameraShake", self, prefix: nameof(CameraShake));
    }

    internal static void Reset()
    {
        foreach (var state in States.Values) StopSpinning(state, shutdown: false);
        States.Clear();
        FiringGun = null;
    }

    private static void Equipped(Equippable_RangedWeapon __instance)
    {
        var gun = Identify(__instance);
        if (gun is null) return;
        try { GunAudio.Configure(__instance.gameObject, gun.Options, avatar: false); }
        catch (Exception error) { ModSupport.WarnOnce($"guns:audio:equip:{gun.Options.Id}", $"Weapon sound setup failed: {error.Message}"); }
    }

    private static void Unequipped(Equippable_RangedWeapon __instance)
    {
        if (States.Remove(__instance.Pointer, out var state)) StopSpinning(state, shutdown: false);
    }
    private static void Reloading(Equippable_RangedWeapon __instance)
    {
        if (States.TryGetValue(__instance.Pointer, out var state)) StopSpinning(state, shutdown: false);
    }

    private static CustomGun? Identify(Equippable_RangedWeapon weapon)
    {
        var id = weapon.itemInstance?.Definition?.ID;
        return id is not null && WeaponAssets.Guns.TryGetValue(id, out var gun) ? gun : null;
    }

    private static FireState State(Equippable_RangedWeapon weapon)
    {
        if (!States.TryGetValue(weapon.Pointer, out var state))
        {
            States[weapon.Pointer] = state = new FireState();
            if (weapon.itemInstance?.Definition?.ID == "minigun")
            {
                state.Animation = weapon.GetComponentsInChildren<Animation>(true).FirstOrDefault(x => x.GetClip("MiniGun Windup"));
                foreach (var sound in weapon.GetComponentsInChildren<AudioSourceController>(true))
                {
                    if (sound.gameObject.name == "Windup Sound") state.WindupSound = sound;
                    if (sound.gameObject.name == "Shutdown Sound") state.ShutdownSound = sound;
                }
            }
        }
        return state;
    }

    private static void StopSpinning(FireState state, bool shutdown)
    {
        state.Spin.Reset();
        try
        {
            if (state.Animation is { } animation && animation) animation.Stop("MiniGun Windup");
            if (state.WindupSound is { } windupSound && windupSound) windupSound.Stop();
            if (shutdown && state.Spinning && state.ShutdownSound is { } shutdownSound && shutdownSound) shutdownSound.Play();
        }
        catch (Exception error) { ModSupport.WarnOnce("guns:windup:stop", $"Weapon spin-down unavailable: {error.Message}"); }
        state.Spinning = false;
    }

    private static void AnimateSpin(Equippable_RangedWeapon weapon, FireState state, bool firing)
    {
        if (weapon.itemInstance?.Definition?.ID != "minigun") return;
        if (!state.Spinning)
        {
            if (state.WindupSound is { } windupSound && windupSound) windupSound.Play();
            state.Spinning = true;
        }
        if (state.Animation is not { } animation || !animation) { ModSupport.WarnOnce("guns:windup:clip", "MiniGun Windup animation missing on the equipped model."); return; }
        var clip = animation["MiniGun Windup"];
        if (clip is null) return;
        clip.wrapMode = WrapMode.Loop;
        clip.speed = 0.2f + 0.8f * state.Spin.Progress;
        // The fire event plays its own barrel/muzzle animation. Resume the spin clip
        // between bursts without restarting it every input frame or interrupting reload.
        if (!firing && !animation.IsPlaying("MiniGun Windup") && !animation.IsPlaying("MiniGun Fire"))
            animation.Play("MiniGun Windup");
    }

    private static void AfterInput(Equippable_RangedWeapon __instance)
    {
        var gun = Identify(__instance);
        if (gun is null) return;
        try
        {
            var state = State(__instance);
            var inventory = PlayerInventory.Instance;
            if (!GunsFeature.Enabled.Value || Time.timeScale == 0 || (PauseMenu.Instance && PauseMenu.Instance.IsPaused)
                || !inventory || !inventory.EquippingEnabled || inventory.AttachedScreen
                || inventory.Equippable?.Pointer != __instance.Pointer || __instance.IsReloading || __instance.IsCocking)
            { StopSpinning(state, shutdown: false); return; }
            var windup = GunOptions.Safe(gun.Options.WindupSeconds.Value, 0, 0, 10);
            var aiming = GameInput.GetButton(GameInput.ButtonCode.SecondaryClick) && __instance.CanAim();
            state.Spin.Step(aiming, blocked: false, Time.deltaTime, windup);
            var firing = GameInput.GetButton(GameInput.ButtonCode.PrimaryClick) && __instance.CanFire(true);
            if (aiming) AnimateSpin(__instance, state, firing && state.Spin.Ready);
            else StopSpinning(state, shutdown: true);
            if (windup > 0 && !state.Spin.Ready) return;
            if (firing && __instance.TimeSinceFire >= __instance.FireCooldown)
                __instance.Fire(); // Native damage, ammo consumption, animation, sound and network messages.
        }
        catch (Exception error) { ModSupport.WarnOnce($"guns:input:{gun.Options.Id}", $"Automatic firing unavailable: {error.Message}"); }
    }

    private static bool BeforeFire(Equippable_RangedWeapon __instance, out CustomGun? __state)
    {
        __state = FiringGun;
        if (!GunsFeature.Enabled.Value) return true;
        var gun = Identify(__instance);
        if (gun is null) return true;
        var windup = GunOptions.Safe(gun.Options.WindupSeconds.Value, 0, 0, 10);
        if (windup > 0 && (!GameInput.GetButton(GameInput.ButtonCode.SecondaryClick) || !State(__instance).Spin.Ready)) return false;
        FiringGun = gun;
        return true;
    }

    private static Exception? FireFinished(Exception? __exception, CustomGun? __state)
    {
        FiringGun = __state; // Also restore after native exceptions or another mod canceling the shot.
        return __exception;
    }

    private static bool CameraJolt(PlayerCamera __instance)
    {
        if (FiringGun is null) return true;
        var strength = GunOptions.Safe(FiringGun.Options.CameraShakeMultiplier.Value, 0.5f, 0, 1);
        if (strength >= 1) return true;
        if (strength > 0) __instance.StartCameraShake(0.04f, 0.08f, true);
        return false;
    }

    private static void CameraShake(ref float intensity)
    {
        if (FiringGun is not null) intensity *= GunOptions.Safe(FiringGun.Options.CameraShakeMultiplier.Value, 0.5f, 0, 1);
    }
}
