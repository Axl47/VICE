# VICE / S1API beta loading fixes

Checked October 7, 2026 with Schedule I 0.4.7f11 / IL2CPP, MelonLoader 0.7.3, Bars S1API 3.2.1-beta.7, Workshop 0.2.5 and Preview 0.12.1.

S1API's packaging patch class targeted `ProductIconManager.GenerateIcons`, which the installed beta no longer exposes. PatchAll aborted that class. The local repair changes the prefix and finalizer targets to the existing, exclusive native `GenerateRuntimeIcons(string) -> Sprite` method. The other packaging hooks can then register. It does not change S1API's public API, method bodies or resources.

Syndicate's load callback could start setup before the loaded flag and local player were available, then immediately enter cleanup. Its supplies list had not yet been initialized. Setup now waits for readiness, and cleanup tolerates an interrupted supplies initialization.

A freshly instantiated Enforcement officer has no runtime NPC data before Awake. Both modules now capture independent source data, let Unity populate the native component references, and then apply the detached data. They track their owned clones' GUIDs, restore them after native startup, and release clone registrations on session cleanup. The beta compiler inlines base startup into `PoliceOfficer.Start`, so the officer override needs its own hook. Police templates are excluded from the daily officer pool, and the source selection uses native pool members instead of another module's template. Existing item, quest and NPC IDs are retained.

## Verification

- Warning-free Enforcement and Syndicate builds. Core lifecycle/default checks, 3,456 Better Police assertions, 500 randomized squad scenarios and 15 Syndicate assertions pass.
- Native signature/address guards pass for 14 Enforcement and 39 Syndicate patch contracts. The new startup hooks have exclusive native targets.
- S1API comparison verifies 259,537 metadata, body and resource records are unchanged, excluding exactly the two intended Harmony target attributes. Reapplying the repair changes zero attributes; an unsupported assembly is rejected.
- An isolated profile copied the current installed mod setup, including the newer Preview and all VICE modules. A runtime probe loaded a copied save, returned to the menu and loaded it again. Both sessions retained 18 pooled officers; the template, investigator and buy-bust officer brought the tested cohort to 21. Their data, GUIDs and IDs were distinct, native components and registries were valid, reserved officers stayed outside the daily pool, and the officer count did not grow on reload. Session cleanup passed.
- The successful two-session run logged no errors or exception entries. All 47 copied save files retained their hashes. Original saves were not loaded by the test. Raw logs, probe implementation, profiles and disposable saves remain local and ignored.

This is a host-side loading/identity check, not a multiplayer or raid/quest playthrough. The known custom sleeve composite-icon fallback warnings remain; they are separate from the fixed Harmony error.

## Reproducing the dependency repair

Produce a separate DLL while keeping the original installed dependency as the comparison input:

```powershell
& scripts/Repair-S1APIBetaIconHook.ps1 -InputPath '<original-S1API.dll>' -OutputPath 'artifacts/vice-load-fix/S1API.Il2Cpp.MelonLoader.dll'
& scripts/Check-S1APIBetaIconHook.ps1 -OriginalPath '<original-S1API.dll>' -RepairedPath 'artifacts/vice-load-fix/S1API.Il2Cpp.MelonLoader.dll'
```

The repair is limited to the inspected Bars build (`db1de3989400b32baaa62ca6a536427754caefc5`) and refuses an incompatible game signature. It is a local dependency hotfix, not a new upstream S1API release. Installation backs up the three replaced DLLs; the remaining installed mods, libraries and configuration are preserved.
