# LSD visual preview 0.10.0

This is a separate manual visual/audio experiment. It registers no product and changes no production, sales or save data. **F8** starts a three-minute preview or requests a short fade-out. **F9** immediately clears it.

## What changed

The 0.9.0 playtest still showed little visible breathing or audible echo, despite positive material/source logs. Those logs did not establish what reached the display or speakers. This revision checks the final paths and fixes character sizing.

### Completed-frame breathing and ripples

Flow now captures the completed rendered frame on the GPU and draws the authored flowing shader onto the final screen image. It no longer depends on an early camera-color target or the experimental gaze mask. The same slight radial expansion/contraction and flowing detail reach the whole image, including the phone when present in that frame. The center naturally moves less during radial breathing, while small flowing offsets remain possible there. This preserves phone layout and input targets; graphics move by only a few pixels at normal strength.

At stationary default settings (`Intensity=0.60`, `RippleStrength=0.30`, `MotionStrength=0.50`), the flow's maximum vector displacement varies between **3.78 and 5.4 pixels**. The absolute flow bound remains 18 pixels. The bound is an upper limit, not the displacement at every pixel. Normal strength remains 30% of the corresponding full ripple range. `CenterClarityDegrees` is retained for compatibility and future experiments but does not mask the default path.

The owned lens-breathing profile also restores the earlier coefficient: `0.11 * MotionStrength` at default ripple strength, before overall intensity blending. Both axis multipliers are explicitly enabled, so native profile values cannot disable that lens layer. Motion and ripple strength still control it. The slow shader breathing is independent of whether the native lens layer appears.

Moving or turning smoothly reduces both displacement layers. At four metres per second, strength approaches 62.5% of stationary strength, with a 25% floor during rapid travel. Standing still restores the stationary effect. The added vignette remains removed. Vivid colors, scene color breathing, outlines, upright afterimages and the approved text settings are retained.

### Echo on original audio buses

The preview inserts owned native **Echo mixers after the game's main, music and menu buses**. Existing music, ambience, pooled footsteps, impacts, vehicle sounds and UI audio pass through this DSP directly. There are no copied clips, source-selection limits, cursor seeking, source scans, microphone capture or playback detours. Nested native buses are processed only at their terminal output to avoid doubling the effect.

The owned mixers preserve the original dry signal at unity gain. They use a **220 ms delay**, decay ratio **0.18**, and a wet return that slowly varies between **23.5% and 29.4%** at default peak settings. Original source settings, game mixer snapshots, category volumes, spatial attenuation and master mute remain under game control. This uses Unity's [native Echo mixer effect](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioEchoEffect.html); its serialized type and parameter order were checked against the game's existing Police chatter mixer.

Each native output connection is leased and its previous destination retained. Zero audio strength, pause, listener mute, F9, expiration and cleanup mute the owned wet return and restore connections still owned by the preview. No audio objects or callbacks are initialized during normal startup. The 0.8.1 startup repair remains in place.

### Ordinary-sized character hallucinations

The previous capture divided renderer scale by avatar-root scale, then built geometry beneath a unit-scale root. That discarded imported model scale and could turn a normal character into a huge figure. Snapshots now retain renderer world scale and enter a rotation-only avatar frame. Combined transformed mesh bounds align the feet with the ground. A final size guard preserves normal captures and fits unexpected import/bake sizes into a 1.25–2.1 metre height range.

Only rendering geometry is copied. NPC logic, collisions, audio, particles, aura and ring helpers are excluded. Clothing/skin colors, low opacity, at most two figures, spawn spacing and fading under direct attention remain as before.

## Install and use

1. Close the game and replace only `Mods/Personal.LSDPreview.dll` from `LSDPreview-0.10.0.zip`. Keep backups outside `Mods`.
2. Restart, load a save and press F8. Allow roughly 30 seconds for the rising phase to reach its peak.
3. Stand still near a shop wall, window frame or sign and watch several breathing cycles. Then compare F9. Test music at the gas station/shop and footsteps on a quiet street.
4. Open the phone, talk to an NPC, move and ride. Effects continue while gameplay runs; only a real pause suspends them. Loading, death, unconsciousness, player/product replacement or scene cleanup clears the preview.

Preferences remain in `UserData/MelonPreferences.cfg`, section `[PersonalLSDPreview]`. Existing custom values are preserved.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Allow the manual preview. |
| `Intensity` | `0.60` | Overall visual strength. |
| `MotionStrength` | `0.50` | Flow, breathing, color separation and afterimages; zero disables motion. |
| `RippleStrength` | `0.30` | Flow/lens strength; zero disables both. |
| `CenterClarityDegrees` | `5` | Reserved; default flow has no gaze mask. |
| `AfterimageStrength` | `0.625` | Upright delayed images, also scaled by motion. |
| `MaterialColorStrength` | `0.45` | Continuous scene/phone RGB breathing. |
| `HallucinationStrength` | `0.55` | Character opacity. |
| `AudioStrength` | `0.35` | Direct native-bus echo; zero restores routing. |
| `OutlineStrength` | `0.70` | Defined edges and sharpening. |
| `TextBlurStrength` | `0.375` | HUD/dialogue softness and phone glyph fringes. |
| `PhoneEffects` | `true` | Additional phone colors and glyph treatment. |
| `WorldVisuals` | `true` | Faded character figures. |
| `GhostModelNPC` | `""` | Empty selects a nearby loaded NPC; otherwise ID/full name. |
| `DurationSeconds` | `180` | Duration, bounded to 20–600 seconds. |

The completed-frame vision pass also affects the phone's rendered image; `PhoneEffects` controls its additional targeted colors/text treatment. Button hit targets and layouts are unchanged.

## Resources and build

Normal flow uses one full-resolution GPU capture and an owned material, drawn from a `WaitForEndOfFrame` coroutine after cameras and UI have rendered. It performs no CPU image readback, tile/material scan or texture-pattern projection. The source frame is rendered fresh before each capture, so the warp does not feed itself back. Captures resize with the screen; the coroutine exits and textures/materials are released on cleanup. GPU cost in the loaded city remains a playtest measurement.

Afterimages retain three bounded history images and corrected vertical sampling. Phone colors/glyph effects retain bounded trackers and restore values only while still owned. Color and outline effects use an owned Volume profile. No native meshes or source materials are modified by the character snapshots.

The checked-in shader and Echo bundles let `scripts/Build-LSDPreview.ps1` build and package without a Unity editor or Python. The shader contains authored executable programs; the Echo bundle contains only four owned mixer/group/snapshot sets and their container, with no audio clips, meshes or textures. Build checks verify file and embedded-resource hashes. Regeneration uses `scripts/Build-LSDShader.py` or `scripts/Build-LSDEcho.py` with the local UnityPy tools and the installed game's serialization schema.

The build compiles against installed IL2CPP assemblies with warnings treated as errors. Pure checks cover timing, visibility/input combinations, flow bounds, movement comfort, color ownership, character size guards and echo wet gain. Compiled guards reject normal-path source scans/replays, listener-history/PCM writes, stripped echo filters, mixer snapshot replacement, startup audio initialization and playback detours. Diagnostic-only tone generation and image/audio readback are intentionally separate.

## Verification and playtest

Normal startup must reach Menu and log the scene-loaded/main-loop heartbeat without starting effects. Explicit `--lsd-render-check` tests the normal shader binding/draw method against a controlled offscreen image, then compares the actual screen before/after drawing, including zero-strength identity. The screen check needs a visible game window: the hidden launch produced an empty capture and cannot establish visible motion. Explicit `--lsd-audio-check` sends two short owned diagnostic tones through the original music bus, first with echo off and then at default strength. It measures the delayed signal in final listener output and verifies original routing restoration. When both flags are supplied, the image check finishes before audio sampling begins. Normal launches run neither diagnostic; no save is loaded by either check.

The visible menu check on October 6 at 20:40 passed with **zero changed pixels at zero strength**, then **706,052 of 3,686,400 final-screen pixels changed at default strength**. The same draw method also passed the controlled 1280×720 image test with zero identity errors and 320,363 pixels varying with animation time. These establish an actual screen draw, correct orientation/color at zero strength and animated shader output; city appearance and GPU cost still need a gameplay comparison.

The following default-strength final-listener check measured nearly identical dry tone amplitudes (`0.088909`/`0.088941`) and a 220 ms tail rising from `0.000357` with echo off to `0.018971` with echo on. All three original audio output routes were restored. This establishes a delayed signal in the final mix, beyond the earlier copied-source probes. Sound quality and its balance against the particular shop track still need a gameplay comparison.

Loaded-save appearance remains a manual check: watch a stationary wall/sign, look at an NPC face, open the phone, then move/turn/ride and compare F9. Check that figures resemble adult NPC size, motion stays subtle, text/controls remain usable, colors do not become patchy and cleanup is immediate. Inspect the `LSD completed-frame flow rendered`, `LSD direct bus echo` and `LSD character size` lines if a layer is missing. Repeated F8/F9, pause/mute/resume, screen resizing and scene changes should not accumulate resources or effects.

The strong original 0.5.0 displacement remains preserved in [baseline commit eb55537](https://github.com/Axl47/ScheduleOne-Mods/commit/eb55537) and the [future alcohol note](future-alcohol-preview.md). Alcohol remains a future note only.

## Artistic references

[Firsthand visual accounts](https://www.reddit.com/r/LSD/comments/cxloux/lsd_visuals/) and the community [drifting description](https://effectindex.com/effects/drifting) informed the restrained breathing/color direction. [Phone-screen accounts](https://www.reddit.com/r/LSD/comments/zwaog5/what_does_your_phone_screen_look_like_on_lsd/) and [screen-visual recreation discussions](https://www.reddit.com/r/LSD/comments/1i9ps2e/attempt_to_recreate_the_visual_effect_when_you/) informed colored glyph fringes. These are varied anecdotal artistic references.

The community [auditory-distortion catalogue](https://effectindex.com/effects/auditory-distortion) includes fleeting reverberation and echoes. A small [2015 study by Kaelen and colleagues](https://pubmed.ncbi.nlm.nih.gov/26257162/) concerned emotional responses to music; it does not establish echo as a typical effect. Audio and character apparitions remain artistic choices for this game experiment.
