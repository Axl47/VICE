# Model worker prompt — VICE Lifestyle home games

Create replaceable low-poly Schedule I artwork for a playable pool table and darts board. The gameplay prototype already defines its dimensions. Do not implement rules, scoring, physics, networking or Unity components. Supply artwork only; no finished model is required to build or play the prototype.

## Coordinates and files

All measurements below are metres, at Unity scale 1 = 1 m. Unity coordinates are +Y up, +X right, +Z forward. Use Blender metric units, unit scale 1. Apply object rotation and scale. Export FBX using -Z Forward / Y Up, selected objects, mesh and empties only, no leaf bones, no baked animation. Verify the imported FBX dimensions and axes rather than assuming exporter settings preserve them.

Deliver `VICE_HomeGames.blend`, `PoolTable.fbx`, `PoolAccessories.fbx`, `DartsBoard.fbx`, `Dart.fbx`, a `Textures/` directory, and a README listing exported dimensions, material slots and named empties. Include an orthographic dimension sheet. Keep mesh names exact and unique. No lights, cameras, Rigidbody components or collision meshes in exports. Use one mesh per named part unless the contract below requests a root empty.

## Pool

`PoolTable_Root` origin: floor under centre of playing bed. Local X is width, Z is length; the break starts at negative Z. Exact playing surface spans X [-0.56, +0.56], Z [-1.12, +1.12], bed Y = 0.78. Ball radius is exactly 0.028575; ball centre resting Y = 0.808575. Exterior dimensions: 1.40 X × 0.86 Y × 2.52 Z. Grid placement reserves 3 × 6 half-metre tiles. S1API offsets the whole visual by (0.50, 0, 1.25) from the native grid origin; do not bake that offset into artwork.

Required meshes: `Pool_Body`, `Pool_Cloth`, `Pool_Rails`, `Pool_Pockets`, `Pool_Legs`. Material slots: `Wood`, `Cloth`, `RailRubber`, `PocketBlack`, `Metal`. Six named empties: `Pocket_NW`, `Pocket_NE`, `Pocket_W`, `Pocket_E`, `Pocket_SW`, `Pocket_SE`, at (X,Z) = (-0.56,+1.12), (+0.56,+1.12), (-0.56,0), (+0.56,0), (-0.56,-1.12), (+0.56,-1.12), all Y = 0.78. Their visual dark openings must align with code-owned circular capture regions of radius 0.065. Pocket mouths are simplified; leave 0.09 m gaps measured along each adjoining cushion. Rail art must not obscure the code-owned centre boundaries X = ±(0.56 - radius), Z = ±(1.12 - radius).

Accessories: `PoolCue_Root` with `Pool_Cue` mesh; origin at tip, shaft extends along local -Z, total length 1.45 m, maximum diameter 0.032. Material slots `CueWood`, `CueTip`. Empties `CueTip` at origin and `CueGrip` at (0,0,-1.05). No rig required: code translates the whole cue for charge/strike. `PoolRack_Root` and `Pool_Rack`: origin centre of equilateral triangle, flat in XZ, inside side length 0.30, height 0.025, thickness 0.01, slot `RackWood`. Rack is a removable visual prop; code owns ball arrangement.

`Pool_Ball_00` through `Pool_Ball_15`: each origin at its own centre, diameter exactly 0.05715, identity transforms. 00 is white cue ball; 01–07 solids; 08 black; 09–15 stripes. Slot `BallAtlas`; share one atlas with readable numbers (both hemispheres), UVs avoid seams through numbers. No collision meshes. Code moves visual spheres and owns every circle collision; no baked movement or networking.

## Darts

`DartsBoard_Root` origin at scoring centre on the mounting wall plane. +Z points away from wall toward thrower; +Y points to the centre of the 20 wedge; +X right from the thrower's view. `Darts_Backplate` diameter 0.52, thickness 0.025, occupying Z [0,0.025]. `Darts_Board` diameter 0.45, occupying Z [0.025,0.055]. `Darts_NumberRing` and `Darts_Wire` face near Z = 0.057, no thicker than 0.002. `Darts_Mount` hidden behind board, inside those overall bounds. Slots `SisalDark`, `SisalLight`, `RingRed`, `RingGreen`, `WireMetal`, `Numbers`, `MountMetal`.

Exact scoring radii: inner bull 0.00635, outer bull 0.0159, triple inner/outer 0.099/0.107, double inner/outer 0.162/0.170. Each wedge is 18 degrees, 20 centred on +Y, clockwise order from thrower's view: 20,1,18,4,13,6,10,15,2,17,3,19,7,16,8,11,14,9,12,5. Alternate light/dark singles, red/green doubles and triples. Wires and artwork do not score hits; code uses analytical rings/wedges and exact-boundary conventions. UVs must preserve the orientation; do not rotate to fit an atlas. Empty `BoardFace` at (0,0,0.055); `BoardCentre` at origin. Placeable height is chosen by the player; recommend scoring centre 1.73 m above floor, throw line 2.37 m away.

`Dart_Root` origin at tip; local +Z points from tip toward tail. Meshes `Dart_Tip`, `Dart_Barrel`, `Dart_Shaft`, `Dart_Flights`; total length 0.16 m, maximum flight span 0.036, tip at Z=0, tail at Z=0.16. Slots `DartMetal`, `DartPlastic`, `DartFlight`. Empty `DartGrip` at (0,0,0.06). Whole-object translation is sufficient for throw/stick; no rig or animations. The code will put the tip at a scored coordinate on the face and can clear darts each turn.

## Appearance, budgets and integration

Use chunky silhouettes, modest wear, simple saturated colours, readable numbers and restrained roughness. Avoid photorealistic microdetail. Pool table <= 6,000 triangles; cue <= 500; rack <= 300; balls <= 320 each. Board including numbers/wires <= 4,000; each dart <= 400. <= 12 material slots for an assembled table and accessories; <= 8 for assembled board/darts. Use shared atlases where practical: one 1024² pool furniture atlas, one 1024² balls atlas, one 1024² darts atlas. Supply PNG base colour, optional normal and packed metallic/roughness/AO with channel mapping. No generated shader dependency, embedded textures or absolute texture paths. No transparent layers except a justified cutout; numbers should remain readable from gameplay view. Include simple LOD1 meshes at roughly half the triangles if practical.

Rules/state and physics are separate ordinary C# classes in VICE.Lifestyle. Furniture uses native S1API IDs `vice.lifestyle:darts` and `vice.lifestyle:pool`. Code restores local visuals on each peer; model meshes never spawn their own network objects. Integration replaces static visual children under `FurnitureVisual` while retaining native placement, bounds, interaction, GUID and code-owned dynamic parts. An adapter maps the exact accessory names to the prototype's visual handles. Cloth extents/height, ball diameter, scoring radii, board orientation/face depth, pocket centres and cue tip pivot must match exactly. Exterior styling, legs, mounting hardware and wear can change within the stated outer bounds. A replacement asset must not move roots or add colliders to alter gameplay.
