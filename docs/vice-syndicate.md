# VICE Syndicate 0.1.0

VICE.Syndicate is the cartel module in the personal VICE suite. VICE.Enforcement owns police; Syndicate owns cartel events and difficulty. It targets Schedule I **0.4.7f11 beta / IL2CPP**, Unity 2022.3.62f2, MelonLoader 0.7.3 and .NET 6 x64. There is no Mono build.

The baseline retains configurable ambush locations and weapons, physical dealer robberies, drive-bys, deal interception, enhanced cartel dealers, stolen customers/items, gatherings, business sabotage, dead-drop mini quests, six hostile/allied end-game quests, dealer persuasion/recruitment and allied supplies. Release 2.0.0's nine JSON defaults are included and embedded so extra ambush locations work on first launch. Existing files are preserved.

## Build and install

Run `./scripts/Build-VICE.ps1 -Modules Syndicate` to compile the current Core, Syndicate and its checks, then export both DLLs in one update ZIP. The command prints the export directory and ZIP path. With the game closed, run `./scripts/Install-VICE.ps1 -PackageDir <export-directory>`, or build with `-Install`. `Build-VICESyndicate.ps1` forwards to this shared workflow.

The package installs `Mods/VICE.Syndicate.dll` and shared `UserLibs/VICE.Core.dll`. Keep one Core DLL in UserLibs for the VICE modules. Remove the original Cartel Enforcer DLL before installing Syndicate. The installer preserves existing JSON settings and backs up replaced DLLs. Select multiple modules or `-All` to export them together with the same Core.

## Configuration and difficulty

JSON settings and save-specific records live under `UserData/VICE.Syndicate/`. Melon preferences use category `VICESyndicate`. No original Cartel Enforcer settings or persistent records are imported automatically.

The difficulty defaults carry over Personal Better Police's hostile cartel settings:

| Setting | Default | Effect |
| --- | --- | --- |
| `RivalGang` | `true` | Enable the additional hostile difficulty on the host. |
| `GangCooldownMultiplier` | `0.5` | Halve newly generated native global and regional cooldowns; round up, minimum one game hour. |
| `GangAmbushRadiusMultiplier` | `1.5` | Expand ambush detection radius by 50% during its check, then restore it even after an exception. |

Existing values under `PersonalBetterPolice` seed these settings once; an existing Syndicate value wins. Truce status, native progression and host authority remain required. Already saved cooldowns finish normally. Nonzero custom event cooldowns in `EventFrequency/config.json` take precedence over native cooldown generation.

Syndicate removes only the legacy Personal Better Police cartel hooks if that DLL is still loaded, retaining its police hooks. VICE.Enforcement excludes the cartel hooks entirely. This prevents a second 0.5 multiplier or a second radius expansion.

## Beta port and shared Core

The port uses the new handover signature, Conversation dialogue containers, SleepController events, NPC speed controls, replicated health, StoredItemPrefab and modern NakedAppearance/avatar objects. Dealer clothing persuasion compares worn object IDs and color properties. Quest officer looks keep their uniform while changing skin/hair; the enforcer boss uses modern appearance and available vest/chain/watch objects.

VICE.Core supplies patch helpers, migration of personal settings, default-file seeding and SubscriptionScope. Syndicate releases global time/sleep/cartel-status handlers on save exit and rebinds the load callback when the LoadManager changes. Preferences now update boolean feature flags correctly. Harmony hooks install after configuration is ready.

## Validation and playtest

The build treats warnings as errors. Compiled-metadata checks verify Harmony targets and parameter contracts against the installed beta and audit their native addresses across Cpp2IL assemblies. Only identical same-owner FishNet writer aliases are accepted; unrelated shared native bodies fail the build. Pure checks cover cooldown rounding, bounds and invalid numeric settings.

Compilation and metadata checks do not establish live gameplay correctness. Verify on a copied save: load/exit/reload twice without duplicate events, check hostile and truced states, trigger each event in debug mode, complete hostile/allied quests, compare dealer clothes for persuasion, save/reload stolen inventory and customers, and test matching module versions on multiplayer peers. Beta portrait, boss apparel and clone navigation changes still need visual playtesting.

The initial 0.4.7f11 build passed with zero warnings/errors, 15 assertions and 37 patch contracts/native-address checks. Use `syndicate help`, `syndicate list <target>` and `syndicate start <target>` in the game console after loading a save for event testing.

## Provenance and license

Source baseline: [XOWithSauce/schedule-cartelenforcer](https://github.com/XOWithSauce/schedule-cartelenforcer), commit `4b674404fffe5109b46b990387af46407917d223`, upstream version 2.0.0. JSON defaults are from the 2.0.0 IL2CPP release. Only the IL2CPP source branches were imported into this monorepo, with independent VICE identity and versioning; no GitHub fork was created.

Original source copyright **2026 XOWithSauce**, MIT licensed. The complete original notice remains in `src/VICE.Syndicate/LICENSE.CartelEnforcer`, is embedded in the DLL and is included under `Licenses/CartelEnforcer-MIT.txt` in the package. Personal VICE adaptations are by Axl47. Renaming does not replace the original source copyright.
