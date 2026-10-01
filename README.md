# Schedule One personal mods

Private monorepo containing five independently installable MelonLoader mods for the Steam **beta / IL2CPP** branch. Current target: **Schedule I 0.4.7f7**, **Unity 2022.3.62f2**, **MelonLoader 0.7.3**, x64 / .NET 6. The installed **S1API (Forked by Bars) 3.2.1-beta.7** and **TheRegulars 1.0.0** are retained. Highballer's optional custom-NPC guard also supports the previously inspected S1API 2.5.0 field layout.

Features and defaults:

| Mod | Purpose |
| --- | --- |
| `Personal.Warehouse.dll` | Warehouse access at all hours, including before the normal unlock rank. Oscar sells small/medium/large racks, small/medium/large/**huge storage closets**, lockers and cabinets at their existing prices. Additional existing storage IDs can be configured. |
| `Personal.BetterPolice.dll` | Pursuit speed increases from 1.3× to 2× with distance, wider and longer vision, shorter notice time, faster arrests, stronger aim, reachable interception destinations and nearby active backup officers. |
| `Personal.Highballer.dll` | Estimated counteroffer success and highest acceptable price at a 98% threshold. Exact packaged delivery planning with native handover validation. Optional automatic counteroffers, scheduling and quantity increases default off. S1API custom NPCs and special customers are skipped by default. |
| `Personal.Casino.dll` | Blackjack and Ride the Bus maximum wagers of $100,000; slot wager ladder multiplied by 100. Bets remain subject to available money and normal game payouts. |
| `Personal.MoreGuns.dll` | AK47 ($15,000, 30 rounds) and minigun ($75,000, 400 rounds), with matching spare magazines at the arms dealer. Automatic fire, minigun aim windup, native reloads, avatar models and magazine trash registration. Rank restrictions default off. |

Configuration is stored by MelonLoader in `UserData/MelonPreferences.cfg` under the `PersonalWarehouse`, `PersonalBetterPolice`, `PersonalHighballer`, `PersonalCasino`, `PersonalMoreGuns`, `PersonalMoreGuns_ak47` and `PersonalMoreGuns_minigun` categories. Restart after editing settings. Old mod config files are not imported.

## Build

Install a .NET SDK 8 or newer and launch the beta with MelonLoader once to generate `MelonLoader/Il2CppAssemblies`. Build references come directly from the installation. From PowerShell in this repo:

```powershell
./scripts/Build.ps1
```

Use `-GameDir`, `-OriginalMoreGuns` or `-DotnetPath` to override the installation, authorized original `MoreGuns.dll`, or SDK executable. `-NugetConfig` supports a local/offline package feed. `Game.local.props` can also set `GameDir` for direct IDE builds.

The build extracts the original weapon bundle into ignored `.research/assets/`, compiles the five DLLs, runs the algorithm checks and creates `artifacts/PersonalMods-0.1.0.zip`. More Guns requires the owner's original bundle; the other four projects can be built separately without it.

## Install and verify

Close the game, then run:

```powershell
./scripts/Install.ps1
```

Installation copies only the five personal DLLs into `Mods`, backs up any previous personal versions in `artifacts/install-backups/`, and verifies their hashes. It refuses to install alongside conflicting old versions. Alternatively copy desired DLLs from the package's `Mods` folder. S1API and TheRegulars remain installed.

See [the playtest checklist](docs/playtest.md) for live verification and [compatibility findings](docs/compatibility.md) for beta changes addressed. Compilation and startup checks do not establish gameplay correctness. Multiplayer peers should use matching versions and settings, particularly for custom item IDs and slot-machine bets.

Keep More Guns installed while its custom items or magazine trash exist in a save. Disabling its `Enabled` setting stops shop/input additions but retains definitions for loading existing saves.

## Personal use

This repository is for the owner's personal use. Permission to adapt the old mods was supplied by the owner; it is not a grant of redistribution rights. Game assemblies, third-party binaries, extracted weapon assets, decompiled reference source, saves and local configuration are excluded from Git. No public releases are intended.

Local build packages include the private weapon assets and must remain personal. See [provenance](docs/provenance.md) for original authors and references.
