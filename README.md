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

Starting with Lifestyle 0.1.8, [single-player YouTube playback](docs/vice-lifestyle-youtube.md) is an optional **VICE YouTube development add-on**. The base Lifestyle package includes no browser player, companion installer or YouTube development guide. Images, MP4s, direct media URLs and Relay work independently. Install `VICE.YouTube.dll` and the separately supplied Build-A-Base 0.4.26 TV companion to reveal the YouTube option, captions and chapters. Neither release bundles the roughly 489 MiB companion. Saved YouTube IDs and favorites are preserved when the add-on is absent.

Lifestyle 0.1.2 stores local images, videos and caches under `UserData/VICE/Lifestyle/Television`, automatically moving existing `PersonalTV` data on initialization. TV preferences remain under `PersonalTV`, including the local Relay configuration. Screen state and images remain in each host save's `Modded/PersonalTV` folder. Playback grants and Relay tokens are excluded from saves. Existing furniture IDs and saved-media formats remain unchanged.

Lifestyle 0.1.5 and LSD Workshop 0.2.10 retain the IL2CPP packet fix and restore custom furniture visuals when a guest receives the native donor prefab. Initial guest workshop state requests retry until acknowledged. TV file transfers release failed attempts immediately, identify retries separately and retain packets through transient writer failures. Single-player MP4 playback bypasses the 512 MiB sharing cap. Workshop meshes are embedded in its DLL, and the TV model is generated by Lifestyle; no separate model pack is required. Every multiplayer participant needs matching mod binaries and compatible S1API versions. Remove custom screens from saves before uninstalling their item definition. Live Relay and two-player synchronization require their own playtests; automated tests cover the packet payload lifecycle, media-state and Relay contracts.

Interface 0.1.1 reduces shared HUD overhead with cached map membership and bounded visual/customer refreshes while keeping player motion smooth. Enforcement 0.1.3 removes guests' scene search on every frame. Syndicate 0.1.2 runs persistent save data and event setup only on the host, avoiding the guest `LoadStolenItems` initialization crash. Restart every participant with matching updated DLLs.

## Interface

`VICE.Interface.dll` replaces `Personal.UIEnhancements.dll`.

The square minimap follows your player with north up and a rotating facing arrow. It includes native map artwork/icons and known-customer portraits. It hides while the phone or pause menu is open. Defaults are 240 HUD units wide, 160 world units across, and 1.55 icon scale.

Tracked deals show the agreed total payment and average price per unit. Known-customer portraits follow unlocked customers on the full map, with indoor customers represented at their entrance. Multiplayer compass markers show player names in the native label position.

Preferences remain under `PersonalUIEnhancements`. Minimap, payout labels, customer portraits, and multiplayer markers can be disabled separately. Each player installs Interface to see its displays. See the [UI implementation notes](docs/ui-enhancements.md) and [playtest checklist](docs/playtest.md).

## Enforcement, Syndicate, and Substances

Enforcement and Syndicate share the VICE build/export workflow. Substances remains in its existing development projects.

Build Enforcement with `scripts/Build-VICE.ps1 -Modules Enforcement`. It combines the police systems and retains the original personal police settings through Core's preference migration. Cartel changes belong to Syndicate. See the [Enforcement guide](docs/vice-enforcement.md).

Build Syndicate with `scripts/Build-VICE.ps1 -Modules Syndicate`. It owns cartel events and the personal hostile-cartel difficulty settings. See the [Syndicate guide](docs/vice-syndicate.md).

Substances remains under active development in the existing LSD projects. `scripts/Build-LSDGameplay.ps1` builds the workshop gameplay and its companion preview; `scripts/Build-LSDPreview.ps1` builds the preview independently. Keep using these projects until their own migration is complete. See the [workshop guide](docs/lsd-workshop.md), [gameplay design](docs/lsd-gameplay-design.md), and [effects preview guide](docs/lsd-preview.md).

## Build and export VICE

Launch the IL2CPP beta with MelonLoader once to generate `MelonLoader/Il2CppAssemblies`. Install compatible IL2CPP S1API before building Lifestyle. From PowerShell in this repository:

```powershell
./scripts/Build-VICE.ps1 -Modules Commerce,Lifestyle
./scripts/Build-VICE.ps1 -Modules Lifestyle
./scripts/Build-VICE.ps1 -AddOns YouTube  # Separate optional add-on; requires installed Lifestyle 0.1.8+
./scripts/Build-VICE.ps1          # Core only
./scripts/Build-VICE.ps1 -All     # All six implemented gameplay modules
```

Every invocation builds and checks the current Core, then builds and checks exactly the selected modules with warnings treated as errors. Each export uses a fresh directory under `artifacts`, so old module DLLs cannot leak into a smaller update. Core + Commerce + Lifestyle exports contain:

```text
Mods/
  VICE.Commerce.dll
  VICE.Lifestyle.dll
UserLibs/
  VICE.Core.dll
```

The result prints its directory and ZIP paths. Every package includes one `UserLibs/VICE.Core.dll`, version/selection metadata, hashes, an installer and guides. Enforcement and Syndicate include their defaults and licenses when selected. The current LSD projects are not VICE modules yet. Add `-Install` to install the export while the game is closed.

Use `-GameDir`, `-OriginalMoreGuns`, `-DotnetPath`, `-NugetConfig` or `-OutputDirectory` for local overrides. The bundled local SDK is used when present; otherwise the script uses `dotnet`. `Game.local.props` can set the game reference path for direct project builds.

Arsenal requires the owner's authorized original MoreGuns asset bundle. If the extracted bundle already exists in `.research/assets`, the script reuses it. Otherwise it calls `scripts/Prepare-Assets.ps1` with `-OriginalMoreGuns`. No private assets or game assemblies are downloaded.

The old module build scripts forward to `Build-VICE.ps1`; they no longer contain separate compilation or export logic. See the [unified build guide](docs/vice-build.md) for selections, checks and installation. Direct project builds compile DLLs without creating an update export.

## Install and preserve existing data

Close the game, build the package, then run:

```powershell
./scripts/Install-VICE.ps1 -PackageDir <printed-export-directory>
```

For selected modules only:

```powershell
./scripts/Install-VICE.ps1 -PackageDir <printed-export-directory> -Modules Lifestyle
```

The installer checks packaged hashes before writing, backs up the selected predecessors and existing replacements outside `Mods`, installs the selected VICE DLLs, and removes their predecessor DLLs after verification. It also installs the shared Core in `UserLibs` and removes a duplicate Core from `Mods`. A failed installation restores the previous DLLs.

Only selected modules and their predecessors are replaced. Enforcement also replaces its legacy police DLLs, while Syndicate rejects a still-installed original Cartel Enforcer. Existing JSON settings are preserved and missing selected-module defaults are seeded. Preferences, shopping lists, local media and game saves are preserved. `Personal.TVProof.dll` is removed when installing Lifestyle because its test registration conflicts with the TV feature.

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
