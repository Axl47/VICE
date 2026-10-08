# LSD visual preview 0.12.1

This module supplies the visual/audio treatment for the [Off Register workshop](lsd-workshop.md) and remains independently usable as a manual preview. It registers no product itself. Consuming a workshop tab starts a treatment; another tab refreshes it without stacking. **F8** starts a three-minute manual preview or requests a short fade-out. **F9** immediately clears it outside a workshop interaction. Inside workshop controls it saves the drawing or skips the current production stage for testing.

## What changed

Version 0.12.1 increases screen ripple displacement and lens breathing by another **25% over 0.12.0**. `RippleStrength` rises from `0.375` to `0.46875`; defaults revision 6 upgrades the previous default once and preserves custom strengths. At the stationary default peak, displacement is bounded to **5.90625–8.4375 pixels**, and lens amplitude before Volume blending is **0.0859375**. The absolute 18-pixel displacement and 0.11 lens caps, audio, closed-eye patterns and all other treatment settings stay unchanged.

Version 0.12.0 implements the first [closed-eye patterns](future-closed-eye-patterns.md) pass. During an active manual or workshop-triggered treatment, **hold B** to close the eyes over 0.85 seconds and reveal soft, localized circles, spirals and lattices emerging from darkness. **Release B** to reopen over 0.45 seconds. The existing open-eye parameters, preferences migration and owned echo route remain unchanged.

Patterns expand and brighten with slow breathing. Brightness follows the trip envelope, overall intensity and the new `ClosedEyeStrength=0.55`; animation speed follows `MotionStrength`. At zero motion the patterns stay still. Zero closed-eye strength keeps closure dark without patterns; zero overall intensity disables closure. Full closure replaces scene detail with a near-black background. It does not stop movement or change game input bindings.

Phone, dialogue, typing, cursor-visible menus, camera UI, blocked look input, pause and focus loss immediately reopen the eyes. Release B before holding again after an interruption. F8 Stop also clears closure immediately while the existing treatment fades out; F9, expiration, loading, death, scene unload and shutdown discard closure state. Camera replacement disposes the previous GPU resources and retains the held-key transition. The echo wet return now follows the smooth eye-closure transition: fully closed eyes make it **45% stronger**, and reopening restores the open-eye level. UI interruptions and Stop immediately remove the boost.

Version 0.11.2 reserves F9 for open workshop controls and preserves the treatment parameters. Version 0.11.1 adds the native workshop-consumption trigger and keeps all 0.11.0 treatment parameters unchanged. It recognizes only the workshop's stable tab IDs and clears/restarts the existing owned effect instance.

The owner approved 0.10.1's subtle motion and audible echo as a baseline. Its fixed source commit, installed DLL hash, captured preference section and implementation formulas are recorded in the [approved baseline](lsd-baseline-0.10.1.md). The old package and an additional DLL/preferences backup remain available locally.

The 0.11.0 trial raised default echo return, ripple displacement and lens breathing by **25%**, then added independently adjustable reading-focus effects. Version 0.12.1 adds a further 25% to the ripple/lens strengths. Overall intensity, afterimages, colors, character sizes, timing, movement smoothing and the upright presentation remain as approved.

| Layer | Approved 0.10.1 | 0.11.0–0.12.0 | 0.12.1 |
| --- | --- | --- | --- |
| RippleStrength | 0.30 | 0.375 | 0.46875 |
| Stationary peak displacement bound | 3.78–5.4 px | 4.725–6.75 px | 5.90625–8.4375 px |
| Lens amplitude before Volume blend | 0.055 | 0.06875 | 0.0859375 |
| AudioStrength | 0.35 | 0.4375 | 0.4375 |
| Peak echo return | 23.52–29.4% | 29.4–36.75% | 29.4–36.75% |
| TextFocusStrength | absent | 0.50; 0 retains earlier treatment | 0.50 |

### Completed-frame breathing and ripples

Flow captures the completed rendered frame on the GPU, warps it into a second owned texture with explicit texture coordinates, and presents it with Unity's ordinary texture blit. Presentation explicitly reverses the captured texture's top-origin rows on Direct3D, once. The custom shader no longer draws directly to the backbuffer or reads the camera-global `_FlipY`. This separates texture processing from screen presentation. The frame includes the phone when present; its layout and input targets remain intact.

The approved 0.10.1 field uses 70% uneven broad/detail flow, 20% small ripples around a slowly drifting origin, and 10% gentle radial breathing. The same field is used at greater displacement. Both horizontal and vertical texture details move across the image. The proportions sum to one and each field is bounded, preserving the absolute pixel limit. They do not add visible geometric overlays or change scene materials. The separate native zoom-like breathing receives the same proportional increase.

At stationary default settings (`Intensity=0.60`, `RippleStrength=0.46875`, `MotionStrength=0.50`), the flow's maximum vector displacement varies between **5.90625 and 8.4375 pixels**, exactly 25% above 0.12.0 and 56.25% above the approved 0.10.1 bound. The absolute flow bound remains 18 pixels. The bound is an upper limit, not the displacement at every pixel. Normal strength is 46.875% of the corresponding full ripple range. `CenterClarityDegrees` is retained for compatibility and future experiments but does not mask the default path.

The owned lens profile uses `min(0.11, 0.11 * RippleStrength / 0.30 * MotionStrength) * movementComfort * sin(0.65 * elapsed)`, before overall intensity blending. Its 0.30 calibration stays fixed when the default rises to 0.46875; otherwise normalization would cancel the requested increase. Both axis multipliers remain enabled. Motion and ripple strength still control it. Shader breathing is independent of the native lens layer.

Moving or turning smoothly reduces both displacement layers. At four metres per second, strength approaches 62.5% of stationary strength, with a 25% floor during rapid travel. Standing still restores the stationary effect. The added vignette remains removed. Vivid colors, scene color breathing, outlines, upright afterimages and the approved text settings are retained.

### Echo on original audio buses

The preview inserts owned native **Echo mixers after the game's main, music and menu buses**. Existing music, ambience, pooled footsteps, impacts, vehicle sounds and UI audio pass through this DSP directly. There are no copied clips, source-selection limits, cursor seeking, source scans, microphone capture or playback detours. Nested native buses are processed only at their terminal output to avoid doubling the effect.

The owned mixers preserve the original dry signal at unity gain. They use a **220 ms delay**, decay ratio **0.18**, and a wet return that slowly varies between **29.4% and 36.75%** at default peak settings. Closing the eyes ramps the wet return up by **45%**, to **42.63–53.2875%** at default peak settings, then ramps it back down when they reopen. The absolute 90% wet cap still applies at high custom strengths, and zero AudioStrength stays silent. Delay, decay, dry gain, pulse timing and routing remain approved. Original source settings, game mixer snapshots, category volumes, spatial attenuation and master mute remain under game control. This uses Unity's [native Echo mixer effect](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioEchoEffect.html); its serialized type and parameter order were checked against the game's existing Police chatter mixer.

Each native output connection is leased and its previous destination retained. Zero audio strength, pause, listener mute, F9, expiration and cleanup mute the owned wet return and restore connections still owned by the preview. No audio objects or callbacks are initialized during normal startup. The 0.8.1 startup repair remains in place.

### Ordinary-sized character hallucinations

The previous capture divided renderer scale by avatar-root scale, then built geometry beneath a unit-scale root. That discarded imported model scale and could turn a normal character into a huge figure. Snapshots now retain renderer world scale and enter a rotation-only avatar frame. Combined transformed mesh bounds align the feet with the ground. A final size guard preserves normal captures and fits unexpected import/bake sizes into a 1.25–2.1 metre height range.

Only rendering geometry is copied. NPC logic, collisions, audio, particles, aura and ring helpers are excluded. Clothing/skin colors, low opacity, at most two figures, spawn spacing and fading under direct attention remain as before.

### Reading focus

Instead of rewriting words, the trial briefly makes their strokes harder to resolve. HUD, dialogue and phone labels receive independently phased bursts of faint offset glyph shadows; signed-distance fonts also vary stroke weight slightly and soften edges. Clearer intervals let the eye recover. Native words, character order, numbers, choices, font sizes, spacing, layout and button targets remain intact. This avoids maintaining alternate dialogue/localizations or changing the meaning of a task or purchase.

`TextFocusStrength=0.50` is blended by the trip envelope and overall intensity. The maximum default added shadow alpha is 0.225, SDF offsets are bounded to 0.27 horizontally and 0.114 vertically in shader units, and additional face dilation to ±0.0135. Legacy shadows use the same timing with small pixel offsets. The effect uses owned font copies/Shadow components, reserves padding once, animates at up to 20Hz and tracks at most 192 combined labels. F9, pause, screen changes and cleanup restore still-owned effects. Native text color and content remain intact.

The original `TextBlurStrength` stays independent. `TextFocusStrength=0` removes the new treatment. Text legibility and the appearance of loaded dialogue/phone screens remain playtest judgments.

## Install and use

1. Close the game and replace only `Mods/Personal.LSDPreview.dll` from `LSDPreview-0.12.1.zip`. The preview also works with the existing workshop DLL. Keep backups outside `Mods`.
2. Restart, load a save and press F8. Allow roughly 30 seconds for the rising phase to reach its peak.
3. Stand still near a shop wall, window frame or sign and watch several breathing cycles. Then compare F9. Test music at the gas station/shop and footsteps on a quiet street.
4. Open the phone, talk to an NPC, move and ride. Effects continue while gameplay runs; only a real pause suspends them. Loading, death, unconsciousness, player/product replacement or scene cleanup clears the preview.
5. During the trip, hold B while standing still, then release it. Compare `ClosedEyeStrength=0.2` and `0.8`, and test phone, dialogue, typing, pause, movement and F8/F9 while B is held. After a UI interruption, release B before trying again.

Preferences remain in `UserData/MelonPreferences.cfg`, section `[PersonalLSDPreview]`. Defaults revision 6 upgrades unchanged `RippleStrength=0.375` to `0.46875` once and retains custom values; earlier revision 5 upgraded the original ripple/audio defaults. To replay the approved strengths after upgrading, set `RippleStrength=0.30`, `AudioStrength=0.35`, `TextFocusStrength=0` and keep `DefaultsRevision=6` so deliberate settings are not upgraded again. Set `RippleStrength=0.375` with revision 6 to return to the 0.12.0 distortion strength. Exact historical playback is documented in the [baseline](lsd-baseline-0.10.1.md).

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Allow the manual preview. |
| `Intensity` | `0.60` | Overall visual strength. |
| `MotionStrength` | `0.50` | Flow, breathing, color separation and afterimages; zero disables motion. |
| `RippleStrength` | `0.46875` | Flow/lens strength; zero disables both. |
| `CenterClarityDegrees` | `5` | Reserved; default flow has no gaze mask. |
| `AfterimageStrength` | `0.625` | Upright delayed images, also scaled by motion. |
| `MaterialColorStrength` | `0.45` | Continuous scene/phone RGB breathing. |
| `HallucinationStrength` | `0.55` | Character opacity. |
| `AudioStrength` | `0.4375` | Direct native-bus echo; zero restores routing. |
| `OutlineStrength` | `0.70` | Defined edges and sharpening. |
| `TextBlurStrength` | `0.375` | HUD/dialogue softness and phone glyph fringes. |
| `TextFocusStrength` | `0.50` | Intermittent drifting glyph shadows/stroke softening; 0 retains earlier treatment. |
| `ClosedEyeStrength` | `0.55` | Brightness of held-B closed-eye patterns; 0 gives darkness only. |
| `PhoneEffects` | `true` | Additional phone colors and glyph treatment. |
| `WorldVisuals` | `true` | Faded character figures. |
| `GhostModelNPC` | `""` | Empty selects a nearby loaded NPC; otherwise ID/full name. |
| `DurationSeconds` | `180` | Duration, bounded to 20–600 seconds. |

The completed-frame vision pass also affects the phone's rendered image; `PhoneEffects` controls its additional targeted colors/text treatment. Button hit targets and layouts are unchanged.

## Resources and build

Normal flow uses two full-resolution GPU textures (capture and warped output) and an owned material, drawn from a `WaitForEndOfFrame` coroutine after cameras and UI have rendered. It performs no CPU image readback, tile/material scan or texture-pattern projection. The source frame is rendered fresh before each capture, so the warp does not feed itself back. Textures resize with the screen; the coroutine exits and textures/materials are released on cleanup. The extra GPU texture/copy replaces the direct backbuffer draw. GPU cost in the loaded city remains a playtest measurement.

Closed-eye patterns share that shader, material and texture pair. A separate branch runs only during closure; reopening restores the existing flow branch. It also renders when ripple strength is zero, with no extra pattern textures, UI canvases, source-material scanning or per-frame object discovery. Since the completed-frame pass includes HUD pixels, closure is immediately suppressed during UI interaction rather than obscuring interactive screens.

Afterimages retain three bounded history images and corrected vertical sampling. Phone colors/glyph effects retain bounded trackers and restore values only while still owned. Color and outline effects use an owned Volume profile. No native meshes or source materials are modified by the character snapshots.

The checked-in shader and Echo bundles let `scripts/Build-LSDPreview.ps1` build and package without a Unity editor or Python. The shader contains authored executable programs; the Echo bundle contains only four owned mixer/group/snapshot sets and their container, with no audio clips, meshes or textures. Build checks verify file and embedded-resource hashes. Regeneration uses `scripts/Build-LSDShader.py` or `scripts/Build-LSDEcho.py` with the local UnityPy tools and the installed game's serialization schema.

The build compiles against installed IL2CPP assemblies with warnings treated as errors. Pure checks cover timing, visibility/input combinations, flow bounds, movement comfort, color ownership, character size guards, open-eye echo wet gain, the 45% closed-eye echo boost and its transitions/cap, and the exact 25% ripple/lens increase over 0.12.0. Compiled guards reject normal-path source scans/replays, listener-history/PCM writes, stripped echo filters, mixer snapshot replacement, startup audio initialization and playback detours. They also reject wording, spacing or layout writes from the reading-focus layer. Diagnostic-only tone generation and image/audio readback are intentionally separate.

## Verification and playtest

Version 0.12.0 built with zero warnings/errors and passed **133,262 pure assertions**, including all 256 closed-eye eligibility combinations, held/released transition timing, interruption rearming, invalid frame times, cleanup and frame-rate independence. Compiled held-B, existing audio/vision/text guards and embedded asset hashes passed. The optional `--lsd-render-check` diagnostic additionally verifies dark localized motion, zero-pattern darkness, zero-motion stillness and exact reopened zero-flow identity. These new GPU diagnostic checks and loaded-save appearance have not yet been run in the game; the earlier measurements below apply to their named versions.

Normal startup must reach Menu and log the scene-loaded/main-loop heartbeat without starting effects. Explicit `--lsd-render-check` tests the normal shader binding/draw method against a controlled offscreen image, including motion outside the central half of the screen. It then compares the actual screen before/after drawing through two identical screenshot captures, including zero-strength identity. The former comparison between a captured texture and direct backbuffer `ReadPixels` mixed coordinate conventions and missed the visible flip; its orientation result is superseded. The screen check needs a visible game window: a hidden launch cannot establish visible motion. Explicit `--lsd-audio-check` sends two short owned diagnostic tones through the original music bus, first with echo off and then at default strength. It measures the delayed signal in final listener output and verifies original routing restoration. When both flags are supplied, the image check finishes before audio sampling begins. Normal launches run neither diagnostic; no save is loaded by either check.

The visible **0.11.0 menu check on October 6 at 22:13** passed with **zero changed pixels at zero strength** and **34,388 changed final-screen pixels at the new default**. The controlled 1280×720 shader check passed with zero identity errors and 292,564 time-varying pixels, including **226,497 outside the central half-width/half-height rectangle**. The final-listener audio check preserved nearly identical dry amplitudes (`0.088910`/`0.088946`), while the 220 ms tail rose from `0.000357` with echo off to `0.023896` with the stronger default echo. All three native bus routes restored successfully. No save was loaded. These checks establish screen orientation, local motion and audible echo; new text legibility and city balance remain gameplay checks.

The preserved 0.10.1 baseline menu check at 21:26 had zero-strength identity, 32,978 changed final-screen pixels and 199,843 time-varying pixels outside the central rectangle. Its subsequent gameplay report approved the subtle direction. The 0.11.0 pure/build checks passed 132,983 assertions, including the exact 25% default ripple, lens-breathing and wet-return ratios; compiled checks preserve the normal audio/vision path and reject text-content/layout changes.

The unchanged 0.10.0 audio implementation's default-strength final-listener check measured nearly identical dry tone amplitudes (`0.088909`/`0.088941`) and a 220 ms tail rising from `0.000357` with echo off to `0.018971` with echo on. All three original audio output routes were restored. The owner's subsequent gameplay report confirmed that the echo is audible and the current audio direction works well. No audio sources, mixer settings or echo code changed in 0.10.1.

Loaded-save appearance remains a manual check: watch a stationary wall/sign, look at an NPC face, open the phone, then move/turn/ride and compare F9. Check that figures resemble adult NPC size, motion stays subtle, text/controls remain usable, colors do not become patchy and cleanup is immediate. Inspect the `LSD completed-frame flow rendered`, `LSD direct bus echo` and `LSD character size` lines if a layer is missing. Repeated F8/F9, pause/mute/resume, screen resizing and scene changes should not accumulate resources or effects.

The strong original 0.5.0 displacement remains preserved in [baseline commit eb55537](https://github.com/Axl47/ScheduleOne-Mods/commit/eb55537) and the [future alcohol note](future-alcohol-preview.md). Alcohol remains a future note only.

## Artistic references

[Firsthand visual accounts](https://www.reddit.com/r/LSD/comments/cxloux/lsd_visuals/) and the community [drifting description](https://effectindex.com/effects/drifting) informed the restrained breathing/color direction. [Phone-screen accounts](https://www.reddit.com/r/LSD/comments/zwaog5/what_does_your_phone_screen_look_like_on_lsd/) and [screen-visual recreation discussions](https://www.reddit.com/r/LSD/comments/1i9ps2e/attempt_to_recreate_the_visual_effect_when_you/) informed colored glyph fringes. These are varied anecdotal artistic references.

The community [auditory-distortion catalogue](https://effectindex.com/effects/auditory-distortion) includes fleeting reverberation and echoes. A small [2015 study by Kaelen and colleagues](https://pubmed.ncbi.nlm.nih.gov/26257162/) concerned emotional responses to music; it does not establish echo as a typical effect. Audio and character apparitions remain artistic choices for this game experiment.
