# UnityGlyphV3 ball-only readability probe

Only ball longitude UV handedness changes relative to the accepted geometry/winding in UnityBasisV2 commit 41a155a. Geometry, pivots, normals, cue/rack, all colours and stripe layouts are unchanged. No board or scoring change.

Import PoolAccessories.fbx with the same Unity settings and shared texture bindings as the previous probe. Render both sphere hemispheres using the same ordinary cameras, with no negative-scale objects, projection changes or image flips. Numbers 1–15 should read upright and unmirrored from each hemisphere. Geometry remains 288 triangles and Ø0.05715 per ball; cue146 and rack24.

This is a glyph-only test, not resolution of the pending board presentation-coordinate convention.
