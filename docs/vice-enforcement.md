# VICE Enforcement

Enforcement adds property heat and raids, disguised investigators, surveillance cameras, patrols and sentries, buy busts, snitching samples, drug apprehension, open-carry enforcement, and configurable officers. Pursuits include coordinated pressure, interception, alternative entrances, shared sightings, and nearby backup. Cartel events belong to [Syndicate](vice-syndicate.md).

Install `VICE.Enforcement.dll` in `Mods` and `VICE.Core.dll` in `UserLibs`. Remove replaced police mods using the package's upgrade instructions. Every multiplayer participant needs matching binaries and a restart after updating.

## Configuration

Preferences are in `UserData/MelonPreferences.cfg`. Restart after editing.

| Category | Controls |
| --- | --- |
| `VICEEnforcement` | Module enablement and event feature switches. |
| `VICEEnforcementPolice` | Pursuit speed, detection, accuracy, arrest, backup, and flanking. |
| `VICEEnforcementInformation` | Phone reports: `Enabled`, `ContactName`, and `CooldownGameMinutes`. |

JSON configuration and patrol routes are in `UserData/VICE/Enforcement`. Missing defaults are created without replacing existing files. Heat and information history persist per save slot and organization outside native saves; include this folder in mod-data backups. Original NACops heat is not automatically imported.

Police adjustments and event systems coordinate their settings to avoid duplicate movement, vision, arrest, and combat overrides. Officer weapons, health, building access, and body searches remain configurable.

## Local Source contact

**Local Source** appears in the phone's **Messages** app after loading a save. It reports broad pressure changes around owned properties without revealing numerical heat, investigator identities, or raid predictions. Its default shared cooldown is three game hours, configurable from 30 to 1,440 game minutes.

| Band | Pressure rises to | Pressure falls to |
| --- | --- | --- |
| Attention | 6 | Quiet at 3 or lower |
| Heavy attention | 14 | Attention at 10 or lower, or Quiet at 3 or lower |

Bands remain fixed if the raid threshold changes. Small fluctuations do not repeatedly trigger messages. Rising pressure takes priority; each property retains its latest pending transition. Reports wait while a raid is active.

First load and new properties establish a silent baseline. A raid's heat reset does not generate a calm report. Sleep advances the cooldown; pause time does not. Up to 20 reports and the remaining cooldown survive reloads.

The host generates reports for loaded organization participants. Rejoining players receive history silently, then notifications for new reports. Disabling the contact locally hides it; disabling it on the host stops reports for everyone.

## Multiplayer

The host schedules events, spawns officers, and evaluates camera evidence separately for each player. Guests display replicated officer appearances and camera state.

Genuine two-player verification remains outstanding for officer appearances, late joins, camera impacts, and report delivery. Check these on a copied save. Report problems with relevant `MelonLoader/Latest.log` lines.

See the [build guide](vice-build.md), [playtest checklist](playtest.md), and [credits](provenance.md).
