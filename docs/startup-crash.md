# Startup crash investigation

The original 0.1.0 build crashed before the main menu. Four launches on October 1, 2026 at 00:28–00:31 produced Windows Application Error 1000 / .NET Runtime 1023 events: `coreclr.dll` 6.0.36, access violation `0xc0000005`, fault offset `0x1d4089`.

All four local dumps have the same failing managed chain: `PersonalMods.BetterPolice.BetterPoliceMod.PursuitTick` → `Behaviour.get_Npc` → native exception formatting → `PursuitTick` again. This also occurs in the attempts with Highballer removed and with More Guns removed. The subsequent Warehouse/S1API/TheRegulars run reached the save twice and accepted a joining player.

## Cause and correction

Cpp2IL metadata identifies `PursuitBehaviour.OnActiveTick` as a seven-byte native thunk at RVA `0x434630`, shared by **2,613 methods** across generated assemblies, including unrelated constructors. Detouring this body sends non-pursuit objects into the police callback; exception construction repeats the callback and terminates the runtime.

**Better Police 0.1.1** uses the distinct `PursuitBehaviour.BehaviourUpdate` body and checks that the pursuit is active. Native pursuit-field reads are inside the managed error boundary.

The same audit identified two other cases addressed in this package:

- **Warehouse 0.1.1:** the `DarkMarket.Unlocked` getter is shared with curfew, sewer-key and checkpoint flags. The mod now unlocks the warehouse during its own update, using native unlock notification on the host and its own backing field/event on clients. It no longer detours the shared getter.
- **Casino 0.1.1:** Blackjack and Ride the Bus share `GetBetLimits`. This version replaced two overlapping detours with one, but later live testing revealed an owner-marshalling error for the sibling controller. **Casino 0.1.2** removes the shared detour entirely and changes the betting panel's slider range after it opens; see `compatibility.md`.

`Check-NativeTargets.ps1`, invoked by the compatibility/build script, now checks address sharing across all generated Cpp2IL assemblies. Unexpected shared targets fail the build, including the former Casino pair. Only the exact reviewed Customer/FishNet writer pair is allowed: it has the same native owner and arguments, and its observer-writer behavior is intentionally covered by the customer hook.

## Installation and verification

Close the game, then use **Better Police 0.2.0**, **Warehouse 0.1.1** and **Casino 0.1.2** from `PersonalMods-0.3.3/Mods`. Better Police retains the original startup correction and adds the [squad/gang update](police-coordination.md). Add the other desired personal DLLs. Do not leave duplicate copies of earlier DLLs in `Mods`.

Verify the versions above in `MelonLoader/Latest.log`, reach the main menu, and load a save. Then follow `playtest.md`. On October 1 the owner confirmed the corrected startup mods run, and the local log shows all six loading and successful save loads. This establishes recovery from the reported startup crash; weapon reload/animation issues and the later casino slider failure are addressed separately in More Guns 0.2.0 and Casino 0.1.2. The live game process and installed files were left to the owner for manual installation.

The dumps remain local and excluded from Git. No dump or save data is included in this repository or package.
