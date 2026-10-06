# LSD visual preview 0.6.0

This separate `Personal.LSDPreview.dll` experiment explores the effect before adding an LSD product. F8 starts a 180-second preview or fades it out; F9 clears it immediately. It has no production, sales or save-data integration.

## This revision

The approved subtle lens breathing, vivid saturation/contrast, upright afterimages and faded native character meshes remain. The displacement range is calibrated against **the 0.5.0 default**, rather than its old maximum: new `1.00` is 10% stronger than that reference, and new default `0.30` is 30% of the new maximum. Thus the previous default sits at approximately 90.9% of the new range, and the new default amplitude is 33% of the previous default. Blend stays at the former default reference, making displacement linear across the new range. `MotionStrength` and overall intensity still apply.

An existing untouched `RippleStrength=0.50` migrates once to `0.30`. Other numeric custom settings are retained, though ripple values now use the new calibration. The original implementation and full old range are preserved in [baseline commit eb55537](https://github.com/Axl47/ScheduleOne-Mods/commit/eb55537) and the [future alcohol experiment note](https://github.com/Axl47/ScheduleOne-Mods/blob/main/docs/future-alcohol-preview.md). The alcohol experiment is a note only.

Effects and trip time continue through phone use, dialogue, typing, microphone/voice input restrictions, transport and focus loss **while gameplay is running**. Only an actual pause suspends the preview; loading, death, unconsciousness, player replacement or a change in consumed product clears it. Camera replacements retain trip progress. The mod does not change the game's background-running setting.

The patchy shared-material tint layer is removed. Slow RGB color breathing now joins the owned global color-adjustment pass, covering the entire rendered scene continuously. There are no camera rays, collider-dependent selection, material discovery, per-tile phases or writes to world materials. This trades separate material phases for reliable coverage; existing scene colors respond differently to the same gentle RGB gains. Saturation `56`, contrast `28 ± 4`, exposure `0.15` EV and subtle hue movement retain the 0.5.0 direction before overall intensity blending.

The open phone receives gentle color breathing on native UI graphics plus soft cyan/purple fringes around its text. Phone app changes are discovered immediately; active screen content is checked periodically. Dialogue text also keeps its softness. Label content, button geometry and hit targets remain native. `PhoneEffects=false` disables the added phone treatment. Phone graphic alpha is unchanged; newer native/theme color writes become the restoration baseline. Closing the phone or resetting restores owned colors and font materials.

The 0.5.0 live log confirmed that `AudioEchoFilter` has no native IL2CPP class in this game build. Non-generic component calls could not fix that stripped type. This version blends the game's existing normal and distorted **audio mixer snapshots**, which are already used by native product effects. The added weight swells intermittently, reaching at most 10.5% at the default peak and 50% at maximum audio strength. This is the game's distortion treatment; it is not a new 210 ms echo. Exact sound and audibility need a live check.

A postfix observes native `AudioManager.SetDistorted` requests without changing them. Native distortion takes precedence; a newer native transition invalidates the preview's cleanup claim. F9, expiration and pause restore the latest native state when the preview still owns the last contribution. Source volumes, pitch and routing are not edited. `AudioStrength=0` removes the added contribution. Logs identify the bound snapshots and a nonzero blend.

## Research guiding the direction

[Firsthand visual accounts](https://www.reddit.com/r/LSD/comments/cxloux/lsd_visuals/) describe brighter colors, breathing objects and existing texture details seeming to move. The community [drifting description](https://effectindex.com/effects/drifting) distinguishes breathing, morphing and texture flow. The design follows the requested subtle, recognizable scenery.

[Phone-screen accounts](https://www.reddit.com/r/LSD/comments/zwaog5/what_does_your_phone_screen_look_like_on_lsd/) mention brighter screens, moving letters and green/purple shadows around text and icons. [A discussion of screen-visual recreations](https://www.reddit.com/r/LSD/comments/1i9ps2e/attempt_to_recreate_the_visual_effect_when_you/) includes wavy letters, colored fringes and differing descriptions. These are anecdotal artistic references with substantial variation. The phone treatment uses restrained color and glyph-edge changes rather than moving clickable controls.

Auditory descriptions also vary. The community [auditory-distortion catalogue](https://effectindex.com/effects/auditory-distortion) includes fleeting reverberation and echoes. A small [2015 study by Kaelen and colleagues](https://pubmed.ncbi.nlm.nih.gov/26257162/) found stronger emotional responses to music in ten volunteers; it does not establish an echo effect. Audio and character apparitions remain artistic choices for this game experiment.

## Install or update

1. Close the game. From `LSDPreview-0.6.0.zip`, copy **only** `Mods/Personal.LSDPreview.dll` into the game's `Mods` folder, replacing the previous preview DLL.
2. Restart and load a save. Begin near a visible NPC so an appearance can be captured. Native camera post-processing must be enabled.
3. Press **F8** to start or fade out over two seconds. Press **F9** to clear immediately, including during phone use and dialogue.

The build script packages the experiment without installing it or launching the game. Previous packages remain locally available for comparison. The internal `DefaultsRevision=3` upgrades untouched defaults once and preserves later manual edits.

## Tuning

Edit `UserData/MelonPreferences.cfg`, category `PersonalLSDPreview`, while the game is closed. Strength values accept 0–1.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Enabled` | `true` | Manual F8 preview; nothing starts automatically. |
| `Intensity` | `0.60` | Overall strength. |
| `MotionStrength` | `0.50` | Lens breathing, color separation, ripples and afterimages; zero disables these four. |
| `RippleStrength` | `0.30` | New displacement range; maximum is 110% of the 0.5.0 default. |
| `AfterimageStrength` | `0.625` | Delayed screen images, also scaled by motion. |
| `MaterialColorStrength` | `0.45` | Continuous scene color breathing and phone color movement. Name retained for configuration compatibility. |
| `HallucinationStrength` | `0.55` | Character opacity; zero hides figures. |
| `AudioStrength` | `0.35` | Intermittent native distorted-snapshot blend; zero restores native audio. |
| `OutlineStrength` | `0.70` | Defined edges and sharpening. |
| `TextBlurStrength` | `0.375` | HUD/dialogue softness and phone glyph fringes. |
| `PhoneEffects` | `true` | Additional treatment of the phone screen. |
| `WorldVisuals` | `true` | Enables character hallucinations. |
| `GhostModelNPC` | `""` | Empty selects a nearby visible loaded NPC; otherwise use an NPC ID or full name. |
| `DurationSeconds` | `180` | Session length, bounded to 20–600 seconds. |

## Rendering and cleanup

Color/outline effects use an owned Volume profile. Ripples use an owned native psychedelic pass/material, leaving the mushroom feature's settings and URP camera target intact. Afterimages keep bounded GPU capture, three history images, sampling corrected for the graphics API's vertical origin, and no CPU readback. Text softness/fringes use owned font-material copies and restore only labels still using them. Phone colors track at most 160 graphics, refresh at most every 0.75 seconds and animate at 20 Hz; font effects track at most 192 labels across HUD, dialogue and phone.

Audio uses Unity's [weighted snapshot transition](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Audio.AudioMixer.TransitionToSnapshots.html), updating at most four times per second with a smooth transition. Native snapshot changes can trigger an immediate resynchronization. The observer hook is required for safe restoration; if it cannot be installed, the added audio layer stays inactive. The game still controls native product audio and any sources outside that mixer.

At most two character figures appear, at least five seconds apart, and fade under direct attention. They retain source clothing/skin tint distinctions with slight desaturation and contain rendering components only. Capture excludes disabled renderers, particles and aura/ring/effect helpers. Owned resources and renderer callbacks are released on reset, expiration, scene unload and deinitialization.

## Verification

The build compiles against the installed IL2CPP assemblies with warnings treated as errors. Pure logic checks cover timing, gaze, all focus/input/menu/transport visibility combinations, the displacement calibration at every sampled wave phase, color gain bounds/continuity, alpha/HDR preservation and restoration after external writes, plus audio envelope and native-transition ownership. Compiled audio references must contain the snapshot blend and must not reference the stripped echo filter. These checks do not establish live appearance, actual audio routing or frame-time performance.

Live checks for 0.6.0:

1. Compare the same scene against 0.5.0. Default ripple amplitude should be about one third; `RippleStrength=1` should be just above the previous default. Lens breathing should retain its subtle timing.
2. Inspect the road/tile boundary from the reported screenshot, indoor floors and distant objects. Color movement should be continuous across material boundaries and should not appear when approaching a discovery radius. Compare `MaterialColorStrength=0`.
3. Open the phone, switch apps, type and talk. Check gentle colored glyph fringes, responsive controls and uninterrupted world effects. Close/reopen; press F9 and confirm restored colors/fonts. Compare `PhoneEffects=false`.
4. Alt-tab while the game continues, then return; trip time should advance. Pause explicitly and confirm it suspends. Test skateboard, car, dialogue and voice input.
5. Listen to footsteps, ambience and dialogue through a full preview. Check for `LSD audio bound native snapshots` and `LSD audio is blending the native distorted mixer snapshot`, without a stripped-filter warning. Compare `AudioStrength=0` and `1`, then restore `0.35`. Verify normal audio after F9 and native product distortion taking precedence.
6. Confirm upright afterimages, faded figures, expiration after three minutes and clean repeated F8/F9, death/load and scene cleanup.
