# Disposable S1LSD visual comparison 0.1.1

This is a separate removable preview based on the user-supplied **S1LSD 0.11.0** archive. Its MIT license allows adaptation with attribution. The original perception shader tree/programs and dose defaults are preserved byte for byte, and selected pure configuration/visual calculations were recovered from the supplied DLL. The shader's container was repaired for loading, as described below. This is a comparison of its visual direction, not a complete port of the original mod.

## Try it

With `Personal.S1LSDComparison.dll` in the game's `Mods` directory, load your game normally. Nothing starts automatically.

1. Close other menus and press **F6** to open the comparison panel.
2. Choose **Classic** or **Subtle**, then **Hold peak** or **Full timeline**.
3. Click a tier: **70**, **140**, **280** or **360**. The original UI labels these tiers in µg.
4. Look at textured walls, road surfaces, shadows and moving bright objects. Stand still for a few seconds to see gaze amplification; turn to compare trails. The completed-frame shader also includes the phone and HUD.
5. Press **F10** to clear the comparison immediately. F6 reopens the panel while testing. After stopping, **F8** starts our personal preview again with its existing settings.

The panel uses native game UI buttons with hover/selection colors. Version 0.1.0 stopped drawing at a stripped `GUILayout.Toolbar` call, leaving only labels; version 0.1.1 replaces that entire panel. While the panel is open, **C/S** select Classic/Subtle, **P/T** select Hold peak/Full timeline, and **1–4** start the four tiers as keyboard alternatives. **F6** or **Esc** closes the panel. Style/timing selections apply when you start a tier.

| Original tier label | Full timeline | Classic crawl scale at normalized peak, 1080p | Subtle crawl scale at normalized peak, 1080p | Geometry channel peak |
| --- | --- | --- | --- | --- |
| 70 | 3.5 minutes | 0.83 px | 0.54 px | 0 |
| 140 | 5.5 minutes | 1.75 px | 1.08 px | 0.35 |
| 280 | 8.5 minutes | 4.83 px | 3.25 px | 0.45 |
| 360 | 11 minutes | 9.17 px | 6.50 px | 0.65 |

These are shader crawl scales, not guarantees that every pixel moves by that amount. The original shader suppresses displacement on silhouettes and uses texture/brightness information. Peak channels fluctuate with the original phase noise. Geometry is also gated by scene darkness inside the shader.

Hold peak is the default: it starts at the middle of the peak phase and keeps that phase active, while breathing and shader animation continue. Full timeline runs onset → come-up → peak → plateau → come-down → afterglow and then stops. Comparing tiers uses the same seed, zero tolerance, neutral setting and no random bad-trip episodes. Phase timing variation is disabled for repeatable comparisons.

## Included and adapted

Included: the original perception shader, dose curves, texture crawl, breathing, brightness-dependent hue changes, vibrance, local contrast, fringes, selective/reprojected trails, four geometric form weights, and corresponding native color/bloom/lens/grain/vignette targets. Classic/Subtle changes are preserved from the original visual mixer. The optional broad-wave preset in the archive's separate `classic-5a09783.json` is **not** applied; this comparison uses the release's embedded defaults.

The supplied `s1lsd_shaders.bundle` resource is actually a raw serialized shader file with two Shader objects and no AssetBundle/container. Unity rejected it through `LoadFromMemory` despite its serialized data using the same Unity version as the game (`2022.3.62f2`). `Build-S1LSDComparisonBundle.py` wraps the perception shader in a valid UnityFS bundle using our working owned container. It verifies that the entire perception shader tree, including executable programs and binding metadata, is unchanged. The original resource remains in `Assets/s1lsd_shaders.original.bundle` for provenance; the unused Copy shader is omitted because our GPU history copy replaces it.

The original camera callback and native render passes are replaced with our completed-frame GPU capture/draw/presentation path. This avoids depending on the camera-event approach that previously produced missing movement in our tests. The shader is unchanged. The draw includes UI; camera reprojection is approximate during translation and menu/overlay changes. Three screen-sized textures hold source, output and history, without CPU readback during normal previews. History invalidates on resize, large turns or position jumps. An owned Volume supplies world-camera grading; native profiles and materials are never edited.

The original's audio filters/procedural soundscape, closed-eye display, bad-trip setting mechanics, tolerance saving, recipes, items, NPC consumption, sales, movement modifiers and console commands are omitted. **Audio remains dry during comparison** so it does not inherit our own audio effect. Porting its audio can be a separate experiment: its listener-filter implementation includes `AudioEchoFilter`, which is stripped from this game build. Our working native mixer echo remains available in our personal preview.

Our personal preview's existing update callback is temporarily suspended while a comparison runs and restored when it stops. Its active effect is cleared rather than stacked. Its DLL and preferences are unchanged; no preference values are set or saved by this comparison. The personal preview timer does not resume automatically. The new mod creates no products and writes no save data or configuration files. Exiting/loading another scene releases the comparison and its panel/input lease.

## Remove it

Close the game, then remove or rename only `Mods/Personal.S1LSDComparison.dll`. Nothing else is required. Our existing `Personal.LSDPreview.dll`, `Personal.LSDGameplay.dll`, personal settings and saves remain in place. Do not install the original `S1LSD.dll` alongside this preview for this comparison; its product/patch systems would make the comparison less isolated.

## Build and validation

```powershell
./scripts/Build-S1LSDComparison.ps1
# Optional, with the game closed:
./scripts/Build-S1LSDComparison.ps1 -Install
```

The package is `artifacts/S1LSDComparison-0.1.1.zip`. It contains one mod DLL, this guide and the original MIT license/notices. No game or loader DLLs are distributed. The build verifies both source and embedded asset hashes, rejects dependencies on the original gameplay/persistence runtime and checks the completed-frame rendering calls. Standalone checks cover all four dose/style fixtures, original durations, peak holding without freezing breathing, finite targets, timeline completion and cleanup.

An explicit **`--s1lsd-comparison-check`** launch flag runs a menu-only shader diagnostic: controlled texture identity, time-varying movement, final-screen identity/orientation and visible final-screen displacement. It loads no save and is never run during an ordinary launch. Native rendering and gameplay observations are recorded below after checks run.

The visible menu check on **October 7, 2026 at 05:40** passed with **zero controlled-image identity errors**, **205,366 time-varying pixels** at tier 360 in the 1280×720 offscreen image, **zero final-screen changes at zero strength**, and **153,318 changed final-screen pixels** with the effect active. Native color profile creation/update/disposal also passed, and the personal preview callback restored successfully. The final pass emitted no render-texture cleanup warning. No save was loaded. Evidence is retained locally in `artifacts/s1lsd-comparison-check/final-menu.log` and `player-final.log`. This verifies loading, bindings, screen orientation, animation, native grading APIs and cleanup; the four tiers' appearance in the city and phone interaction remain playtest observations.

Version **0.1.1** replaces the stripped IMGUI toolbar with an owned uGUI Canvas, GraphicRaycaster, native Buttons and TextMeshPro labels. Its build guard rejects IMGUI dependencies and verifies the native control types. The October 7 live UI check visibly confirmed distinct controls, selected-state colors and mouse clicks on Subtle, Full timeline and tier 70; the log recorded the requested settings and completed-frame rendering. The 70 full timeline completed and restored the personal callback, and the same live session subsequently started tier 360 and reopened its panel. The check began at the main menu, but the game entered Main before button testing; this is not a menu-only UI result. Individual clicks on tiers 140/280, Stop/Close and the keyboard alternatives were not separately verified by automation. All four dose/style calculations and cleanup pass the standalone checks. Evidence: `artifacts/s1lsd-panel-check/native-panel.log`.

The opt-in **`--s1lsd-panel-check`** flag permits manual F6 panel testing at the main menu without a loaded player. Ordinary launches continue to require a loaded game. This flag never loads a save itself.
## Attribution and provenance

Original effects/configuration: **S1LSD Contributors**, copyright 2026, MIT. See `licenses/S1LSD/LICENSE.txt` and `licenses/S1LSD/THIRD_PARTY_NOTICES.txt`. New preview wrapper, controls, frame presentation and isolation: this repository. Vendor source headers identify adapted files; the comparison-only `HoldPreviewPhase` method is marked in the dose controller. `Assets/provenance.json` records the original assembly and embedded asset SHA-256 hashes.

Original DLL SHA-256: `f33137bf2427574f30d2134e2858cb8ca5cc0c9363749b08dab34251eed5b9a9`.

This preview adds no alcohol effect; the previously recorded alcohol displacement experiment remains a future note in the baseline documentation.
