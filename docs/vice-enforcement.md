# VICE Enforcement 0.1.5

Version 0.1.5 repairs runtime police appearances and adds explicit multiplayer officer/camera synchronization. Install matching Enforcement 0.1.5 and Core on the host and every guest, then restart the game. Existing saves, property heat, preferences and the Local Source contact are retained.

Version 0.1.4 adds **Local Source**, a provisional information contact in the native phone's **Messages** app. It appears automatically after loading a save; no NPC, purchase or introduction quest is required. The name is configurable, and the character's identity/backstory is deferred. It has text-only presentation using the native contact UI.

Version 0.1.3 uses FishNet's registered network manager during readiness polling, so guests no longer search the entire scene every frame. Host police settings and event behavior are preserved.

The first police module in the **VICE** family combines the IL2CPP side of [XOWithSauce's NACops 2.1.3](https://github.com/XOWithSauce/schedule-nacops/tree/97cedcd9f06e16bf16f9460493b87bc2fe460560) with Personal Better Police 0.2.0's police changes. Version 0.1.4 is built and checked against the installed **Schedule I beta 0.4.7f12**, Unity 2022.3.62f2 and MelonLoader 0.7.3, x64 / .NET 6. The upstream release targeted 0.4.6f13. Source and IL2CPP release defaults are pinned to commit `97cedcd9f06e16bf16f9460493b87bc2fe460560`; its MIT notice is preserved in `src/VICE.Enforcement/LICENSE.NACops` and the package.

VICE Enforcement retains property heat/raids, disguised investigators, camera surveillance, extra patrols/sentries, buy busts, snitching samples, drug apprehension, open-carry enforcement and configurable officer health/weapons/body searches. It also retains Better Police's coordinated pressure/interception, reachable alternative entrances, spaced support, shared sightings, nearby backup and personal speed/vision/aim/arrest settings. AK47 and minigun open carry are recognized when Personal More Guns is installed.

**No cartel or rival-gang code is compiled into this module.** Those changes belong to **VICE.Syndicate**. Installing Enforcement replaces Personal Better Police as the police owner; the older DLL's gang adjustments cease with it unless Syndicate is also installed.

## Shared family layout

| File | Destination | Responsibility |
| --- | --- | --- |
| `VICE.Core.dll` | `UserLibs` | Shared patch/config helpers, preference migration, default files and event lifetime management. |
| `VICE.Enforcement.dll` | `Mods` | Police behavior and NACops events; one Melon entry point. |

Core is a reusable dependency, with no gameplay hooks of its own. The other personal modules remain separately installable; this change begins the family migration. Future modules reference the same Core. Keep just one Core copy in `UserLibs`.

## Configuration and merge policy

New NACops feature switches use `VICEEnforcement` in `UserData/MelonPreferences.cfg`. `Enabled=false` disables the whole module on the next launch. JSON settings and patrol data live in `UserData/VICE/Enforcement/`; packaged defaults are also embedded and seed only missing files. Existing files and heat data are preserved. The defaults contain 17 foot routes, 18 sentry entries and two vehicle routes.

Better Police's settings migrate into `VICEEnforcementPolice`. The first launch uses values from `PersonalBetterPolice` as defaults, including your speed, FOV, accuracy, backup and flanking settings. Existing values in the new category take precedence on subsequent launches. The old category remains available for rollback. Its `Enabled` switch controls only the Better Police adjustments inside Enforcement.

The personal chase tuning uses `ChaseSpeedMultiplier = 0.78` and `DistantChaseSpeedMultiplier = 1.2`, 40% below the previous 1.3/2.0 values across the pursuit distance curve. `NoticeTimeMultiplier = 1.875` makes detection take 2.5 times as long as the previous 0.75 setting, reducing detection speed by 60%. These are also the defaults when no existing or legacy preferences are present. Normal patrol movement and vision range are unchanged.

Better Police owns movement multiplier, vision/notice timing, arrest ticks and native combat/search behavior. NACops' `OverrideMovement`, `OverrideVision`, `OverrideArresting` and `OverrideCombatBeh` are forced off in this combined build. NACops still controls officer weapon type/base damage/magazine/taser settings, health, building access and body-search configuration. Better Police applies its minimum accuracy and aiming multiplier to the configured weapon. This prevents two movement/vision/arrest systems from overwriting each other.

New cosmetics use the beta's modern avatar objects. Investigators borrow a loaded civilian's appearance/outfit and hide their police belt. Additional officers and raiders retain native police outfits with randomized appearance; the old raid clothing texture layers are no longer used.

Event scheduling, runtime NPC creation, property raids and camera detection run on the host. Guests register the same officer prefab and render host-selected appearances and camera states. A real two-player check remains required. Restart after changing settings. Heat persists per save slot and organization in `UserData/VICE/Enforcement/HeatData`, independently of native save files. Old NACops heat files are not automatically imported.

## Runtime appearances and multiplayer cameras

The old appearance capture moved live officers to one shared offscreen position across coroutine yields. Several captures could therefore photograph overlapping NPCs and their hair, and movement replication could expose those staging teleports. Version 0.1.5 captures one NPC's baked renderer geometry synchronously, including its material properties, on an isolated render layer. It does not teleport or pause the network NPC. Investigator outfit replacement removes both registered avatar objects and orphaned children inherited from a live clone before applying a fresh appearance. The civilian donor's data remains unchanged.

Runtime officers use a dedicated FishNet prefab collection (`47123`, slot `0`), registered on each peer during Main scene initialization. Clones have no inherited scene identity or network parent. FishNet allocates their network IDs; Enforcement no longer manually initializes every clone with ID 150 or disables networking before spawning. Native network behaviours remain in the same order on host and guest. The host sends object-ID/GUID/identity, appearance and visibility snapshots in bounded batches. Guests restore the appearance on the existing native spawned object rather than creating independent substitute officers. Native movement/combat replication remains responsible for motion and interaction. Reserved investigators and buy-bust officers are excluded from the regular police pool on both sides. Prefab registration and packet callbacks are released on save exit, including when Unity has already destroyed the template.

Camera components are now created on guests as well as the host. The host selects active cameras and publishes their active, broken, sighted, progress and beam-target state twice per real second. Late-joining guests receive repeated current snapshots. Guest components render this state and do not independently choose cameras or dispatch police.

The host checks each loaded player against range, camera direction and unobstructed sight. Ray samples use the eyes, midpoint and body center with floating-point interpolation; a full ray-hit buffer cannot establish clear sight. Detection progress, evidence expiry and camera cooldown are separate for each camera/player pair, so observing the host does not hide or implicate a guest. Client-computed visibility and allowlisted visual-state labels are sent four times per second, bound to the authenticated connection's loaded player; the host rechecks geometry and ignores stale observations after two seconds. It applies the existing crime/dispatch policy to the player actually seen. Owner crime/pursuit updates use deduplicated, acknowledged notices to that connection, without replaying another player's evidence. Guest camera impacts request host approval; active state, damage proximity (75 world units), one-time XP and broken/reset state are handled on the host. Daily camera rotation retains the existing selection policy.

These changes require the module on every participant. A guest running an older Enforcement build cannot register the new runtime officer prefab or camera protocol. Host/guest visibility, local furniture/NPC presentation mods, late joins and guest camera impacts should be checked in a genuine two-player session before treating multiplayer behavior as verified.

## Information contact

Local Source reports broad changes in pressure around owned properties, using their native readable names. Examples include “Bungalow is getting attention. Keep a low profile,” “There's a lot of attention around Barn. Be careful over there,” and “Things are quiet around Bungalow again. Keep it that way.” A heavy-to-moderate transition says attention has eased but has not gone quiet. These reports describe the existing property heat; they do not expose numerical heat, investigator identities, raid eligibility, predicted raid times or guarantees.

The initial policy uses the existing investigation scale: investigators can add roughly 2–9 heat before multipliers, low-heat additions use a threshold of 8, sleep decays heat only above 12, and the default raid heat threshold is 14. Reports use these fixed bands, independently of any customized raid threshold:

| State | Entering from below | Leaving toward calm |
| --- | --- | --- |
| Quiet | Initial heat below 6 | Attention settles to Quiet at 3 or lower |
| Attention | 6 or higher | Remains Attention through 4–5 |
| Heavy attention | 14 or higher | Drops to Attention at 10 or lower; directly to Quiet at 3 or lower |

Hysteresis prevents repeated messages near thresholds. Heat is sampled on the host once per real second from Enforcement's existing heat list. Investigation increases/decreases and sleep decay therefore produce reports only when they cross a stabilized band. The contact does not alter police behavior or add heat decay: heat at 12 can stay under attention until an existing investigation reduces it. The existing raid-selection heat reset explicitly establishes a silent Quiet baseline, discarding that property's pending report. A raid resetting heat is not evidence of calm. All reports are held while an Enforcement raid is active.

Settings live in `UserData/MelonPreferences.cfg`, under `VICEEnforcementInformation`. Restart after editing. `Enabled` defaults to `true`; `ContactName` defaults to `Local Source`; `CooldownGameMinutes` defaults to `180` and is clamped to 30–1440. The host's cooldown controls generation. Disabling Enforcement also disables this contact. A participant can disable their own contact display; disabling it on the host stops reports for everyone.

The cooldown is shared across properties. Rising pressure takes priority, then the highest current band, then the oldest pending transition. Each property retains only its latest stabilized state relative to its last reported state. If pressure rises and falls back to the last reported band during cooldown, the pending warning is cancelled. If it remains above that band, the latest pressure is reported when eligible. A Quiet message requires a calming transition from a previously established or reported pressure state; it is never sent every day or on every load. After one report, other pending properties wait for the next global interval.

First load establishes a silent baseline for every tracked property; buying/encountering a new property also establishes a baseline. Up to 20 recent reports, their identities, stabilized bands, last reported bands, pending transitions and remaining cooldown persist in `UserData/VICE/Enforcement/InformationData`. Filenames include the save slot and a SHA-256 hash of the exact organization name to avoid sanitized-name collisions. This follows heat's external per-slot/per-organization lifetime. State uses an atomic replacement and is checkpointed as game time or report state changes, on native save, and before save exit. Missing or unreadable data starts with a silent baseline and logs a warning for unreadable data.

The clock uses native total game minutes; sleep advances the cooldown, while pause/menu time does not. Remaining cooldown survives reload and is reanchored to the loaded game clock, so loading an earlier save cannot instantly release it. If saved heat differs from a stored property's last observation, that property is silently reconciled to the loaded heat and stale pending work is discarded. History and the remaining global cooldown are retained. Leaving a save clears pending in-memory state and deduplication, destroys the contact UI, removes native registrations and unregisters the receive callback before another save starts.

### Multiplayer and native phone behavior

The host alone reads heat, accesses `ActiveSaveInfo`, generates reports and writes report data. Every authenticated, loaded player in the current organization receives the same bounded text history through reliable FishNet snapshots; the host displays its own report locally. Guests never access host-only save metadata or heat files. Matching Enforcement/Core binaries are required. Late-loading/rejoining participants receive the first snapshot silently as history, then see new reports with the normal native message notification. Snapshots repeat every five real seconds for delivery recovery; stable report IDs prevent repeated notifications. A host receiving a packet ignores it, and there is no guest-to-host report submission path.

The implementation reuses the workshop's `MessageContactInfo` / `MSGConversation` pattern and the repository's synchronous IL2CPP packet payload convention. Native `SendMessage(message, notify, network)` supplies phone bubbles, unread state and alerts. Enforcement passes `network=false` and excludes its custom conversation from native replication/save registration, because its late-created custom thread cannot rely on native spawn-time conversation synchronization. Enforcement owns bounded history and delivery instead, preventing native/custom duplicate sends. No S1API dependency or new phone app is added.

## Build and install

From this repository in PowerShell:

```powershell
./scripts/Build-VICE.ps1 -Modules Enforcement
./scripts/Install-VICE.ps1 -PackageDir <printed-export-directory>
```

Alternatively run the build with `-Install`. Both scripts accept `-GameDir`. The unified build uses the local portable SDK when available and prints a fresh export directory/ZIP containing Enforcement and the current Core. `Build-VICEEnforcement.ps1` forwards to this same workflow. No original Mono project or DLL is used in the build. The old personal suite remains buildable.

The installer requires the game to be closed. It backs up existing police/Core binaries, disables `Personal.BetterPolice`, original BetterPolice/NACops and duplicate Core copies in `Mods`, then copies Enforcement into `Mods` and Core into `UserLibs`, verifies hashes and seeds missing data files. It preserves other modules and existing settings. To roll back with the game closed, remove Enforcement and restore the prior police DLL from the printed backup folder; keep Core while other VICE modules require it. The old suite's installer skips Personal Better Police when Enforcement is installed.

Manual installation uses the package's `Mods`, `UserLibs` and `UserData` layout. Merge data without overwriting personal edits. Do not keep original NACops or Better Police active alongside Enforcement; it refuses to initialize when their assemblies are present.

## Validation and first playtest

Version 0.1.5 passed 60 automated Enforcement assertions, the existing 3,456 Better Police assertions, Core's 55 assertions, native patch signature/address checks, package hashing and the installer fixture. The new assertions cover camera cone/range/ray fractions, separate host/guest evidence and cooldowns, stale evidence expiry, visibility rolls, allowed remote labels, snapshot validation and removal of the legacy manual network initialization.

The isolated native repair diagnostic passed two loaded sessions on beta 0.4.7f12. It verified the registered non-scene officer prefab, distinct genuinely spawned network object IDs/data/GUIDs, six investigator outfit replacements without duplicate active hair or mutation of the civilian donor, nonempty isolated textures without capture moving the NPC, native appearance JSON/packet serialization, camera initialization/break/reset, and save-exit cleanup. Result: `artifacts/enforcement-repair-native/output-r4/repair-passed.txt`. It used a copied save and a separate loader directory. This is single-instance native validation, not a two-player result.

The required multiplayer follow-up is to load matching 0.1.5 binaries on both players, compare patrol/investigator/raider identities and appearances up close and at impostor distance, join after runtime officers already exist, check that each player independently triggers the same host-selected camera, and check guest impacts and reconnects without duplicate crimes. Cosmetic mods and network timing can affect these paths; matching settings and a copied save are recommended for that follow-up.

Compilation uses warnings as errors against the installed IL2CPP assemblies. Automated checks verify Core cleanup/default-file preservation, Better Police's obstacle-route planner and sight memory, all active attribute hook signatures, exclusive native patch addresses, one IL2CPP entry point, and exclusion of cartel types. These checks establish build/API compatibility; gameplay and multiplayer correctness require a live playtest.

Information-contact automated checks cover rising/calming transitions, hysteresis, raid suppression/reset, global cooldown, cancellation and priority across properties, saved pending work, clock rewind/reload, rolled-back heat, organization/slot separation, atomic disk persistence, packet payload/reset contracts, lifecycle forwarding and required native API signatures. These checks run automatically in the Enforcement build/export workflow. The native diagnostic project is `tests/VICE.Enforcement.Native`; it is a separate test mod and is never included in the export.

The single-instance native diagnostic on beta 0.4.7f12 uses a separate MelonLoader base directory and an unregistered save copy under `artifacts/enforcement-information-native`. It exercises actual native contact/UI creation, a silent initial baseline, rising and calming report bubbles, cooldown gating, native save/reload with silent history restoration, menu/UI cleanup and exactly one contact thread after reload, exclusion from native messaging/save registries, and the real native generic FishNet writer/reader with a Unicode report payload. The diagnostic expires one cooldown by changing only its isolated test state; automated checks separately verify the full game-minute expiry policy. It does not establish two-player delivery or notification appearance on a guest.

The final contact probe passed, with its result in `artifacts/enforcement-information-native/output-verified/passed.txt`. The minimal-loader run also logged a native `NPC.ExitBuilding` null reference during `PoliceOfficer.Activate`; no information-contact method appears in that stack. The contact checks passed, but this run does not certify all police gameplay on f12. Existing native avatar warnings also appear in the copied-save diagnostic.

Outstanding live checks: use two players with matching binaries to verify one alert per new report on each participant, silent late-join history, guest-disabled display, host-disabled generation, packet retry recovery, leaving/rejoining and switching between organizations/save slots. Also play through naturally occurring investigation and sleep transitions, confirm phone alert/audio presentation, and verify that a raid never produces a misleading Quiet report. The contact's text and provisional identity can be refined after those checks.

The installer was exercised twice against an isolated fixture, verifying police/Core replacement, DLL hashes, default-file seeding, preservation of existing settings and unrelated modules, and safe reinstallation. This fixture check does not install into the real game.

Use a disposable or copied save first. Verify startup with both VICE assemblies loaded, then load the save and wait for officer setup. In the in-game console, `enforcement help` lists commands (`nacops` remains an alias). Check `enforcement list footpatrol`, `enforcement list sentry` and `enforcement list raid`, then exercise patrols, investigator, camera detection and a pursuit with multiple officers. Raid tests can destroy equipment and seize product, so exercise them only in the copied save. Check save/reload and returning to the menu twice to verify isolated heat data and event cleanup. Inspect MelonLoader logs for errors and verify native police, personal weapons and other installed modules still behave correctly.

From 0.1.1, existing `UserData/VICE.Enforcement` data automatically moves into `UserData/VICE/Enforcement` before defaults are seeded. Existing destination files are preserved; conflicting legacy files are retained in `UserData/VICE/MigrationConflicts`. Install the updated Core 0.1.2 with this module.
