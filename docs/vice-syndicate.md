# VICE Syndicate

Syndicate adds cartel ambushes, dealer robberies, drive-bys, deal interception, enhanced dealers, stolen customers/items, gatherings, business sabotage, dead-drop quests, hostile/allied end-game quests, recruitment, and allied supplies. Police behavior belongs to [Enforcement](vice-enforcement.md).

Install `VICE.Syndicate.dll` in `Mods` and `VICE.Core.dll` in `UserLibs`. Remove original Cartel Enforcer before installing. All multiplayer participants need matching binaries; persistent records and event scheduling run on the host.

## Configuration

Preferences use `VICESyndicate` in `UserData/MelonPreferences.cfg`. JSON settings and save-specific records are in `UserData/VICE/Syndicate`. Missing defaults are created without replacing existing files. Original Cartel Enforcer records are not imported automatically. Restart after editing preferences.

| Setting | Default | Effect |
| --- | --- | --- |
| `RivalGang` | `true` | Additional hostile difficulty on the host. |
| `GangCooldownMultiplier` | `0.5` | Halve newly generated native cooldowns, rounded up to at least one game hour. |
| `GangAmbushRadiusMultiplier` | `1.5` | Expand hostile ambush detection radius by 50%. |

Saved cooldowns finish normally. Custom nonzero cooldowns in `EventFrequency/config.json` take precedence over native generation. Progression, truce state, and eligibility still apply.

## Checking gameplay

Use a copied save to check hostile/truced states, quests, persuasion, stolen inventory/customer persistence, and load/exit/reload behavior. Multiplayer, character appearance, and clone navigation require live playtesting.

For event diagnostics after loading a save, use `syndicate help`, `syndicate list <target>`, or `syndicate start <target>` in the console. Debug events do not establish normal scheduled frequency.

See the [build guide](vice-build.md), [playtest checklist](playtest.md), and [credits](provenance.md). Syndicate adapts XOWithSauce's MIT-licensed Cartel Enforcer; its original notice remains in the source and package.
