# Independent home-games presentation review

Reviewed implementation: `55108cb524eee6402a2305f98323f071b411ab73` in draft [PR #8](https://github.com/Axl47/ScheduleOne-Mods/pull/8).

**Accepted within the presentation review scope; ready for manual playtesting.** No remaining blocker was found after the primitive-board visibility and stale seat-message fixes. The accepted models, UVs, normals and textures remain unchanged. This evidence addition does not change implementation or artwork.

| Evidence | Source and interpretation |
| --- | --- |
| [Inventory thumbnail sheet](Gaia_VICE_PR8_Thumbnails.png) | Unity model renders, with the candidate pool finish and accepted darts tuning. These are not inventory UI screenshots. |
| [Native contact sheet](Gaia_VICE_PR8_NativeContactSheet.png) | Actual Schedule I captures from an isolated copied save 5. Programmatic single-host interactions; no physical-input or co-op claim. |
| [Material comparison](Gaia_VICE_PR8_UnityMaterialComparison.png) | Controlled Unity 2022.3.62f2 / URP 14.0.12 renders. These are not game screenshots. |

Sheets use original captured pixels, resized and labeled without retouching or exposure correction. Pixel inspection confirmed complete table framing, clear near pockets, matching 20/6/11 dart impacts, readable primitive labels and current seated feedback.

Independent final-head regression: **426 Lifestyle assertions passed**. Final native runs passed **85 artwork and 88 primitive checks**. The tested DLL matches the exported package: SHA256 `be98378c617bb8808eecab20c370ac0654a1354ec667830700674fd8326b54bf`. Both runs recorded identical before/after hashes for all 49 original save files and unchanged production DLLs/preferences. Maximum projection error was below 0.8 mm; all measured restored camera poses showed zero displacement and rotation.

Native testing followed the user's instructions, “Use save 5 for testing” and “feel free to do any test, the game is closed,” using a separate loader profile and copied save. No merge, production installation or original-save mutation occurred in this presentation pass.

The material comparison supports restraining pool highlights without a geometry repair. Serrated cloth-side shadows remain a separate lighting/shadow-map issue; these results do not establish elimination of the reported white lines under every game lighting condition. Darts/ball tuning and global AA are preserved. See [world-play validation scope](../../../vice-home-games-world-play.md) for source evidence paths and controls.

Physical mouse/keyboard accessibility, purchase and placement ghosts, wall/obstruction cases, other aspect ratios, complete matches and genuine host/guest co-op remain manual follow-ups. PR #8 remains draft and unmerged. Library publication was unavailable; these exact review sheets are preserved here for inspection.
