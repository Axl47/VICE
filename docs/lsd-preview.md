# LSD visual preview 0.7.0

This separate `Personal.LSDPreview.dll` experiment explores perception before adding an LSD product. F8 starts a 180-second preview or fades it out; F9 clears it immediately. No production, sales or save-data integration is added.

## This revision

The large bends had a concrete cause: the native shader's `_Amplitude` changes how its noise pans, while `_Blend` controls the actual scene displacement. Earlier amplitude reductions and the 0.6.0 range calculation therefore did not reduce bending as intended. This revision replaces the owned pass's shader with authored, pixel-limited peripheral flow. The game's original mushroom feature remains independent.

At the current defaults, the added displacement is bounded to **0.378–0.54 pixels**, before the focus mask and movement reduction. Even with all strengths at one, its vector length cannot exceed six pixels. A **12-degree radius around the gaze center has exactly zero added displacement**, followed by a gradual transition toward the periphery. The radius accounts for camera field of view and aspect ratio. Edges fade to zero to avoid stretched border pixels. Slow breathing changes the small displacement continuously.

The old broad lens distortion is removed because it would bend the protected center. Color breathing, saturation/contrast, outlines, upright afterimages and faded character meshes retain their previous settings. Moving or turning smoothly reduces bending further; standing still restores its subtle strength. At four metres per second it approaches 62.5% of stationary strength; rapid travel approaches a floor of 25%. The crouch vignette targets twice the native crouch intensity, capped at one and blended through overall preview intensity. It stops overriding immediately when standing.

The phone home screen is a sibling of `AppsCanvas`; the old raycaster-based selection missed it. Phone treatment now covers active graphics beneath `phoneModel`, including the home screen and apps. Most phone labels use legacy `UnityEngine.UI.Text`, so they now receive owned, softly moving cyan/purple glyph shadows. TMP labels retain their owned soft font materials. Button positions, layout and hit targets remain native. Closing the phone, switching apps or pressing F9 removes the owned glyph effects and restores eligible colors/materials.

The 0.6.0 log confirmed that mixer snapshot blending ran, but the user heard no echo. The native effect is reverb, and the Unity `AudioEchoFilter` component is stripped from this build. This revision instead captures actual listener output and replays it through one quiet owned stereo source, with a **nominal 220 ms delay**. It does not replace native mixer snapshots or edit game sources. At default peak strength, the return varies from about 2.2% to 7.35% of captured output; the absolute gain cap is 35%, keeping repeated feedback decaying.

Unity's [listener-output API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioListener.GetOutputData.html) exposes a recent analysis history, with an initial warm-up, rather than a guaranteed chronological low-latency stream. This echo is an experimental main-thread approximation: actual delay and continuity depend on device buffering and frame timing. It uses reusable sample buffers and [runtime clip writes](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioClip.SetData.html). Long stalls discard pending audio and clear the owned loop. Pause, mute, zero strength and F9 silence and clear it; resume starts fresh. No microphone capture or audio files are used.

Effects and trip time continue through phone use, dialogue, typing, voice input restrictions, transport and focus loss while gameplay runs. Only an actual pause suspends the preview. Loading, death, unconsciousness, player replacement or a change in consumed product clears it. Camera replacements retain trip progress. The game still controls whether it runs in the background.

Continuous global RGB color breathing remains in place, without material discovery, camera rays or per-tile writes. Saturation `56`, contrast `28 ± 4`, exposure `0.15` EV and subtle hue movement retain the approved direction before overall intensity blending. The original 0.5.0 displacement formulas and implementation remain preserved in [baseline commit eb55537](https://github.com/Axl47/ScheduleOne-Mods/commit/eb55537) and the [future alcohol note](https://github.com/Axl47/ScheduleOne-Mods/blob/main/docs/future-alcohol-preview.md). Alcohol remains a future note only.

## Install or update

1. Close the game. From `LSDPreview-0.7.0.zip`, copy only `Mods/Personal.LSDPreview.dll` into the game's `Mods` folder, replacing the previous preview DLL.
2. Restart and load a save. Native camera post-processing must be enabled. The authored peripheral shader targets the installed game's Direct3D 11 renderer.
3. Press **F8** to start or fade out over two seconds. Press **F9** to clear immediately, including during phone use and dialogue.

The build packages the experiment without installing it or launching the game. Previous packages remain locally available. Existing custom numeric settings are retained, but `RippleStrength` now controls the corrected pixel-limited range. Its default remains `0.30`; the internal `DefaultsRevision=3` is unchanged.

## Tuning

Edit `UserData/MelonPreferences.cfg`, category `PersonalLSDPreview`, while the game is closed. Strength values accept 0–1.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Enabled` | `true` | Manual F8 preview. |
| `Intensity` | `0.60` | Overall strength. |
| `MotionStrength` | `0.50` | Peripheral breathing, color separation, ripples and afterimages. |
| `RippleStrength` | `0.30` | Pixel-limited peripheral displacement. |
| `CenterClarityDegrees` | `12` | Clear gaze radius, bounded to 0–25 degrees. Zero still fades outward from the exact center. |
| `AfterimageStrength` | `0.625` | Delayed screen images, also scaled by motion. |
| `MaterialColorStrength` | `0.45` | Continuous scene/phone RGB breathing; configuration name retained. |
| `HallucinationStrength` | `0.55` | Character opacity. |
| `AudioStrength` | `0.35` | Actual delayed output return; zero silences and clears it. |
| `OutlineStrength` | `0.70` | Defined edges and sharpening. |
| `TextBlurStrength` | `0.375` | HUD/dialogue softness and phone glyph fringes. |
| `PhoneEffects` | `true` | Added phone colors and glyph treatment. |
| `WorldVisuals` | `true` | Character hallucinations. |
| `GhostModelNPC` | `""` | Empty selects a nearby loaded NPC; otherwise NPC ID or full name. |
| `DurationSeconds` | `180` | Session length, bounded to 20–600 seconds. |

## Rendering, resources and build

Color/outline/crouch effects use an owned Volume profile. Peripheral displacement uses an owned native pass and material with `Assets/PeripheralWarp.hlsl` compiled into a tiny embedded shader bundle. Its executable vertex/pixel programs are authored; Unity serialization and binding metadata match the installed game. The bundle contains only the shader and its container, with no meshes or textures. Source and bundle hashes are checked by the build. If Unity rejects the shader or the graphics API is unsupported, the log reports fallback to a very small native warp without a center mask.

The checked-in bundle lets `Build-LSDPreview.ps1` compile without a Unity editor or Python dependency. To change its HLSL, regenerate it using `scripts/Build-LSDShader.py` with Windows `d3dcompiler_47`, UnityPy and lz4. The generator reads the installed game's serialization layout and the locally prepared asset bundle's container type; it verifies that every executable shader program matches the newly compiled source. The hash manifest prevents accidentally packaging stale HLSL.

Afterimages retain bounded GPU capture, three history images, corrected vertical sampling and no CPU readback. Phone colors track at most 160 active graphics, refresh every 0.75 seconds and animate at 20 Hz. Text tracks at most 192 combined TMP/legacy labels. Legacy glyph effects add and destroy only owned Shadow components. TMP materials and graphic colors restore only while still owned; newer native/theme writes are preserved.

Audio uses one owned non-spatial source and a two-second mutable clip. Capture buffers hold 8,192 frames per channel, and writes use 512-frame stereo packets. Gain stays below unity because the listener history can include the return itself. Logs separately report initialized capture, nonzero captured game audio, and nonzero playback from the owned source. Those signal checks are more useful than a filter-enabled message, but still do not prove subjective audibility.

At most two rendering-only character figures appear, at least five seconds apart, and fade under direct attention. Owned resources and renderer callbacks are released on reset, expiration, scene unload and deinitialization.

## Verification and live checks

Compilation uses the installed IL2CPP assemblies with warnings treated as errors. Pure checks cover timing, all input/focus/menu/transport visibility combinations, pixel bounds, clear-center transitions, movement smoothing across frame rates, color ownership, audio chunk timing and stereo impulse placement across loop wraps. The build verifies the embedded authored shader and actual listener/clip calls, and rejects stripped echo-filter references or unsafe generic audio component lookups. These checks do not establish live appearance, native shader binding, audio quality or frame-time performance.

1. Revisit the shop NPC and gas-station street in the screenshots. Faces and straight lines in the center should stay steady; peripheral bending should be barely detectable. Compare `RippleStrength=0`. Confirm the log says `LSD peripheral shader loaded`, without a fallback warning.
2. Walk, sprint, turn and ride; added bending should soften during movement. Stand still to see subtle breathing return. Crouch/stand and check that the stronger edge vignette clears correctly.
3. Open the phone home screen, read labels, then switch apps. Check color breathing, soft moving colored glyph edges and responsive controls. Look for `LSD phone colors bound` and `LSD phone text bound`. Close/reopen and press F9 to check restoration; compare `PhoneEffects=false`.
4. Listen to footsteps or another distinct game sound during the peak. Look for both `LSD echo captured nonzero game output` and `LSD echo source is playing nonzero output`. Compare `AudioStrength=0` and `1`, then restore `0.35`. Check F9, mute, pause/resume and repeated previews for immediate silence and no stale replay.
5. Confirm continuous tint across the previously patchy road boundary, upright afterimages, faded figures, three-minute expiration and clean scene/death/load cleanup. Recheck phone/dialogue/voice input, focus loss and transport continuity.

## Artistic references

[Firsthand visual accounts](https://www.reddit.com/r/LSD/comments/cxloux/lsd_visuals/) and the community [drifting description](https://effectindex.com/effects/drifting) informed the restrained breathing/color direction. [Phone-screen accounts](https://www.reddit.com/r/LSD/comments/zwaog5/what_does_your_phone_screen_look_like_on_lsd/) and [screen-visual recreation discussions](https://www.reddit.com/r/LSD/comments/1i9ps2e/attempt_to_recreate_the_visual_effect_when_you/) informed colored glyph fringes. These are varied anecdotal artistic references.

The community [auditory-distortion catalogue](https://effectindex.com/effects/auditory-distortion) includes fleeting reverberation and echoes. A small [2015 study by Kaelen and colleagues](https://pubmed.ncbi.nlm.nih.gov/26257162/) concerned emotional responses to music; it does not establish echo as a typical effect. Audio and character apparitions remain artistic choices for this game experiment.
