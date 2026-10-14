# Compatibility and upgrades

VICE targets Schedule I's Steam **beta / IL2CPP** branch with **MelonLoader 0.7.3**. Mono builds are unsupported. Lifestyle and Substances require compatible IL2CPP S1API. Game updates can change native APIs; use packages checked against your game build.

## Installation and conflicts

- Install gameplay DLLs in `Mods` and one `VICE.Core.dll` in `UserLibs`.
- Use the Core supplied with your update. Close the game before installation and restart every multiplayer participant afterward.
- Remove replaced standalone mods using the package's upgrade instructions. Duplicate implementations can conflict; Core prevents affected modules from initializing when detected.
- Remove original Cartel Enforcer before installing Syndicate. Enforcement owns police; Syndicate owns cartel behavior.
- The optional YouTube adapter needs its separate pinned browser companion and supports single-player only.

Use the [installer](vice-build.md) for source exports to verify hashes, back up replaced DLLs, and preserve unselected modules, settings, and saves. See [user data](vice-user-data.md) for migration and backup locations.

## Feature behavior

| Feature | Compatibility details |
| --- | --- |
| Commerce trading | Custom S1API and special customers are skipped by default. Multi-product counteroffers remain manual. Automatic handover requires a validated exact packaging plan. |
| Gas-mart lists | Central and West only. Loading restores a cart without purchasing; unlocks, stock, prices, money, and capacity still apply. |
| Casino | Raised limits apply to Blackjack and Ride the Bus. Native validation/payouts remain; slots keep original behavior. |
| Arsenal | Keep definitions installed while custom weapons or magazines remain. Audio follows master/effects volume. |
| Interface | Local displays; each player installs it independently. Transactions/progression are unchanged. |
| Television | Shared local videos are capped at 512 MiB; single-player local playback has no sharing cap. |
| Substances | Workshop IDs/save formats are retained. Keep the module and S1API while workshop content remains. |
| Poker | Back up/restore inventories and settlement records together. Cash out before uninstalling. |

## Multiplayer and support

Use matching VICE binaries and compatible S1API across peers. Police synchronization, home games, TV sharing, workshop behavior, and poker still have live checks described in their guides and the [playtest checklist](playtest.md).

Report your game branch/build, module/dependency versions, reproduction steps, and relevant `MelonLoader/Latest.log` lines. Try updates and gameplay checks on a copied save.
