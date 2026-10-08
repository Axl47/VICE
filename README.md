# VICE

VICE is a personal mod suite for **Schedule I**, organized into modules that each own a part of the game. Related features share one DLL, while their code and settings remain separate inside that module.

The suite targets the Steam **beta / IL2CPP** branch, using the locally installed game and MelonLoader assemblies. The current police and syndicate ports target **0.4.7f11**, with Unity **2022.3.62f2**, MelonLoader **0.7.3**, and .NET **6**, x64. Builds require a .NET SDK 8 or newer. There is no Mono build.

This repository is for private use. VICE includes adaptations of existing mods and privately supplied assets; the suite name does not replace the original authors' attribution or grant redistribution rights.

## Modules

| Module | Responsibility | Current features / source |
| --- | --- | --- |
| **VICE.Core** | Shared infrastructure | Patching, preferences, settings migration, logging, event cleanup, default files, and predecessor conflict detection. A library in `UserLibs`, with no gameplay entry point. |
| **VICE.Enforcement** | Police and law enforcement | NACops combined with Better Police's police behavior: pursuit coordination, police difficulty, raids, investigators, surveillance, and patrols. |
| **VICE.Syndicate** | Cartel and organized crime | Cartel events, hostile difficulty, ambushes, robberies, rival dealers, alliances, and quests. This is the module previously called Cartel. |
| **VICE.Commerce** | Buying, selling, and business transactions | Highballer negotiations and delivery automation, Gas Purchases shopping lists, and Warehouse access/catalogue changes. |
| **VICE.Substances** | Drugs, production, and consumption effects | The destination for LSD Gameplay and its visual/audio treatment, with room for future drugs. **Reserved; the LSD projects have not been migrated.** |
| **VICE.Arsenal** | Weapons and ammunition | More Guns: AK47, minigun, magazines, automatic firing, weapon presentation, and tuning. |
| **VICE.Lifestyle** | Entertainment and home amenities | Casino wager limits and placeable TV screens with images, local video, direct media URLs, and Relay integration. |
| **VICE.Interface** | General HUD, maps, and information display | UI Enhancements: minimap, tracked-deal payouts, known-customer portraits, and multiplayer compass labels. |

The intended suite has eight modules. Substances is an architectural destination, not an existing `VICE.Substances.dll`; its current builds remain `Personal.LSDGameplay.dll` and `Personal.LSDPreview.dll` while that work continues.

## Why these boundaries

Commerce owns transaction behavior, including its shop and negotiation controls. Interface owns general information displays, such as the minimap and agreed deal payouts. A feature can display UI without belonging to Interface.

Enforcement owns police behavior; Syndicate owns cartel behavior. Arsenal supplies weapons regardless of who uses them. Adding a weapon does not require merging it into either faction module.

Substances owns drug content and effects. Drug classes, such as psychedelics or stimulants, belong inside that module until their size or independent maintenance needs justify a separate module. The finished consumption effects belong with their drug; manual previews and shader diagnostics remain development tools.

Lifestyle owns leisure activities and home amenities. Casino stays here because the feature changes gambling; Commerce owns the buying and selling systems used across the business.

Core contains reusable services needed by modules. Gameplay hooks, custom items, and feature-specific rules stay in their owning module. Adding a feature normally means adding a feature folder and settings to an existing module, rather than creating another installed DLL.

## Commerce

`VICE.Commerce.dll` replaces `Personal.Highballer.dll`, `Personal.GasPurchases.dll`, and `Personal.Warehouse.dll`.

- **Trading:** estimates counteroffer success and searches for the highest acceptable price at the configured threshold, default 98%. Exact packaged delivery automation uses the native handover checks. Automatic counteroffers, contract scheduling, and quantity increases remain optional and default off; automatic handover defaults on. Custom S1API customers and special customers are skipped by default.
- **Purchasing:** saves up to 32 named gas-mart shopping lists and remembers the last successful purchase. Load a list or use **Repeat last**, then complete the normal checkout. Availability, stock, current prices, money, and inventory capacity still apply. An unavailable list preserves the current cart.
- **Warehouse:** keeps warehouse access available at all hours and before the normal unlock rank by default. Oscar sells the game's storage racks, closets, lockers, and cabinets at their existing prices. Extra existing storage item IDs can be configured.

Preferences retain the `PersonalHighballer`, `PersonalGasPurchases`, and `PersonalWarehouse` categories in `UserData/MelonPreferences.cfg`. Commerce 0.1.1 stores shopping lists in `UserData/VICE/Commerce/Purchasing/orders.json` and automatically moves existing `PersonalGasPurchases` data there. See the [user-data migration guide](docs/vice-user-data.md).

See the [saved-purchase playtest](docs/playtest.md), [compatibility notes](docs/compatibility.md).

## Arsenal

`VICE.Arsenal.dll` replaces `Personal.MoreGuns.dll`.

The arms dealer sells the AK47 ($15,000, 30 rounds) and minigun ($75,000, 400 rounds), plus matching spare magazines. Automatic firing, magazine reloads, animated minigun spin-up, and tuned weapon presentation retain the personal mod's behavior. Hold aim to spin up the minigun; reloading, switching weapons, or releasing aim resets the delay. Rank restrictions default off.

| Default tuning | AK47 | Minigun |
| --- | --- | --- |
| Spread multiplier | 0.7 | 0.5 |
| Bullet knockback multiplier | 0.65 | 0.65 |
| Sound volume multiplier | 0.4 | 0.25 |
| Camera shake multiplier | 0.5 | 0.5 |

Preferences remain under `PersonalMoreGuns`, `PersonalMoreGuns_ak47`, and `PersonalMoreGuns_minigun`. Existing gun, magazine, and trash IDs remain unchanged. Keep Arsenal installed while these items exist in a save; disabling its `Enabled` setting stops shop/input additions while retaining item definitions for loading.

The private weapon bundle is embedded in Arsenal using the existing resource identifier. It is not committed to Git. See the [weapon tuning notes](docs/weapon-tuning.md) and [asset provenance](docs/provenance.md).

## Lifestyle

`VICE.Lifestyle.dll` replaces `Personal.Casino.dll` and `Personal.TV.dll`. It requires the installed IL2CPP **S1API** furniture and lifecycle APIs; the local reference is S1API (Forked by Bars) **3.2.1-beta.7**.

**Casino** raises the Blackjack and Ride the Bus wager limits to $100,000 by default. Available cash and native payouts still apply. Slot machines retain their original behavior. Preferences remain under `PersonalCasino`.

**Television** adds a $450 Wall Screen at Herbert's Bleuball's Boutique. Place it on a wall, interact with it, and select an image, direct HTTP(S) video URL, Relay episode, or local MP4. Playback controls include pause, seeking, volume, favorites, and a larger viewing mode. Local MP4 selections share metadata first; Play transfers the selected file through the host and waits for connected peers. See the [video sharing guide](docs/vice-lifestyle-video-sharing.md).

Lifestyle 0.1.2 stores local images, videos and caches under `UserData/VICE/Lifestyle/Television`, automatically moving existing `PersonalTV` data on initialization. TV preferences remain under `PersonalTV`, including the local Relay configuration. Screen state and images remain in each host save's `Modded/PersonalTV` folder. Playback grants and Relay tokens are excluded from saves. Existing furniture IDs and saved-media formats remain unchanged.

Every multiplayer participant needs matching Lifestyle and compatible S1API versions. Remove custom screens from saves before uninstalling their item definition. Live Relay and two-player synchronization require their own playtests; automated tests cover the media-state and Relay contracts.

## Interface

`VICE.Interface.dll` replaces `Personal.UIEnhancements.dll`.

The square minimap follows your player with north up and a rotating facing arrow. It includes native map artwork/icons and known-customer portraits. It hides while the phone or pause menu is open. Defaults are 240 HUD units wide, 160 world units across, and 1.55 icon scale.

Tracked deals show the agreed total payment and average price per unit. Known-customer portraits follow unlocked customers on the full map, with indoor customers represented at their entrance. Multiplayer compass markers show player names in the native label position.

Preferences remain under `PersonalUIEnhancements`. Minimap, payout labels, customer portraits, and multiplayer markers can be disabled separately. Each player installs Interface to see its displays. See the [UI implementation notes](docs/ui-enhancements.md) and [playtest checklist](docs/playtest.md).

## Enforcement, Syndicate, and Substances

These modules have separate development and build workflows. The Commerce/Arsenal/Lifestyle/Interface migration does not change their gameplay code.

Build Enforcement with `scripts/Build-VICEEnforcement.ps1` and install with `scripts/Install-VICEEnforcement.ps1`. It combines the police systems and retains the original personal police settings through Core's preference migration. Cartel changes belong to Syndicate. See the [Enforcement guide](docs/vice-enforcement.md).

Build Syndicate with `scripts/Build-VICESyndicate.ps1` and install with `scripts/Install-VICESyndicate.ps1`. It owns cartel events and the personal hostile-cartel difficulty settings. See the [Syndicate guide](docs/vice-syndicate.md).

Substances remains under active development in the existing LSD projects. `scripts/Build-LSDGameplay.ps1` builds the workshop gameplay and its companion preview; `scripts/Build-LSDPreview.ps1` builds the preview independently. Keep using these projects until their own migration is complete. See the [workshop guide](docs/lsd-workshop.md), [gameplay design](docs/lsd-gameplay-design.md), and [effects preview guide](docs/lsd-preview.md).

## Build and package the four migrated modules

Launch the IL2CPP beta with MelonLoader once to generate `MelonLoader/Il2CppAssemblies`. Install compatible IL2CPP S1API before building Lifestyle. From PowerShell in this repository:

```powershell
./scripts/Build-VICEModules.ps1
```

The script builds Commerce, Arsenal, Lifestyle, Interface, and their Core dependency with warnings treated as errors. It runs transaction, shopping-list, weapon spin-up, TV state/Relay, module registration/lifecycle, native patch-address, and installer checks. It creates `artifacts/VICE.Modules-0.1.0.zip`, containing:

```text
Mods/
  VICE.Commerce.dll
  VICE.Arsenal.dll
  VICE.Lifestyle.dll
  VICE.Interface.dll
UserLibs/
  VICE.Core.dll
```

The package also includes this README, the installer, a SHA-256 manifest, and the feature guides. Enforcement, Syndicate, and the current LSD DLLs are not bundled by this script. It does not install anything unless `-Install` is supplied.

Use `-GameDir`, `-OriginalMoreGuns`, `-DotnetPath`, or `-NugetConfig` for local overrides. The bundled local SDK is used when present; otherwise the script uses `dotnet`. `Game.local.props` can set the game reference path for direct project builds.

Arsenal requires the owner's authorized original MoreGuns asset bundle. If the extracted bundle already exists in `.research/assets`, the script reuses it. Otherwise it calls `scripts/Prepare-Assets.ps1` with `-OriginalMoreGuns`. No private assets or game assemblies are downloaded.

`scripts/Build.ps1` and `scripts/Package.ps1` forward to the four-module VICE workflow. For a single module, build its project under `src/VICE.<Module>` directly. The solution retains the other modules' projects and development tests.

## Install and preserve existing data

Close the game, build the package, then run:

```powershell
./scripts/Install-VICEModules.ps1
```

For selected modules only:

```powershell
./scripts/Install-VICEModules.ps1 -Modules Commerce,Interface
```

The installer checks packaged hashes before writing, backs up the selected predecessors and existing replacements outside `Mods`, installs the selected VICE DLLs, and removes their predecessor DLLs after verification. It also installs the shared Core in `UserLibs` and removes a duplicate Core from `Mods`. A failed installation restores the previous DLLs.

Enforcement, Syndicate, Better Police, LSD Gameplay, LSD Preview, and S1API are outside this installer's replacement list. Preferences, shopping lists, local media, and game saves are preserved. `Personal.TVProof.dll` is removed when installing Lifestyle because its test registration conflicts with the TV feature.

Keep **one current `VICE.Core.dll` in `UserLibs`** for the suite. Rebuild Core-dependent packages from the current workspace before installing them; an older package can carry an older Core. The migrated modules use Core's new predecessor compatibility check.

The runtime also rejects loaded predecessor assemblies before applying replacement hooks. Install a feature's old DLL or its VICE replacement, with matching versions on multiplayer peers.

## Source layout and shared services

```text
src/VICE.Core/                  Shared library
src/VICE.Enforcement/           Police
src/VICE.Syndicate/             Cartel and organized crime
src/VICE.Commerce/              Trading, Purchasing, Warehouse
src/VICE.Arsenal/               Guns
src/VICE.Lifestyle/             Casino, Television
src/VICE.Interface/             Hud
src/LSDGameplay/, LSDPreview/   Current Substances development
```

Each migrated gameplay DLL has one MelonLoader entry point. Its feature classes are ordinary classes, with the module forwarding the lifecycle callbacks they need. They do not instantiate hidden Melon mods or depend on the predecessor DLLs.

All four modules reference Core's `ModSupport` for patches, preferences, and warnings, instead of embedding a copy of `src/Shared/ModSupport.cs`. `SubscriptionScope` owns gas-mart completion listeners and TV lifecycle subscriptions and releases them during cleanup. `ModuleCompatibility` detects loaded predecessors before initialization. These shared helpers are available to future modules; see the [Core contract](src/VICE.Core/README.md) and [module migration notes](docs/vice-module-migration.md).

## Validation and personal use

Builds and automated checks establish compilation, data contracts, callback forwarding, package integrity, and inspected native patch targets. They do not establish live gameplay correctness. After installation, use a copied save to check shop access, remembered purchases, negotiations/handover, weapon registration/reloads, TV persistence/playback, and HUD cleanup across loading and leaving saves. Multiplayer and live Relay need separate playtests.

Game assemblies, third-party binaries, extracted weapon assets, local configurations, saves, and build packages stay outside Git. Original notices remain with the imported modules and in the [provenance documentation](docs/provenance.md). Packages containing private assets remain personal; no public release is intended.
