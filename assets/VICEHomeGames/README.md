# VICE Lifestyle • Home games artwork

Original low-poly artwork for the Schedule I Lifestyle prototype. Artwork only: no gameplay changes, colliders, collision meshes, components, networking, animation, lights or cameras in the exports.

## Files and review status

- `VICE_HomeGames.blend`: editable metric source, Blender 4.3.2.
- `PoolTable.fbx`, `PoolAccessories.fbx`, `DartsBoard.fbx`, `Dart.fbx`: separate export packages.
- `Textures/`: three original 1024 × 1024 RGB PNG base-colour atlases.
- `Renders/`: actual source-mesh offline Cycles renders, not in-game screenshots. Presentation-only ball/cue/rack positions are not saved in the source or exports.
- `Orthographic_Dimensions.png`: labelled orthographic views of the actual models.
- `concepts/`: original generated visual references. These are aesthetic references, not geometry specifications. Use the corrected pool v2 reference.
- `scripts/`: deterministic generation, portable-FBX path cleanup, rendering and independent validation.
- `validation/`: machine-readable results and exact per-mesh source manifest.

Serialized FBX and Blender re-import are checked independently. Actual Unity 2022.3.62f2 import and in-game rendering remain separate review gates. The prototype's current URP/Unlit materials will not reproduce offline lit renders without appropriate URP/Lit material binding.

## Coordinates and authoring

Every measurement is metres, 1 unit = 1 metre. The source deliberately stores **Unity contract coordinates directly: +Y up, +X right, +Z forward**. This differs from Blender's conventional Z-up scene; do not rotate the artwork to “correct” it. All mesh vertices, object transforms and named empties are validated in this contract space.

Source objects overlap at the origin where the contract requires independent pivots. Solo the named collections to inspect each package. All mesh rotation/scale transforms are identity; every ball has an independent centre origin and identity location/rotation/scale. Accessory display layouts exist only in the unsaved render scene.

Export uses `axis_forward='-Z'`, `axis_up='Y'`, `use_space_transform=False`, `bake_space_transform=False`, `apply_scale_options='FBX_SCALE_UNITS'`. Raw FBX XYZ geometry remains in the contract basis; metre scale is recorded as `UnitScaleFactor=100` centimetres per unit, rather than a non-identity root scale. FBX metadata is Y-up and negative-Z front. The independent binary parser verifies the actual result rather than inferring it from settings. Importer-specific basis handling must still be checked in Unity.

All texture references are relative `Textures/...`, including both FBX absolute-name and relative-name fields. No embedded textures, custom shaders or normal/packed-map dependencies. Base colour is sRGB. Roughness and metallic are ordinary material scalars; no packed channel mapping applies.

## Pool contract

| Element | Exact dimensions / coordinates |
|---|---|
| `PoolTable_Root` | Floor under bed centre: `(0,0,0)` |
| Exterior | X 1.40 × Y 0.86 × Z 2.52 |
| Playing surface | X `[-0.56,+0.56]`; Z `[-1.12,+1.12]`; Y `0.78` |
| Ball | Diameter `0.05715`, radius `0.028575` |
| Resting ball centre | Y `0.808575` |
| Pocket capture centres | X `±0.56`, Z `-1.12,0,+1.12`, Y `0.78` |
| Visual pocket opening | Radius `0.065`; aligned dark mouths and actual cloth edge cutouts |
| Corner cushion gaps | `0.09` along each adjoining cushion |
| Side cushion gaps | Full `0.18`, corresponding to code-owned `abs(Z)<0.09` |
| Cue | Length `1.45`, maximum diameter `0.032`; tip at origin; extends local −Z |
| Rack | Inside equilateral side `0.30`; wall thickness `0.01`; height `0.025`; centre in XZ |

The cloth's complete rectangle bounds are preserved; its six edge openings expose the dark pocket artwork. Rail rubber begins at X `±0.56` / Z `±1.12`, so it does not obscure the code-owned ball-centre boundaries X `±0.531425`, Z `±1.091425`. A visual pocket is not a physics or scoring component.

Named meshes: `Pool_Body`, `Pool_Cloth`, `Pool_Rails`, `Pool_Pockets`, `Pool_Legs`; `Pool_Cue`; `Pool_Rack`; `Pool_Ball_00` through `Pool_Ball_15`.

Named empties:

| Empty | Local position |
|---|---|
| `Pocket_NW` | `(-0.56,0.78,+1.12)` |
| `Pocket_NE` | `(+0.56,0.78,+1.12)` |
| `Pocket_W` | `(-0.56,0.78,0)` |
| `Pocket_E` | `(+0.56,0.78,0)` |
| `Pocket_SW` | `(-0.56,0.78,-1.12)` |
| `Pocket_SE` | `(+0.56,0.78,-1.12)` |
| `PoolCue_Root`, `CueTip` | `(0,0,0)` |
| `CueGrip` | `(0,0,-1.05)` |
| `PoolRack_Root` | `(0,0,0)` |

The 0.30 m inside rack is an intentionally removable decorative prop. It does **not** fit the prototype's current 15-ball arrangement: its centre spacing and ball size require approximately 0.328787 m inside side length. Do not use this artwork to arrange balls or silently enlarge it.

Ball 00 is white; 01–07 solids, 08 black, 09–15 stripes. One atlas contains all sixteen tiles. Number islands occur on opposite hemispheres, away from the longitudinal UV seam. Longitude is explicitly oriented to keep both hemisphere numbers unmirrored.

## Darts contract

| Element | Exact geometry |
|---|---|
| `DartsBoard_Root`, `BoardCentre` | Scoring centre on mounting wall plane `(0,0,0)` |
| `Darts_Backplate` | Diameter `0.52`, Z `[0,0.025]` |
| `Darts_Board` | Diameter `0.45`, Z `[0.025,0.055]` |
| `Darts_NumberRing`, `Darts_Wire` | Thin planar face at Z `0.057`, no thickness beyond allowed `0.002` |
| `Darts_Mount` | Recessed within the above bounds |
| `BoardFace` | `(0,0,0.055)` |
| Inner / outer bull | Radii `0.00635` / `0.0159` |
| Triple | Radii `0.099` / `0.107` |
| Double | Radii `0.162` / `0.170` |

+Z points toward the thrower; +Y is the centre of 20; +X is right from the thrower's view. Fixed front-projection UVs preserve orientation. Each wedge spans 18°; clockwise order is:

`20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5`.

Singles alternate charcoal/ivory; 20 is charcoal. Double/triple 20 is red, alternating green. Scoring remains analytical in code; artwork and wires do not decide boundary hits. Recommended scoring-centre height: 1.73 m; throw line: 2.37 m. Player placement owns these world offsets.

Dart root is exactly at the tip, local +Z toward the tail. `Dart_Tip`, `Dart_Barrel`, `Dart_Shaft`, `Dart_Flights` together occupy Z `[0,0.16]`, with maximum flight span `0.036`. `DartGrip` is `(0,0,0.06)`. No rig or animation.

## Materials and budgets

Pool source and exports retain: `Wood`, `Cloth`, `RailRubber`, `PocketBlack`, `Metal`, `CueWood`, `CueTip`, `RackWood`, `BallAtlas`.

The pool assembly uses **9 unique materials**, within the 12-material budget. There are **29 summed renderer material slots** across all required named meshes and 16 independently moving balls. Unique materials and summed renderer slots are different counts.

The approved darts ambiguity resolution preserves **all 10 exact source material names**: `SisalDark`, `SisalLight`, `RingRed`, `RingGreen`, `WireMetal`, `Numbers`, `MountMetal`, `DartMetal`, `DartPlastic`, `DartFlight`.

Runtime FBX consolidation:

| Source semantic material | Runtime material |
|---|---|
| `MountMetal` | `WireMetal` |
| `DartMetal` | `WireMetal` |
| All other names | Unchanged |

Mount and dart metal use the same atlas metal swatch and ordinary material settings. The source is saved **before** consolidation, so all ten named datablocks remain editable. Source semantic names are also preserved as mesh custom properties for integration documentation.

Combined board + dart exports use **8 unique materials**, with **12 summed renderer slots** (8 on board meshes, 4 on dart meshes). Keeping the nine required named meshes makes an eight-total-renderer-slot reading impossible without violating the mesh contract. This package therefore uses the explicitly approved unique-runtime-material interpretation; it does not claim eight summed slots.

| Asset | Actual triangles | Budget |
|---|---:|---:|
| Pool table | 1,484 | 6,000 |
| Cue | 156 | 500 |
| Rack | 24 | 300 |
| Each ball | 288 | 320 |
| Board, numbers and wires | 2,818 | 4,000 |
| Dart | 158 | 400 |

No LOD1 is included: the pool body is already far below budget, and scoring-face/ball geometry should not be casually decimated. Separate LOD variants can be authored later without changing authoritative pivots or dimensions.

## Integration notes, no gameplay changes

Replace static visual children under the existing `FurnitureVisual`; retain native S1API placement/bounds, interaction, GUID and code-owned dynamic visuals. IDs stay `vice.lifestyle:pool` and `vice.lifestyle:darts`.

Do not bake S1API's `(0.50,0,1.25)` pool grid offset into the artwork. Placement reserves 3 × 6 half-metre tiles. Artwork creates no network objects and adds no colliders.

The prototype currently uses centre pivots for cue and dart visuals, while these deliverables correctly use requested tip pivots. A **separate art adapter**, not a gameplay change, is needed:

- Cue: under a legacy centre-pivot handle whose cylinder runs ±0.725 local Z, place the new tip-root child at local `+Z 0.725` with matching orientation. Alternatively place the tip directly behind the ball using the existing `0.035 + charge × 0.08` visual separation. Do not apply both corrections.
- Dart: under a legacy centre-pivot handle whose mesh runs ±0.08 local Z, place the new tip-root child at local `−Z 0.08`. Alternatively place the new tip directly at board-face Z `0.055`. Do not apply both corrections.
- Balls: instantiate the exact `Pool_Ball_XX` mesh at code-owned positions. Do not retain presentation rotations/positions from review images.
- Rack: removable visual only; the code-owned rack layout remains authoritative.

No adapter implementation is included or implied by this art-only handoff. Test the coordinate wrappers in Unity before enabling art in the live prototype.

## Reproduce and validate

Dependencies: Blender 4.3.2 (bundled FBX importer/exporter and parser), Python 3 with Pillow for atlas/contact-sheet generation, and DejaVu Sans Bold. No third-party model downloads, Unity runtime package or proprietary exporter is required.

From this directory:

```sh
python scripts/create_atlases.py
blender -b --python scripts/build_assets.py
blender -b --python scripts/validate_assets.py
blender -b --python scripts/render_assets.py
python scripts/create_review_sheets.py
```

Generation replaces these artwork outputs only. Rendering adds temporary presentation cameras/lights/ground and object transforms in memory; it does not save them into the source. `build_assets.py` records an exact source manifest and sanitizes every FBX texture filename. Validation checks actual exported binary FBX data and native source, including metre dimensions, roots, axes, parent transforms, mesh names, pivots, material counts, triangle budgets, number UV handedness, pocket-mouth clearance and the full scoring-face orientation. Consult the JSON report for individual checks and limitations.
