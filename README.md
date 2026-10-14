# VICE

A modular mod suite for **Schedule I**, adding police and cartel systems, trading tools, weapons, home entertainment, HUD improvements, and LSD workshop gameplay.

## Features

| Module | Features |
| --- | --- |
| **Enforcement** | Police pursuits, raids, investigators, surveillance, patrols, and property-pressure reports through a phone contact. |
| **Syndicate** | Cartel events, ambushes, robberies, rival dealers, alliances, quests, and hostile difficulty settings. |
| **Commerce** | Trading assistance, optional negotiation and delivery automation, saved gas-mart shopping lists, and expanded warehouse access. |
| **Arsenal** | AK47, minigun, spare magazines, automatic firing, and configurable weapon tuning. |
| **Lifestyle** | Higher casino wager limits, placeable wall screens with image/video playback, and opt-in darts and pool prototypes. |
| **Interface** | Minimap, tracked-deal payouts, known-customer map portraits, and multiplayer compass labels. |
| **Substances** | LSD workshop production and integrated visual/audio consumption effects. |

All gameplay modules use **VICE.Core**, the shared library installed in `UserLibs`.

## Requirements

- Schedule I on the Steam **beta / IL2CPP** branch. Mono builds are not supported.
- **MelonLoader 0.7.3** and compatible IL2CPP **S1API** for Lifestyle.
- Matching VICE binaries and compatible S1API versions across multiplayer participants.

## Installation

1. Close the game and install the required loader and dependencies.
2. Download a suite package from [Releases](https://github.com/Axl47/ScheduleOne-Mods/releases).
3. Extract its contents into the Schedule I folder, beside `Schedule I.exe`. Gameplay DLLs belong in `Mods`; `VICE.Core.dll` belongs in `UserLibs`.
4. When upgrading, follow the included `VICE/README.md` to remove replaced legacy mods. Keep only one `VICE.Core.dll`, in `UserLibs`.
5. Launch the game. Missing configuration files are created automatically.

For exports built from source, use the installer to verify the package, back up replaced DLLs, and preserve settings and saves:

```powershell
./scripts/Install-VICE.ps1 -PackageDir <export-directory>
```

Keep modules that define custom items installed while those items remain in a save. Remove custom screens before uninstalling Lifestyle. Use a copied save when trying new features.

## Configuration and guides

Preferences are stored in `UserData/MelonPreferences.cfg`; some categories retain their legacy names for compatibility. Module data is stored under `UserData/VICE`. See the [user-data guide](docs/vice-user-data.md) for locations and migration details.

- [Police](docs/vice-enforcement.md), [cartel](docs/vice-syndicate.md), and [LSD workshop](docs/vice-substances.md)
- [Weapon tuning](docs/weapon-tuning.md) and [HUD features](docs/ui-enhancements.md)
- [TV playback and video sharing](docs/vice-lifestyle-video-sharing.md)
- [Darts and pool controls](docs/vice-lifestyle-home-games.md): experimental and disabled by default; enable `EnablePrototypes` in `VICE.Lifestyle.Amenities`. Two-player validation is still outstanding.
- [Optional YouTube playback](docs/vice-lifestyle-youtube.md): single-player development add-on requiring `VICE.YouTube.dll` and the separate TV companion download.
- [Compatibility](docs/compatibility.md) and [playtest checklist](docs/playtest.md)

## Building from source

Builds require .NET SDK 8 or newer and a local IL2CPP game installation with generated MelonLoader assemblies. Lifestyle also requires compatible S1API. Arsenal requires an authorized MoreGuns asset bundle, which is not included in this repository.

```powershell
./scripts/Build-VICE.ps1 -Modules Commerce,Lifestyle
./scripts/Build-VICE.ps1 -All
```

The builder checks the selected modules and Core, then creates an export directory and ZIP under `artifacts`. See the [build guide](docs/vice-build.md) for dependencies, module selection, and installation options.

## Credits

VICE includes adaptations of existing mods. Original authors' credits, licenses, and asset permissions are recorded in [provenance](docs/provenance.md) and `licenses`. Third-party assets and the optional TV companion retain their original terms; inclusion in the suite does not grant redistribution rights.
