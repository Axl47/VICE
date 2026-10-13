# UnityBasisV2 export-only review probe

Small candidate FBXs for Unity 2022.3.62f2 review. These replace nothing automatically. The production package remains unchanged until this probe is accepted.

## Changes

- Source authoring stays in canonical Unity-contract XYZ coordinates.
- FBX export pre-reflects local mesh X and empty X, reverses winding, and explicitly triangulates. Unity's observed FBX X reflection should therefore recover the canonical signed coordinates.
- Pool box primitives retain analytically outward winding. No global normal recalculation across touching boxes. Y-axis cylinder winding corrected. Rail sights are single +Y-facing planes.
- Every cloth UV is the centre of the uniform green swatch, removing skinny-triangle texture derivatives and adjacent-cell mip bleed. No double-sided material workaround.

## Required Unity import and checks

Use the same normal/default axis import settings as the failed first candidate, with `ModelImporter.preserveHierarchy=true` so named roots survive. Scale remains one metre per unit. Use ordinary back-face culling.

1. Pool root at identity. `Pocket_NE=(+0.56,0.78,+1.12)`, `Pocket_NW=(-0.56,0.78,+1.12)`; all six signed positions must match README in the production package.
2. Board's +X-right art is asymmetric: `(X,Y,Z)=(+0.12,0,0.055)` lies in 6; `(-0.12,0,0.055)` lies in 11. 20 remains +Y. Number-ring UVs and labels must be unmirrored.
3. From above, every wood top and rubber cushion top is visible with Cull Back; from below those same outer top faces are culled. Side faces remain visible from their outside directions.
4. Cloth is a clean uninterrupted green surface between six true edge bites, with no patterned streaks at player distance. Verify under normal mipmapping and anisotropic filtering.
5. Cue tip is 0 and shaft is -Z 1.45; dart tip is 0 and tail +Z 0.16. Balls remain Ø0.05715 with readable numbers on both hemispheres.
6. Recheck exterior/bed/scoring dimensions and all triangle budgets. Export triangulation changes polygon storage, not shape.

## Material object identity

The FBXs retain eight canonical board/dart runtime material names. Importing two FBXs independently may create a second WireMetal object despite identical names. Bind all WireMetal slots in both packages to the same shared Unity material asset when checking the eight-material object budget. A shared art-import binding will accompany accepted production exports. No gameplay adapter or runtime component is included in this probe.
