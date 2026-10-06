# LSD visual preview 0.5.0

This separate `Personal.LSDPreview.dll` experiment explores the visual direction before adding an LSD product. F8 starts a 180-second preview or fades it out; F9 clears it immediately. It uses the installed game's rendering components and has no production, sales or save-data integration.

## This revision

The material-color breathing, lens breathing, character softness, afterimage orientation and texture-layer removal from 0.4.0 remain. The user confirmed that this version produced the intended subtle, uncanny feeling.

Ripple displacement is now **30% of the 0.4.0 amplitude**, a further 70% reduction at identical settings. The separate breathing settings are unchanged. HUD text blur is 50% stronger (`0.25` to `0.375`), afterimages are 25% stronger (`0.50` to `0.625`), and the default duration doubles from 90 to 180 seconds. Existing untouched defaults migrate once; other custom values are preserved.

Colors now receive stronger saturation (`32` to `56`), a small contrast increase (`24 ± 5` to `28 ± 4`), and a `0.15` EV brightness lift before overall intensity blending. The slow color filter is nearer neutral, keeping more of the original RGB brightness. Hue movement and material-color breathing keep their existing timing and strength.

The 0.4.0 live log showed that its audio layer stopped on a generic `GetComponent` type-initializer exception. This revision uses the non-generic Unity component APIs for listener lookup, child lookup, echo lookup and echo creation. It checks the rendering camera, parents, normal camera rig and, if needed, active listeners outside that hierarchy. The listener is cached; fallback discovery is limited to binding/rebinding and retries every two seconds while none is available.

Echo amount remains restrained: a 210 ms delay, rapid decay, unchanged dry signal and at most 1.26% wet mix at the default peak. Logs now identify the attached listener and confirm when the owned filter is enabled with a nonzero wet mix. This fixes the known initialization path; audible output still needs an in-game check. `AudioStrength=0` disables the addition. Existing listener echo filters are left in control.

Visible opaque world materials continue to breathe gently in color, with independent phases and speeds. At the default peak, each RGB gain stays within approximately 5.7% of its baseline. This changes material tint while retaining native textures, normals, mapping and lighting. Objects sharing a material share its phase. The former geometric pattern layer, texture copies and neighborhood refresh are absent.

Character figures retain source clothing/skin tint distinctions with slight desaturation. Capture excludes disabled renderers, particles and named aura/ring/effect helpers. At most two figures appear, spaced at least five seconds apart, and dissolve under direct attention. `HallucinationStrength` remains `0.55`.

## Research guiding the direction

[Firsthand visual accounts](https://www.reddit.com/r/LSD/comments/cxloux/lsd_visuals/) describe brighter colors, objects appearing to breathe and existing fine texture details seeming to move. The community [drifting description](https://effectindex.com/effects/drifting) separates breathing, morphing and texture flow, with considerable variation in rhythm and intensity. The design choice here is recognizable scenery with gentle color and shape changes, following the requested subtle direction.

Auditory descriptions also vary. The community [auditory-distortion catalogue](https://effectindex.com/effects/auditory-distortion) includes fleeting reverberation and echoes. A small [2015 study by Kaelen and colleagues](https://pubmed.ncbi.nlm.nih.gov/26257162/) found stronger emotional responses to music in ten volunteers; that finding concerns emotional response, not a prescribed echo sound. The restrained echo is an artistic interpretation, independently adjustable, rather than a claim that everyone hears this effect. Character apparitions remain part of the chosen surreal game direction.

## Install or update

1. Close the game. From `LSDPreview-0.5.0.zip`, copy **only** `Mods/Personal.LSDPreview.dll` into the game's `Mods` folder, replacing the previous preview DLL.
2. Restart and load a save. Begin near a visible NPC so an appearance can be captured. Native camera post-processing must be enabled.
3. Press **F8** to start or fade out over two seconds. Press **F9** to clear immediately, including in menus.

The build script packages the experiment without installing it or launching the game. Previous packages remain available for comparison. Existing custom preferences carry over. The internal `DefaultsRevision` marker upgrades untouched older defaults and the three changed 0.4.0 defaults once, then preserves later manual edits.

Effects hide and the timeline pauses during pause, lost focus, typing and menus. Skateboard/car input restrictions preserve the preview; explicit phone/dialogue/cursor checks still suspend it. Loading, death, unconsciousness, player replacement or a change in consumed product clears it. Replacement world cameras get fresh rendering resources while retaining trip progress.

## Tuning

Edit `UserData/MelonPreferences.cfg`, category `PersonalLSDPreview`, while the game is closed. Strength values accept 0–1.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Enabled` | `true` | Manual F8 preview; nothing starts automatically. |
| `Intensity` | `0.60` | Overall strength. |
| `MotionStrength` | `0.50` | Lens breathing, color separation, ripples and afterimages; zero disables these four. |
| `RippleStrength` | `0.50` | Surface/cloud displacement, also scaled by motion. |
| `AfterimageStrength` | `0.625` | Delayed screen echoes, also scaled by motion. |
| `MaterialColorStrength` | `0.45` | Independent slow color breathing of opaque world materials; zero restores baseline colors. |
| `HallucinationStrength` | `0.55` | Character opacity; zero hides figures. |
| `AudioStrength` | `0.35` | Occasional faint echoes; zero disables added processing. |
| `OutlineStrength` | `0.70` | Defined edges and sharpening. |
| `TextBlurStrength` | `0.375` | Softness of gameplay HUD text. |
| `WorldVisuals` | `true` | Enables character hallucinations. |
| `GhostModelNPC` | `""` | Empty selects the nearest loaded visible NPC; otherwise use an NPC ID or full name. |
| `DurationSeconds` | `180` | Session length, bounded to 20–600 seconds. |

## Rendering and cleanup

Material discovery uses one camera-view ray per frame and a bounded local hierarchy search, without scanning the scene. It admits at most one material per frame, tracks at most 48 and updates at most eight per frame. New tints fade in over 0.8 seconds. Unseen materials fade back, and older entries can make room for newly viewed surfaces without a neighborhood boundary. Renderer candidates belonging to players or NPCs, transparent/cutout materials, and shaders without a supported base color are skipped. Meshes without usable colliders or with overriding material property blocks may not show the tint.

The tint is temporary runtime state on shared materials. Native or other-mod color changes become the new baseline. Cleanup restores only colors still matching the preview's last write, preserving newer external changes. Textures, shader settings and material assignments are untouched. Shared materials affect all their users; this is material-level variation, not a unique color field on every mesh.

Color/outline effects use an owned Volume profile. Ripples use an owned native psychedelic pass/material, leaving the mushroom feature's settings and URP camera target intact. Afterimages retain bounded GPU capture, a three-image history, UI sampling corrected for the graphics API's vertical origin, and no CPU readback. HUD softness uses owned font-material copies and restores only labels still using them.

Audio locates and caches an active listener using camera-rig checks and a listener-type fallback, adding only an owned [Unity audio filter](https://docs.unity.com/en-us/engine/6000.6/manual/audio/reference/filters/class-audio-effect). The non-generic [component lookup](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Component.GetComponent.html) and [component addition](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.AddComponent.html) avoid missing audio generic instantiations in the game. Sources [bypassing listener effects](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource-bypassListenerEffects.html) remain native. Menus/focus loss mute and disable the added echo immediately; cleanup destroys it. Character figures contain rendering components only, with no NPC behavior, colliders or networking. Renderer callbacks, owned resources and objects are released on reset, expiration, scene unload and deinitialization. No native methods are patched.

## Verification

The build script compiles against the installed IL2CPP assemblies with warnings treated as errors and runs pure logic checks for timing, gaze, transport/menu decisions, color gain bounds and continuity, native color ownership/rebasing, alpha/HDR preservation and bounded intermittent audio. The build also inspects compiled audio calls to reject generic Unity component lookup/addition regressions. Automated checks do not establish live appearance, collision coverage, actual audio routing or frame-time performance.

Live checks for 0.5.0:

1. Compare the same scene against 0.4.0. Ripple distortion should be far weaker while the subtle breathing remains. Inspect colorful objects, bright skies and indoor shadows for more vivid colors without distracting glare or crushed detail.
2. Inspect walls, roads and indoor surfaces at rest, then look away/back and approach them. Look for slight, unsynchronized color breathing without texture softness or abrupt coverage zones. Compare with `MaterialColorStrength=0`.
3. Inspect character hallucinations for retained clothing/skin variation, subtle fading, and no circles or aura helpers. Compare `HallucinationStrength=0.55` and `0`.
4. Listen to ambient sounds, footsteps and dialogue through a full preview, allowing time for fade-in. Check the log for `LSD echo filter attached to active listener` and `LSD audio echo is enabled`, with no audio initialization warning. Compare `AudioStrength=0` and `1` if the cue is hard to hear, then restore `0.35`. Confirm clean audio in menus and after F9. Sources bypassing listener effects will not change.
5. Check stronger HUD text softness, slightly stronger upright afterimages and natural expiration after three minutes. Test skateboard, car, menus, focus loss, death/load and repeated F8/F9; check restored material colors, fonts and audio, and no residual figures.
