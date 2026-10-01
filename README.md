# Schedule One personal mods

Private monorepo containing six independently installable MelonLoader mods for the Steam **beta / IL2CPP** branch. Current target: **Schedule I 0.4.7f7**, **Unity 2022.3.62f2**, **MelonLoader 0.7.3**, x64 / .NET 6. The installed **S1API (Forked by Bars) 3.2.1-beta.7** and **TheRegulars 1.0.0** are retained. Highballer's optional custom-NPC guard also supports the previously inspected S1API 2.5.0 field layout.

Features and defaults:

| Mod | Purpose |
| --- | --- |
| `Personal.Warehouse.dll` | Warehouse access at all hours, including before the normal unlock rank. Oscar sells small/medium/large racks, small/medium/large/**huge storage closets**, lockers and cabinets at their existing prices. Additional existing storage IDs can be configured. |
| `Personal.BetterPolice.dll` | Pursuit speed increases from 1.3× to 2× with distance, wider and longer vision, shorter notice time, faster arrests, stronger aim, reachable interception destinations and nearby active backup officers. |
| `Personal.Highballer.dll` | Estimated counteroffer success and highest acceptable price at a 98% threshold. Exact packaged delivery planning with native handover validation. Optional automatic counteroffers, scheduling and quantity increases default off. S1API custom NPCs and special customers are skipped by default. |
| `Personal.Casino.dll` | Blackjack and Ride the Bus maximum wagers of $100,000; slot wager ladder multiplied by 100. Bets remain subject to available money and normal game payouts. |
| `Personal.MoreGuns.dll` | AK47 ($15,000, 30 rounds) and minigun ($75,000, 400 rounds), with matching spare magazines at the arms dealer. Automatic fire, animated minigun spin-up, corrected magazine reloads, quieter sound through master/effects settings, tighter spread, 35% less bullet knockback and softer firing shake. Rank restrictions default off. |
| `Personal.GasPurchases.dll` | Named shopping lists and **Repeat last** at both gas-marts. Restores exact quantities at current prices, checks availability/stock, and uses normal checkout. Remembers the last successful purchase across restarts. |

Configuration is stored by MelonLoader in `UserData/MelonPreferences.cfg` under the `PersonalWarehouse`, `PersonalBetterPolice`, `PersonalHighballer`, `PersonalCasino`, `PersonalMoreGuns`, `PersonalMoreGuns_ak47`, `PersonalMoreGuns_minigun` and `PersonalGasPurchases` categories. Restart after editing settings. Old mod config files are not imported.

## Weapon tuning

More Guns 0.2.0 supplies these new defaults in each weapon's category. Existing gun and magazine IDs remain compatible with saved items. Reloading requires a matching spare magazine; damage and fire rate retain their previous defaults.

| Setting | AK47 | Minigun |
| --- | --- | --- |
| `SpreadMultiplier` | `0.7` (30% less spread) | `0.5` (50% less spread) |
| `ImpactForceMultiplier` | `0.65` (35% less knockback) | `0.65` (35% less knockback) |
| `SoundVolumeMultiplier` | `0.4` | `0.25` |
| `CameraShakeMultiplier` | `0.5` | `0.5` |

Volume also follows the game's master and effects sliders, including mute. Camera shake uses a short, softer native shake for these guns; `0` disables their firing shake, while `1` restores the original jolt. Hold aim to spin up the minigun; releasing aim, reloading or switching weapons resets its delay. See [the weapon repair notes](docs/weapon-tuning.md).

## Saved gas-mart purchases

Fill the gas-mart cart once, click **Saved orders** in the shop's upper right, enter a name and click **Save current cart**. Later select that order and click **Load order**, or use **Repeat last** to restore the last purchase that completed successfully. Loading replaces the cart; review the total and use the game's checkout button.

Up to 32 named orders persist in `UserData/PersonalGasPurchases/orders.json`, alongside a separate last-purchase record. Lists are shared between Central and West gas-marts and local saves. Missing, locked or insufficient-stock items reject the whole list and preserve the current cart. Current prices, available money and inventory capacity apply. Saving the same name updates its list; deleting a named list keeps last-purchase history. See [the saved-purchase playtest](docs/playtest.md).

## Build

Install a .NET SDK 8 or newer and launch the beta with MelonLoader once to generate `MelonLoader/Il2CppAssemblies`. Build references come directly from the installation. From PowerShell in this repo:

```powershell
./scripts/Build.ps1
```

Use `-GameDir`, `-OriginalMoreGuns` or `-DotnetPath` to override the installation, authorized original `MoreGuns.dll`, or SDK executable. `-NugetConfig` supports a local/offline package feed. `Game.local.props` can also set `GameDir` for direct IDE builds.

The build extracts the original weapon bundle into ignored `.research/assets/`, compiles the six DLLs, runs the algorithm, saved-purchase and weapon spin-up checks and creates `artifacts/PersonalMods-0.2.1.zip`. More Guns requires the owner's original bundle; the other five projects can be built separately without it.

## Install and verify

The 0.2.1 package includes **More Guns 0.2.0** and the startup corrections in **Better Police, Warehouse and Casino 0.1.1**. If the 0.2.0 package is already installed and working, replace only `Personal.MoreGuns.dll`. See [the crash investigation](docs/startup-crash.md) for the earlier startup fix.

Close the game, then run:

```powershell
./scripts/Install.ps1
```

Installation copies only the six personal DLLs into `Mods`, backs up any previous personal versions in `artifacts/install-backups/`, and verifies their hashes. It refuses to install alongside conflicting old versions. Alternatively copy desired DLLs from the package's `Mods` folder. S1API and TheRegulars remain installed.

See [the playtest checklist](docs/playtest.md) for live verification and [compatibility findings](docs/compatibility.md) for beta changes addressed. Compilation and startup checks do not establish gameplay correctness. Multiplayer peers should use matching versions and settings, particularly for custom item IDs and slot-machine bets.

Keep More Guns installed while its custom items or magazine trash exist in a save. Disabling its `Enabled` setting stops shop/input additions but retains definitions for loading existing saves.

## Personal use

This repository is for the owner's personal use. Permission to adapt the old mods was supplied by the owner; it is not a grant of redistribution rights. Game assemblies, third-party binaries, extracted weapon assets, decompiled reference source, saves and local configuration are excluded from Git. No public releases are intended.

Local build packages include the private weapon assets and must remain personal. See [provenance](docs/provenance.md) for original authors and references.
