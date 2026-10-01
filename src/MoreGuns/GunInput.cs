using Il2CppScheduleOne;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using UnityEngine;

namespace PersonalMods.MoreGuns;

internal static class GunInput
{
    private sealed class FireState { internal float Windup; }
    private static readonly Dictionary<IntPtr, FireState> States = new();

    internal static void Initialize(HarmonyLib.Harmony harmony)
    {
        var self = typeof(GunInput);
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "UpdateInput", self, postfix: nameof(AfterInput));
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "Fire", self, prefix: nameof(BeforeFire));
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "Unequip", self, prefix: nameof(Unequipped));
        ModSupport.Patch(harmony, typeof(Equippable_RangedWeapon), "Reload", self, prefix: nameof(Reloading));
    }

    internal static void Reset() => States.Clear();
    private static void Unequipped(Equippable_RangedWeapon __instance) => States.Remove(__instance.Pointer);
    private static void Reloading(Equippable_RangedWeapon __instance)
    {
        if (States.TryGetValue(__instance.Pointer, out var state)) state.Windup = 0;
    }

    private static CustomGun? Identify(Equippable_RangedWeapon weapon)
    {
        var id = weapon.itemInstance?.Definition?.ID;
        return id is not null && WeaponAssets.Guns.TryGetValue(id, out var gun) ? gun : null;
    }

    private static FireState State(Equippable_RangedWeapon weapon)
    {
        if (!States.TryGetValue(weapon.Pointer, out var state)) States[weapon.Pointer] = state = new FireState();
        return state;
    }

    private static void AfterInput(Equippable_RangedWeapon __instance)
    {
        if (!MoreGunsMod.Enabled.Value) return;
        var gun = Identify(__instance);
        if (gun is null) return;
        try
        {
            var state = State(__instance);
            var inventory = PlayerInventory.Instance;
            if (Time.timeScale == 0 || (PauseMenu.Instance && PauseMenu.Instance.IsPaused)
                || !inventory || !inventory.EquippingEnabled || inventory.AttachedScreen
                || inventory.Equippable?.Pointer != __instance.Pointer) { state.Windup = 0; return; }
            var windup = GunOptions.Safe(gun.Options.WindupSeconds.Value, 0, 0, 10);
            state.Windup = GameInput.GetButton(GameInput.ButtonCode.SecondaryClick) && !__instance.IsReloading
                ? Math.Min(state.Windup + Time.deltaTime, windup) : 0;
            if (windup > 0 && state.Windup < windup) return;
            if (GameInput.GetButton(GameInput.ButtonCode.PrimaryClick) && __instance.TimeSinceFire >= __instance.FireCooldown && __instance.CanFire(true))
                __instance.Fire(); // Native damage, ammo consumption, animation, sound and network messages.
        }
        catch (Exception error) { ModSupport.WarnOnce($"guns:input:{gun.Options.Id}", $"Automatic firing unavailable: {error.Message}"); }
    }

    private static bool BeforeFire(Equippable_RangedWeapon __instance)
    {
        if (!MoreGunsMod.Enabled.Value) return true;
        var gun = Identify(__instance);
        if (gun is null) return true;
        var windup = GunOptions.Safe(gun.Options.WindupSeconds.Value, 0, 0, 10);
        return windup == 0 || (GameInput.GetButton(GameInput.ButtonCode.SecondaryClick) && State(__instance).Windup >= windup);
    }
}
