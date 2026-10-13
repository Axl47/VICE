# Lifestyle home-games artwork integration — 0.2.1

The 0.2.2 [world-play presentation candidate](vice-home-games-world-play.md) replaces the temporary play surface, adds model inventory thumbnails and tunes pool-only highlights. The accepted asset contract below remains intact; current native-test permission/access requirements are recorded in that candidate guide.

Lifestyle now embeds the accepted artwork from [PR #7](https://github.com/Axl47/ScheduleOne-Mods/pull/7). The reviewed art is commit `b6b148e0c2bdf171f8eab9da5b4edddcbd1d9a18`; the imported source/evidence head is `a885844e925e7cc415c7fbb0c0f594297cc4d7d7`. The original Blender, four production FBXs and three atlases retain their accepted SHA-256 values. Historical probes and review renders are source evidence and are not bundled into the mod.

## Build and use

The ordinary `Build-VICE.ps1 -Modules Lifestyle` workflow verifies the accepted artwork and its generated bundle before compiling. On a clean checkout, or when build inputs change, `Build-VICEHomeGamesAssets.ps1` uses the installed Unity **2022.3.62f2** editor to build in the isolated `.tools/vice-home-games-unity` project. It does not modify the LSD Unity project, installed game or saves. To rebuild explicitly:

```powershell
./scripts/Build-VICEHomeGamesAssets.ps1 -Rebuild
./scripts/Build-VICE.ps1 -Modules Lifestyle
```

Use `-UnityPath` on the asset build script if that editor is installed elsewhere. Unity must have a working local license; a sandbox may need access to the editor's licensing client. Generated `src/VICE.Lifestyle/Assets/homegames.bundle` and its hash contract are ignored by Git. Builds reuse a current verified bundle. The DLL embeds the bundle as `VICE.Lifestyle.HomeGames.bundle`, so users do not install loose FBXs, textures or a model DLL. Direct project builds without a generated bundle retain the primitive fallback; module export checks require the embedded resource.

Existing shop access is still opt-in: `EnablePrototypes=true` under `VICE.Lifestyle.Amenities`. `UseModelArtwork=true` is the default. Set it false and restart to compare primitives. If the embedded bundle, named prefab, atlas or supported native shader is unavailable, the mod logs the failure once and uses primitives for remaining visuals. Furniture IDs, prices, wall/grid placement, game rules, scoring coordinates, commands and network state are unchanged. The play surface remains a temporary 2D prototype panel; authored models replace world furniture and accessories.

## Adapter contract

`HomeGameAssets` loads cached local prefab templates and binds each canonical material name to one shared native **URP/Lit** material. It uses the provided base-colour atlases, white tint, opaque surface, ordinary back-face culling and the documented metallic/smoothness scalars. `WireMetal` is shared across board and darts; source aliases `MountMetal` and `DartMetal` map to that same object. The editor bundle contains Standard material assets as portable texture/scalar carriers; the runtime explicitly replaces their shaders with the live game's URP/Lit shader. Editor Standard rendering is not evidence of live URP appearance.

The editor preserves FBX hierarchy, imports original normals/UVs, disables collider/animation generation, and remaps both darts FBXs to the same material assets. It creates 21 static/dynamic prefabs: table, board, cue, rack, dart and sixteen balls. Runtime instances have no Rigidbody or artwork colliders. Native furniture bounds/interaction and the host's analytical circle/pocket/scoring logic remain owned by the existing code.

- **Darts:** the accepted board has visible 6 at root −X and 11 at root +X. `HomeGameVisualContract.Dart` maps analytical `(LastX, LastY)` to visual `(-LastX, LastY, 0.055)` only when the authored board is present. Scoring, panel input and packets keep the original X basis. Each dart mesh sits under the existing centre handle with a −0.08 m Z child offset; the handle is at Z 0.135, placing the authored tip exactly on Z 0.055. Primitive boards retain their original X mapping. Do not reflect the furniture root or change scoring.
- **Cue:** the authored root is the tip and extends along −Z. Its tip is placed behind the cue ball by `0.035 + clamp(chargeSeconds,0,1.5) × 0.08` m. The primitive centre-pivot cue uses an additional 0.725 m centre offset. Rotation and charge state still come from the authoritative game state; artwork does not strike balls.
- **Balls:** exact named ball meshes retain centre pivots, diameter 0.05715 m and identity scale. Code positions them at Y 0.808575 on the unchanged 0.78 m cloth. Guests display host snapshots through the existing presentation path.
- **Rack:** a decorative prop at `(0,0.78,-0.30)` is shown only while idle and hidden during matches. The accepted 0.30 m rack does not fit the existing fifteen-ball layout. It is not resized and never arranges balls.
- **Table:** S1API still supplies the `(0.50,0,1.25)` grid visual offset and the 3 × 6 footprint. The model root does not include that offset. Pocket signed locations, cloth extents and height retain the accepted contract.

The [asset README](https://github.com/Axl47/ScheduleOne-Mods/blob/a885844e925e7cc415c7fbb0c0f594297cc4d7d7/assets/VICEHomeGames/README.md) is the final artwork contract. The [original model-worker prompt](vice-amenities-model-worker-prompt.md) remains available with a note identifying the accepted coordinate clarification.

## Validation and remaining checks

The isolated Unity build performs a new import and reloads the built bundle. Checks cover signed pockets, metre scale, hierarchy, exact cloth/ball/cue/board bounds, tip/grip origins, absence of physics components, atlas retention, actual shared material identities across both darts imports and the bundle, and cue/dart wrapper transforms. The generated report is `artifacts/vice-home-games-import.json`; this is editor validation, not native-game testing. The accepted evidence replay also passes its 183 assertions without altering artwork.

Pure .NET tests additionally pin six/eleven visual mapping while preserving analytical scoring, primitive fallback coordinates, cue tip-versus-centre separation, cloth/ball height and canonical material settings. The existing full 301, pool simulation, turn, delivery and cleanup suites still apply. Export metadata checks require the embedded artwork resource; native IL2CPP compilation and patch-address checks use the installed beta assemblies.

Recorded on 2026-10-13: 342 new Unity editor/import/bundle assertions; 183 accepted-evidence replay assertions; 228 amenity assertions and 378 total Lifestyle assertions; 55 Core assertions; 49 module metadata assertions; five native patch-address declarations; hashed-export validation; and 13 installer assertions in an isolated fixture. The build completed with zero warnings/errors. The editor uses the exact game Unity version but is a separate process/project, not an instance of Schedule I.

The original 0.2.1 integration did not launch the native game. The 0.2.2 [presentation pass](vice-home-games-world-play.md) now records authorized native checks on copies of save 5, including world cameras, timed 20/6/11 hits, cue/ball presentation, control restoration and reload. Production DLLs/preferences and original-save hashes remain unchanged by that pass. Purchase/placement ghosts, full matches, physical inputs and genuine co-op remain follow-ups. PR #7 remains unmerged.
