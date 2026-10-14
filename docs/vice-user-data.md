# User data and saves

VICE creates missing configuration files on first launch and preserves existing settings. Paths below are relative to the Schedule I folder.

| Data | Location |
| --- | --- |
| Preferences | `UserData/MelonPreferences.cfg` |
| TV images and original videos | `UserData/VICE/Lifestyle/Television/Images` and `Videos` |
| TV caches | `UserData/VICE/Lifestyle/Television/Cache` |
| Gas-mart shopping lists | `UserData/VICE/Commerce/Purchasing/orders.json` |
| Police configuration, patrols, heat, and report history | `UserData/VICE/Enforcement` |
| Cartel configuration and persistent records | `UserData/VICE/Syndicate` |

Add images and MP4s to the TV folders above. Downloaded video caches are temporary; original files are retained. Some preference categories, item IDs, and save folders retain legacy names for compatibility. Use the category named in each guide; renaming it will not migrate settings.

## Save-specific content

Paths below are inside the loaded save folder:

- Wall Screen state and images: `Modded/PersonalTV`.
- Workshop artwork, equipment, batches, orders, and tutorial: `Modded/OffRegister/Workshop.json`.
- Poker settlements: `Modded/VICE/Poker/settlement.json`.

Back up the entire save while the game is closed. Poker records and native player inventories must be backed up or restored together. Keep corresponding modules installed while custom items or unsettled funds remain.

## Upgrading

Core moves supported legacy data into module folders before creating defaults. Existing destination files take precedence; conflicting files are preserved under `UserData/VICE/MigrationConflicts`. Interrupted moves can resume next launch. Blocked paths stop migration and are reported in the log.

Install the Core supplied with your update, keeping one copy in `UserLibs`. The [installer](vice-build.md) preserves preferences, media, shopping lists, and saves. Original NACops heat and Cartel Enforcer records are not imported automatically.
