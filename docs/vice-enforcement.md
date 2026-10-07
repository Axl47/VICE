# VICE Enforcement 0.1.0

The first police module in the **VICE** family combines the IL2CPP side of [XOWithSauce's NACops 2.1.3](https://github.com/XOWithSauce/schedule-nacops/tree/97cedcd9f06e16bf16f9460493b87bc2fe460560) with Personal Better Police 0.2.0's police changes. It targets the installed **Schedule I beta 0.4.7f11**, Unity 2022.3.62f2 and MelonLoader 0.7.3, x64 / .NET 6. The upstream release targeted 0.4.6f13. Source and IL2CPP release defaults are pinned to commit `97cedcd9f06e16bf16f9460493b87bc2fe460560`; its MIT notice is preserved in `src/VICE.Enforcement/LICENSE.NACops` and the package.

VICE Enforcement retains property heat/raids, disguised investigators, camera surveillance, extra patrols/sentries, buy busts, snitching samples, drug apprehension, open-carry enforcement and configurable officer health/weapons/body searches. It also retains Better Police's coordinated pressure/interception, reachable alternative entrances, spaced support, shared sightings, nearby backup and personal speed/vision/aim/arrest settings. AK47 and minigun open carry are recognized when Personal More Guns is installed.

**No cartel or rival-gang code is compiled into this module.** Those changes belong to **VICE.Syndicate**. Installing Enforcement replaces Personal Better Police as the police owner; the older DLL's gang adjustments cease with it unless Syndicate is also installed.

## Shared family layout

| File | Destination | Responsibility |
| --- | --- | --- |
| `VICE.Core.dll` | `UserLibs` | Shared patch/config helpers, preference migration, default files and event lifetime management. |
| `VICE.Enforcement.dll` | `Mods` | Police behavior and NACops events; one Melon entry point. |

Core is a reusable dependency, with no gameplay hooks of its own. The other personal modules remain separately installable; this change begins the family migration. Future modules reference the same Core. Keep just one Core copy in `UserLibs`.

## Configuration and merge policy

New NACops feature switches use `VICEEnforcement` in `UserData/MelonPreferences.cfg`. `Enabled=false` disables the whole module on the next launch. JSON settings and patrol data live in `UserData/VICE.Enforcement/`; packaged defaults are also embedded and seed only missing files. Existing files and heat data are preserved. The defaults contain 17 foot routes, 18 sentry entries and two vehicle routes.

Better Police's settings migrate into `VICEEnforcementPolice`. The first launch uses values from `PersonalBetterPolice` as defaults, including your speed, FOV, accuracy, backup and flanking settings. Existing values in the new category take precedence on subsequent launches. The old category remains available for rollback. Its `Enabled` switch controls only the Better Police adjustments inside Enforcement.

Better Police owns movement multiplier, vision/notice timing, arrest ticks and native combat/search behavior. NACops' `OverrideMovement`, `OverrideVision`, `OverrideArresting` and `OverrideCombatBeh` are forced off in this combined build. NACops still controls officer weapon type/base damage/magazine/taser settings, health, building access and body-search configuration. Better Police applies its minimum accuracy and aiming multiplier to the configured weapon. This prevents two movement/vision/arrest systems from overwriting each other.

New cosmetics use the beta's modern avatar objects. Investigators borrow a loaded civilian's appearance/outfit and hide their police belt. Additional officers and raiders retain native police outfits with randomized appearance; the old raid clothing texture layers are no longer used.

Event scheduling, clones, property raids and surveillance run on the host. Multiplayer replication of the upstream runtime NPCs is not playtested; matching versions/settings are recommended. Restart after changing settings. Heat persists per save slot and organization in `UserData/VICE.Enforcement/HeatData`, independently of native save files. Old NACops heat files are not automatically imported.

## Build and install

From this repository in PowerShell:

```powershell
./scripts/Build-VICEEnforcement.ps1
./scripts/Install-VICEEnforcement.ps1
```

Alternatively run the build with `-Install`. Both scripts accept `-GameDir`. Build uses the local portable SDK when available and creates `artifacts/VICE.Enforcement-0.1.0.zip`. No original Mono project or DLL is used in the build. The old personal suite remains buildable.

The installer requires the game to be closed. It backs up existing police/Core binaries, disables `Personal.BetterPolice`, original BetterPolice/NACops and duplicate Core copies in `Mods`, then copies Enforcement into `Mods` and Core into `UserLibs`, verifies hashes and seeds missing data files. It preserves other modules and existing settings. To roll back with the game closed, remove Enforcement and restore the prior police DLL from the printed backup folder; keep Core while other VICE modules require it. The old suite's installer skips Personal Better Police when Enforcement is installed.

Manual installation uses the package's `Mods`, `UserLibs` and `UserData` layout. Merge data without overwriting personal edits. Do not keep original NACops or Better Police active alongside Enforcement; it refuses to initialize when their assemblies are present.

## Validation and first playtest

Compilation uses warnings as errors against the installed IL2CPP assemblies. Automated checks verify Core cleanup/default-file preservation, Better Police's obstacle-route planner and sight memory, all active attribute hook signatures, exclusive native patch addresses, one IL2CPP entry point, and exclusion of cartel types. These checks establish build/API compatibility; gameplay and multiplayer correctness require a live playtest.

The installer was exercised twice against an isolated fixture, verifying police/Core replacement, DLL hashes, default-file seeding, preservation of existing settings and unrelated modules, and safe reinstallation. This fixture check does not install into the real game.

Use a disposable or copied save first. Verify startup with both VICE assemblies loaded, then load the save and wait for officer setup. In the in-game console, `enforcement help` lists commands (`nacops` remains an alias). Check `enforcement list footpatrol`, `enforcement list sentry` and `enforcement list raid`, then exercise patrols, investigator, camera detection and a pursuit with multiple officers. Raid tests can destroy equipment and seize product, so exercise them only in the copied save. Check save/reload and returning to the menu twice to verify isolated heat data and event cleanup. Inspect MelonLoader logs for errors and verify native police, personal weapons and other installed modules still behave correctly.
