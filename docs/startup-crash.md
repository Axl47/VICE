# Startup crash investigation

The original 0.1.0 build crashed before the main menu. Four launches on October 1, 2026 at 00:28–00:31 produced Windows Application Error 1000 / .NET Runtime 1023 events: `coreclr.dll` 6.0.36, access violation `0xc0000005`, fault offset `0x1d4089`.

All four local dumps have the same failing managed chain: `PersonalMods.BetterPolice.BetterPoliceMod.PursuitTick` → `Behaviour.get_Npc` → native exception formatting → `PursuitTick` again. This also occurs in the attempts with Highballer removed and with More Guns removed. The subsequent Warehouse/S1API/TheRegulars run reached the save twice and accepted a joining player.

## Cause and correction

Cpp2IL metadata identifies `PursuitBehaviour.OnActiveTick` as a seven-byte native thunk at RVA `0x434630`, shared by **2,613 methods** across generated assemblies, including unrelated constructors. Detouring this body sends non-pursuit objects into the police callback; exception construction repeats the callback and terminates the runtime.

**Better Police 0.1.1** uses the distinct `PursuitBehaviour.BehaviourUpdate` body and checks that the pursuit is active. Native pursuit-field reads are inside the managed error boundary.

The same audit identified two other cases addressed in this package:

- **Warehouse 0.1.1:** the `DarkMarket.Unlocked` getter is shared with curfew, sewer-key and checkpoint flags. The mod now unlocks the warehouse during its own update, using native unlock notification on the host and its own backing field/event on clients. It no longer detours the shared getter.
- **Casino 0.1.1:** Blackjack and Ride the Bus share `GetBetLimits`. One detour selects the configured limit from the object's actual native type, replacing two overlapping detours.

`Check-NativeTargets.ps1`, invoked by the compatibility/build script, now checks address sharing across all generated Cpp2IL assemblies. Unexpected shared targets fail the build. Only the exact reviewed Casino pair and Customer/FishNet writer pair are allowed. The latter has the same owner and arguments; its observer-writer behavior is intentionally covered by the customer hook.

## Installation and verification

Close the game, then replace **Personal.BetterPolice.dll**, **Personal.Warehouse.dll** and **Personal.Casino.dll** with their 0.1.1 versions from `PersonalMods-0.2.0/Mods`. Add the other desired personal DLLs, including `Personal.GasPurchases.dll`. Do not leave duplicate copies of earlier DLLs in `Mods`.

Verify those three mods report **0.1.1** in `MelonLoader/Latest.log`, reach the main menu, and load a save. Then follow `playtest.md`. These changes remove the unsafe native targets identified in the dumps; the corrected package still needs a fresh game run to confirm startup and gameplay. The live game process and installed files were left to the owner for manual installation.

The dumps remain local and excluded from Git. No dump or save data is included in this repository or package.
