# Better Police 0.2.0

One pursuing officer keeps direct pressure while up to three others try to intercept from the front or sides. Roles remain stable between plans. The host probes complete NavMesh routes through outer waypoints and compares their final approaches. Officers can use another entrance when it is reachable, including when the player stands still. Detours hand movement back to native combat/arrest once the officer reaches the approach point.

Officers share only positions seen by a squad member; this memory expires after six seconds without a sighting. Normal lost-target searches continue. Plans exclude inactive, unconscious, vehicle-bound and non-police NPCs, and end when the player is arrested/unconscious or leaves an active foot pursuit. Routes use native movement and release blocked waypoints after five seconds without progress. Planning is limited to 96 path queries per pursued player every 1.5 seconds.

In a room with one entrance or a dead-end corridor, extra officers hold spaced reachable support positions while one applies pressure. The mod cannot create an extra entrance through a wall. Native weapon use, arrest, line-of-sight checks and network replication still handle the encounter. Nearby radio backup and the earlier speed/vision/aim settings remain available.

The same DLL increases hostile rival gang activity frequency. New global and regional cooldowns default to 50% of the native result, rounded up to at least one game hour. During the native ambush proximity check, configured detection radii are temporarily enlarged by 50%, then restored even if the check throws. This increases the opportunity to trigger an existing ambush without changing its spawn points, goon count, weapons or pool.

The native system still selects eligible activities and regions, respects influence/progression and avoids ambushing a player during a police pursuit. Unknown, truced and defeated cartel states keep their normal scheduling. Existing countdowns loaded from a save finish normally; the cooldown change begins with the next native roll. More frequent opportunities do not guarantee exactly twice as many attacks, since activity selection and player location still matter.

All settings are under `PersonalBetterPolice` in `UserData/MelonPreferences.cfg`. Restart after editing.

| Setting | Default | Effect |
| --- | --- | --- |
| `Flanking` | `true` | Enable squad routes. |
| `CoordinationIntervalSeconds` | `1.5` | Replan every 0.75–5 seconds. |
| `FlankRadiusMeters` | `18` | Probe alternative approaches at 10–40 meters, with some outer probes at 1.6 times this radius. |
| `MaximumDetourMultiplier` | `3` | Bound detours relative to the direct route, plus the probe radius. |
| `MaximumFlankingOfficers` | `3` | Up to four front/side/rear roles; other officers support. |
| `SquadSightMemorySeconds` | `6` | Keep observed target positions for 1–10 seconds. |
| `RivalGang` | `true` | Enable both hostile gang adjustments independently of flanking. |
| `GangCooldownMultiplier` | `0.5` | New native cooldown factor, clamped to 0.25–1. |
| `GangAmbushRadiusMultiplier` | `1.5` | Detection radius factor, clamped to 1–2. |

`Enabled=false` disables both police and gang adjustments. To keep tougher police with native gang frequency, set `RivalGang=false`. No extra DLL or save migration is required. Install this version manually by replacing `Personal.BetterPolice.dll` after closing the game; do not keep duplicate versions in `Mods`.

Automated checks cover two-door buildings, a closed rear entrance, dead-end corridors, invalid/incomplete paths, stable roles, bounded planning, expired sight memory and gang tuning boundaries. Native addresses are checked against the generated beta metadata. Actual routes, combat handoff, startup and encounter frequency still require the [live playtest](playtest.md).
