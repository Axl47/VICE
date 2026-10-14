# Credits and asset permissions

VICE adapts existing mods and retains their original attribution. Renaming or packaging does not replace upstream copyright or grant redistribution rights.

| Source | Authors | Used by |
| --- | --- | --- |
| Warehouse Always Open | Elio | Commerce warehouse access |
| BetterPolice | Frostbyt3 / Babyhamsta | Police and hostile-cartel adjustments |
| Highballer / Deal Optimizer | zocke18r, SFox, Virtunerd | Commerce trading |
| MoreGuns and weapon assets | Voidane | Arsenal |
| NACops | XOWithSauce | Enforcement |
| Cartel Enforcer | XOWithSauce | Syndicate |

Recorded permission for Warehouse, BetterPolice, Highballer, and MoreGuns adaptations is limited to personal adaptation and does not authorize redistribution. The MoreGuns bundle is not committed.

## Licensed source

- NACops baseline: commit `97cedcd9f06e16bf16f9460493b87bc2fe460560`. The MIT notice remains in `src/VICE.Enforcement/LICENSE.NACops` and packages containing Enforcement.
- Cartel Enforcer baseline: commit `4b674404fffe5109b46b990387af46407917d223`. The MIT notice remains in `src/VICE.Syndicate/LICENSE.CartelEnforcer`, embedded resources, and packages containing Syndicate.

VICE adaptations are by Axl47. Original notices remain in source, module resources, and package license directories.

## Assets and dependencies

Arsenal embeds a locally supplied authorized weapon bundle. Its SHA-256 is `B9680C989C9DC4EDCAA6B7CC6B8607BB865BDEA870328E49794A7E052C7EC444`; the extracted input remains outside Git.

The optional YouTube adapter uses the pinned Build-A-Base TV companion and preserves its third-party licenses. Archive inspection has not established author permission to redistribute it. See the [YouTube guide](vice-lifestyle-youtube.md).

Interface uses native UI data/assets at runtime. Game assemblies, third-party binaries, local configurations, saves, and build packages are excluded from source control. Packaging automation does not expand permissions.
