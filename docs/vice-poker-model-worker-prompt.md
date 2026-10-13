# VICE cash poker table — model-worker prompt

Copy everything below into a separate model-worker chat. Produce artwork that replaces the current primitives without changing gameplay.

---

Create a low-poly four-seat home poker table and small reusable card/cash accessories for Schedule I's private VICE.Lifestyle mod. Match the game's simple silhouettes, subdued painted surfaces and modest texture detail. Use dark timber, worn green felt and a padded rail. Give the chairs a comfortable, readable silhouette. There is **no dealer character or dealer seat**. Four players occupy the four cardinal seats.

Deliver an editable Blender scene, FBX exports, textures, and a brief import/readme file. Include top, side and each seat-camera preview. The geometry contract below is fixed. Styling, bevels, stitching and wear are your choices within those dimensions. Do not write gameplay scripts, Unity components, colliders, networking, rules, UI or card-visibility logic.

## Units, axes and roots

- The coordinates below are **Unity local metres**. +X is right/east, +Y is up, +Z is forward/north. The table's root is its centre on the floor, `(0,0,0)`; scale is `(1,1,1)` and rotation is identity.
- Name the furniture visual root `PokerTable_Root`. Every coordinate is relative to this visual root, not the native grid item's origin. S1API places the whole visual at `(1.25,0,1.25)` within its 6×6 half-metre grid footprint. Do **not** bake that placement offset into the artwork.
- Blender uses metre units, scale 1. Its Z-up presentation must export to the stated Unity Y-up axes. Provide an axis helper and confirm the imported top lies in Unity XZ. Apply mesh scale and rotation before export; validate the exported axes rather than relying on an FBX preset label.
- The code-owned footprint is **3.0×3.0 m**. The fixed collision envelope is **2.56×0.76×2.56 m**, centred at `(0,.38,0)` in visual space. Keep furniture meshes within X/Z ±1.28 m. Collision stays in code; omit exported colliders.

## Furniture dimensions

| Part | Fixed dimensions / position |
| --- | --- |
| Table | Circular, 1.80 m outer diameter; highest usable playing plane Y=.760 m |
| Felt | 1.62 m diameter; flat playing plane Y=.760 m; no raised decorations under cards |
| Rail | Between radii .81 and .90 m; keep its top at or below Y=.760 m |
| Body | Approximately Y=.640–.760 m; do not invade runtime cards, cash or UI |
| Pedestal | Centred; approximately .30 m diameter, floor to Y=.640 m |
| Base | Approximately .72 m diameter, top Y=.090 m |
| Chairs | Four; seat cushion 0.40×0.08×0.40 m, centre Y=.430 m, upper seating face Y=.470 m. Chair centre radius 1.08 m. Keep the entire chair within the collision envelope, including its back. A modest back may extend above the seat, but must not enter the camera ray or runtime rail UI. Report any back above Y=.760 m; it is visual only, not part of the fixed collision height. |

Mesh hierarchy: `Poker_TableBody`, `Poker_Felt`, `Poker_Rail`, `Poker_Pedestal`, `Poker_Foot`, and `Poker_Chair_0` through `Poker_Chair_3`. Chair children may be `Seat`, `Back`, `Legs`. Material names: `Poker_Wood`, `Poker_Felt`, `Poker_Rail`, `Poker_ChairFabric`, `Poker_Metal`. The top must render correctly from above with ordinary back-face culling; no doubled faces or negative mesh scales.

## Code-owned seats and camera anchors

Seats advance clockwise, viewed from above with +Z at the top: south, west, north, east. Yaw is a Unity rotation about +Y, applied to the seat-0 coordinates.

| Seat | Seat anchor | Yaw | Camera position | Camera looks at |
| --- | --- | --- | --- | --- |
| 0, south | `(0,.430,-1.080)` | 0° | `(0,1.450,-1.350)` | `(0,.770,0)` |
| 1, west | `(-1.080,.430,0)` | 90° | `(-1.350,1.450,0)` | `(0,.770,0)` |
| 2, north | `(0,.430,1.080)` | 180° | `(0,1.450,1.350)` | `(0,.770,0)` |
| 3, east | `(1.080,.430,0)` | 270° | `(1.350,1.450,0)` | `(0,.770,0)` |

Preview cameras use vertical FOV 70°, near clip .025 m, world +Y up. Empties named `Seat_0`…`Seat_3` and `Camera_0`…`Camera_3` may be supplied as reference helpers. The runtime derives these from `PokerGeometry`; changing artist empties will not change seating or camera behavior.

## Runtime accessory anchors

The runtime supplies and moves these objects. Leave the named spaces clear; do not bake cards, the dealer button, money piles, dollar labels or controls into the furniture mesh.

| Seat | Hole card 0 | Hole card 1 | Stack anchor | Bet anchor | Dealer button |
| --- | --- | --- | --- | --- | --- |
| 0 | `(-.050,.779,-.570)` | `(.050,.779,-.570)` | `(.230,.790,-.620)` | `(-.230,.790,-.410)` | `(.350,.784,-.450)` |
| 1 | `(-.570,.779,.050)` | `(-.570,.779,-.050)` | `(-.620,.790,-.230)` | `(-.410,.790,.230)` | `(-.450,.784,-.350)` |
| 2 | `(.050,.779,.570)` | `(-.050,.779,.570)` | `(-.230,.790,.620)` | `(.230,.790,.410)` | `(-.350,.784,.450)` |
| 3 | `(.570,.779,-.050)` | `(.570,.779,.050)` | `(.620,.790,.230)` | `(.410,.790,-.230)` | `(.450,.784,.350)` |

- Five community-card centres, at Y=.780, Z=0: X=`-.200,-.100,0,.100,.200`. Each has yaw 0°.
- Deck centre: `(.480,.795,.180)`, yaw 0°.
- Pot label anchor: `(-.380,.795,.180)`, yaw 0°. Its placeholder cash bundle centre is .015 m below that anchor. Leave a .38×.10 m label area clear.
- Side-pot amounts are text owned by code; provide no fixed pot compartments.
- Stack bundles use their seat's yaw. Their label is .024 m above the stack anchor, in a .37×.10 m area.
- Bet bundles use their seat's yaw, .014 m below the bet anchor. Their label area is .26×.10 m.
- The private action console has a 0.84×0.272 m world-space face. Seat-0 console centre is `(0,.820,-.800)`, Unity Euler rotation `(70,0,0)`. Other seats rotate its position and orientation by their seat yaw. Leave those four volumes and their seat-camera sightlines clear. **Do not model the console or its buttons.**
- Reference empties may be named `Hole_0_0`, `Hole_0_1` etc., `Community_0`…`Community_4`, `Stack_0`…`Stack_3`, `Bet_0`…`Bet_3`, `Button_0`…`Button_3`, `Deck`, `Pot`, `Controls_0`…`Controls_3`. Put them in a separate `Reference_Anchors` collection. Exporting helpers is optional; their coordinates are informational.

## Accessory files and pivots

Export each accessory separately, with its pivot at its geometric centre, identity rotation and unit scale in Unity. No accessory should carry a table-world offset.

- `Poker_Card`: .063 m along local X, .088 m along local Z, .002 m along local Y. The face points +Y; the top/rank orientation faces toward local +Z from seat 0. Give the face a full non-overlapping 0–1 UV island. Keep face and back material slots separate: `Poker_CardFace` and `Poker_CardBack`. All 52 identities and private/revealed visibility are assigned by code. Supply a neutral face template and a single repeating back; do not encode ownership into the texture.
- `Poker_Deck`: same .063×.088 m footprint, .030 m high, +Y top. Include simple stacked edges. Materials `Poker_CardEdges` and `Poker_CardBack`.
- `Poker_CashBundle`: .090×.035×.050 m in X/Y/Z. Flat +Y top, centre pivot. Materials `Poker_CashPaper`, `Poker_CashBand`. This is an abstract bundle; labels carry dollar amounts. Code may scale its height to .014 m for bets or .015 m for the pot. Keep all lettering decorative and avoid a denomination that contradicts the runtime label.
- `Poker_DealerButton`: .040 m diameter, .006 m high, +Y top; `Poker_Button` material. A small “D” is fine.

## Mesh and texture budgets

- Aim for ≤12,000 triangles for the complete furniture and four chairs. Card ≤80 triangles, deck ≤200, cash bundle ≤160, button ≤128. Use sensible bevels and weighted normals; do not hide large subdivision modifiers in the exported mesh.
- Furniture: one 1024×1024 base-colour atlas, optionally one 1024×1024 normal atlas. Accessories: one 512×512 atlas for cash/button/edges; one 512×512 back texture and a separate 512×512 neutral face template. PNG, sRGB base colour, normal map marked linear. Keep total supplied textures under 8 MiB uncompressed RGBA equivalent if possible.
- UV0 in 0–1. Mirroring is fine on unlettered furniture; avoid mirrored text and visible markings. No lightmap UV requirement. No texture should require a custom shader. Supply material colour/roughness values for Unity URP Lit; current placeholders use unlit materials while integration is pending.

## Export and delivery

Supply `VICE_PokerTable.blend`, `PokerTable.fbx`, `Poker_Card.fbx`, `Poker_Deck.fbx`, `Poker_CashBundle.fbx`, `Poker_DealerButton.fbx`, the PNGs and `README-poker-art.md`. Use mesh-only FBX files: no armatures, scripts, gameplay components, embedded textures or animations. Keep editable Blender collections for furniture, each accessory, textures, reference anchors and preview cameras. Preserve modifiers in the editable scene; triangulate the export.

In the readme, record units, axis conversion, imported dimensions, triangle counts and texture sizes. Include a metre calibration cube outside the exported selection. Verify that a Unity import at scale 1 matches this contract, that faces are visible from all four cameras, and that all accessory pivots are centred. Deliver any stylistic variants with identical geometry and anchors.

The runtime will replace placeholder meshes/materials at these anchors. Collision, dealer rotation, dealing, card identities, hidden information, money, betting UI and networking remain owned by the code. Artwork must not change poker logic.
