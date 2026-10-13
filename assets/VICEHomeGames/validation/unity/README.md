# Independent Unity artwork acceptance

Accepted artwork commit: `b6b148e0c2bdf171f8eab9da5b4edddcbd1d9a18`.

Verdict: **ACCEPTED as artwork** under the approved board presentation contract and importer/shared-material recipe. The subsequent PR evidence refresh changes no reviewed model or atlas bytes.

- 881 source/FBX/reference checks passed when the supplied validator was run independently in Blender 5.2.2. Source was authored in Blender 4.3.2.
- 183 checks against actual Unity 2022.3.62f2 imported data passed.
- All 14 ordinary-camera images were visually inspected. Correct clockwise board sequence and readable glyphs, both readable ball hemispheres, clean cloth, intact Cull Back rail/cushion faces, correct signed pockets and preserved pivots passed.
- Analytical `(0.12,0)` is demonstrated at artwork root `(-0.12,0,0.055)` in visible sector 6. The opposite mapped hit lands in 11. No projection reflection, negative camera/object scale, image flip or gameplay change was used.
- The isolated review project used persistent shared `.mat` assets and importer remaps. Board + dart use eight actual shared Material objects across twelve slots, including one WireMetal reference across both FBXs. Pool uses nine materials and 28 imported slots; Unity drops the unused source Metal slot on Pool_Pockets.

## Files

- `production-review.json`: independent checks, outcome and limitations.
- `unity-preserved-report.json`: actual imported transforms, geometry, material identities and texture resolution.
- `unity-winding-report.json.gz`: complete actual imported triangle/corner-normal measurements, compressed losslessly.
- `material-remap-report.json`: actual shared material objects and texture settings.
- `blender-fbx-validation.json`: independent execution of the supplied validator, including accepted-reference comparisons.
- `accepted-artwork-sha256.json`: exact reviewed model/source/atlas hashes.

Run `python scripts/validate_unity_evidence.py` from the asset root to recheck 183 numeric assertions against these saved measurements. This is a replay of recorded evidence, not a fresh Unity execution. The complete isolated Unity reviewer project was supplied separately as review evidence; its Editor fixtures are not shipped as artwork components here.

## Reproduction setup

Unity 2022.3.62f2; Preserve Hierarchy enabled; globalScale=1; useFileScale=true; bakeAxisConversion=false; imported normals; no collider generation or animation import. Shared Standard materials, opaque surfaces, ordinary Cull Back, white atlas tint. Textures: sRGB, mipmaps enabled, bilinear filtering, anisotropy 8. Canonical material remaps and future visual adapter requirements are documented in the asset README.

Source and exports contain no cameras/lights/components. Cameras, temporary ball arrangements and mapped-hit darts existed only in the unsaved review scene. The 0.30 m rack is decorative and was omitted from the code-owned full-rack layout captures.

**Not verified:** native URP binding, gameplay adapter implementation, live game appearance, multiplayer, physics or save behavior. No active game installation, saves, merge or deployment was changed.
