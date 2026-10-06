# LSD visual preview 0.8.1

This separate `Personal.LSDPreview.dll` experiment explores perception before adding an LSD product. F8 starts a 180-second preview or fades it out; F9 clears it immediately. No production, sales or save-data integration is added.

## This revision

**Startup hotfix:** the 0.8.0 binary was followed by two Windows Application Hang reports before the main menu. Its new global audio playback detours were installed even while the preview was inactive. The log ended after mod initialization, with no preview started. Both target addresses are distinct, so this is not the older shared-native-body failure. The exact failing native stack was not captured. Version 0.8.1 removes both detours entirely and observes the game's existing SFX pool only during an active preview. Build checks reject startup audio initialization and playback hooks. Scene and first-update logs help verify that startup progresses.

The 0.7.0 playtest confirmed that the authored shader loaded, but its default displacement was too small to see: only 0.378–0.54 pixels before the focus mask and movement reduction. Removing the broad lens effect also removed the recognizable breathing. This revision restores a few pixels of peripheral motion and adds a slow radial expansion/contraction to the existing flowing displacement.

At the normal defaults and peak envelope, the vector length is bounded to **3.78–5.4 pixels**, before the focus mask and movement reduction. Actual movement is smaller because the flowing and breathing vectors, focus mask and edge fade reduce it. The breathing cycle is approximately 9.7 seconds. A **five-degree radius around the gaze center has exactly zero added displacement**, followed by a gradual transition toward the periphery. Edges fade to zero to avoid stretched border pixels. The absolute guard is eighteen pixels at any combination of strengths; normal settings with `RippleStrength=1` reach 12.6–18 pixels before masking.

The added crouch vignette is removed. The native crouch vignette keeps its original behavior. Moving or turning still smoothly reduces displacement: at four metres per second it approaches 62.5% of stationary strength; rapid travel approaches a floor of 25%. Standing still gently restores the visible breathing. Broad lens distortion remains removed so that the protected center stays steady. Color breathing, saturation/contrast, outlines, upright afterimages, phone treatment and faded character meshes keep their previous settings.

### Audio: native clip copies

The 0.7.0 log showed nonzero captured audio and a very small return during fade-in, but the user heard no echo. Its diagnostics did not measure the later peak. Listener analysis history and mutable clip writes are no longer used for playback.

The new route passively reads `SFXManager._soundsInUse` during an active preview. The native pool contains footsteps and impacts with their selected clips, pitch and volume already applied. Newly observed playback schedules **one copy of the same clip at +220 ms from observation**, using Unity's [DSP-clock scheduling API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource.PlayScheduled.html). Original onset detection adds up to an ordinary update interval; the scheduled delay then uses the audio clock. Clip, pitch, source volume, mixer group, stereo pan, position and distance attenuation are copied into an owned return source. Original game sources and mixer settings are not changed, and no native playback method is patched.

At default peak strength, the return varies from **14.2% to 18.9% of the original source volume**, before the same native mixer and spatial attenuation. It remains present between slow swells. Maximum settings permit a 90% single copy. Owned returns are absent from the game's SFX pool, so they cannot trigger another echo. There is no feedback loop, microphone capture, recording or synthetic test sound.

Coverage is deliberately concrete: sounds in the native footsteps/impact pool. Music, ambient loops and unrelated controller sources are not observed. Clips longer than thirty seconds and unsupported pitches are skipped. Source membership, clip changes and playback restarts identify new sounds; a one-shot whose cursor stays at zero is not copied repeatedly. A baseline is established at activation and after pause/mute, so already-playing sounds are not replayed on resume. Very short sounds that begin and end between updates can be missed. Observation tracks at most 128 sources without scanning the scene. Up to twelve delayed voices can overlap; excess copies are dropped without interrupting an existing echo. Reservations include the delay, and expired voices are reused. A missed scheduling deadline is discarded. Pause, mute, zero strength, F9, preview cleanup and audio-clock reset stop and clear pending and playing copies.

Logs distinguish pool observation starting, a real clip being scheduled, and nonzero output from an owned source. Additional checks at 10, 30 and 60 seconds report observed starts, scheduled copies, nonzero voices, ignored/dropped starts, current/peak return gain and output peak. This covers the peak rather than stopping at the first quiet fade-in sample. These readings do not establish subjective audibility; the next game playtest is still needed.

### Retained visual layers

The phone treatment covers active graphics beneath `phoneModel`, including the home screen and apps. Legacy `UnityEngine.UI.Text` labels receive owned, softly moving cyan/purple glyph shadows; TMP labels retain their owned soft font materials. Button positions, layout and hit targets remain native. Closing the phone, switching apps or pressing F9 removes owned glyph effects and restores eligible colors/materials.

Effects and trip time continue through phone use, dialogue, typing, voice input restrictions, transport and focus loss while gameplay runs. Only an actual pause suspends the preview. Loading, death, unconsciousness, player replacement or a change in consumed product clears it. Camera replacements retain trip progress. The game still controls whether it runs in the background.

Continuous global RGB color breathing remains in place, without material discovery, camera rays or per-tile writes. Saturation `56`, contrast `28 ± 4`, exposure `0.15` EV and subtle hue movement retain the approved direction before overall intensity blending. The original 0.5.0 displacement formulas and implementation remain preserved in [baseline commit eb55537](https://github.com/Axl47/ScheduleOne-Mods/commit/eb55537) and the [future alcohol note](https://github.com/Axl47/ScheduleOne-Mods/blob/main/docs/future-alcohol-preview.md). Alcohol remains a future note only.

## Install or update

1. Close the game. From `LSDPreview-0.8.1.zip`, copy only `Mods/Personal.LSDPreview.dll` into the game's `Mods` folder, replacing the previous preview DLL. Do not keep 0.8.0 in `Mods` under another DLL name.
2. Restart and load a save. Native camera post-processing must be enabled. The authored peripheral shader targets the installed game's Direct3D 11 renderer.
3. Press **F8** to start or fade out over two seconds. Press **F9** to clear immediately, including during phone use and dialogue.

The build packages the experiment without installing it or launching the game. Previous packages remain locally available. The default `RippleStrength` remains `0.30`. The one-time `DefaultsRevision=4` upgrade changes the previous twelve-degree default center to five degrees; other configured radii and numeric settings are retained. The same ripple setting now uses the visible pixel range described above.

## Tuning

Edit `UserData/MelonPreferences.cfg`, category `PersonalLSDPreview`, while the game is closed. Strength values accept 0–1.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Enabled` | `true` | Manual F8 preview. |
| `Intensity` | `0.60` | Overall strength. |
| `MotionStrength` | `0.50` | Peripheral breathing, color separation, ripples and afterimages. |
| `RippleStrength` | `0.30` | Peripheral flow and radial breathing; normal peak bound 5.4 pixels. |
| `CenterClarityDegrees` | `5` | Clear gaze radius, bounded to 0–25 degrees. Zero still fades outward from the exact center. |
| `AfterimageStrength` | `0.625` | Delayed screen images, also scaled by motion. |
| `MaterialColorStrength` | `0.45` | Continuous scene/phone RGB breathing; configuration name retained. |
| `HallucinationStrength` | `0.55` | Character opacity. |
| `AudioStrength` | `0.35` | Delayed native clip copies; zero silences and clears them. |
| `OutlineStrength` | `0.70` | Defined edges and sharpening. |
| `TextBlurStrength` | `0.375` | HUD/dialogue softness and phone glyph fringes. |
| `PhoneEffects` | `true` | Added phone colors and glyph treatment. |
| `WorldVisuals` | `true` | Character hallucinations. |
| `GhostModelNPC` | `""` | Empty selects a nearby loaded NPC; otherwise NPC ID or full name. |
| `DurationSeconds` | `180` | Session length, bounded to 20–600 seconds. |

## Rendering, resources and build

Color and outline effects use an owned Volume profile. Peripheral displacement uses an owned native pass and material with `Assets/PeripheralWarp.hlsl` compiled into a tiny embedded shader bundle. Its executable vertex/pixel programs are authored; Unity serialization and binding metadata match the installed game. The bundle contains only the shader and its container, with no meshes or textures. Source and bundle hashes are checked by the build. If Unity rejects the shader or the graphics API is unsupported, the log reports fallback to a small native warp without a center mask.

The checked-in bundle lets `Build-LSDPreview.ps1` compile without a Unity editor or Python dependency. To change its HLSL, regenerate it using `scripts/Build-LSDShader.py` with Windows `d3dcompiler_47`, UnityPy and lz4. The generator reads the installed game's serialization layout and the locally prepared asset bundle's container type; it verifies that every executable shader program matches the newly compiled source. The hash manifest prevents accidentally packaging stale HLSL.

Afterimages retain bounded GPU capture, three history images, corrected vertical sampling and no CPU readback. Phone colors track at most 160 active graphics, refresh every 0.75 seconds and animate at 20 Hz. Text tracks at most 192 combined TMP/legacy labels. Legacy glyph effects add and destroy only owned Shadow components. TMP materials and graphic colors restore only while still owned; newer native/theme writes are preserved.

Audio owns at most twelve native AudioSources created through type-based IL2CPP component lookup, plus a reused 512-sample output-probe buffer and a bounded source-onset tracker. It does not create or rewrite audio assets, install callbacks or detours, or scan the scene for sources. The return follows the original mixer routing and native master mute. At most two rendering-only character figures appear, at least five seconds apart, and fade under direct attention. Owned resources and renderer callbacks are released on reset, expiration, scene unload and deinitialization.

## Verification and live checks

Compilation uses the installed IL2CPP assemblies with warnings treated as errors. Pure checks cover timing, all input/focus/menu/transport visibility combinations, displacement bounds and range, clear-center transitions, movement smoothing across frame rates, color ownership, source-onset detection and limits, delayed DSP deadlines, pitch-adjusted voice lifetimes, bounded overlap, slot reuse, stale-event rejection and reset. The build verifies the embedded authored shader, native pool access and scheduled clip calls. It rejects audio initialization in the startup callback, the former native playback detours, listener capture/PCM writes, stripped echo filters, mixer snapshot replacement and unsafe generic audio component lookups. These checks do not establish live appearance, audio quality or frame-time performance.

First verify that 0.8.1 reaches the menu and logs `LSD startup audio hooks removed`, `LSD preview observed scene loaded` and `LSD preview main-loop heartbeat`. No pool observation or echo source should be initialized before F8.

1. Revisit the shop NPC and street in the screenshots. Stand still for 15–20 seconds at peak strength: scenery away from the gaze center should slowly breathe and drift, with steady faces and straight lines inside the center. Compare `RippleStrength=0`. Confirm `LSD peripheral shader loaded` appears without a fallback warning.
2. Walk, sprint, turn and ride; displacement should soften during movement, then recover when stationary. Crouch/stand and confirm only the game's original vignette remains.
3. Open the phone home screen, read labels, then switch apps. Check color breathing, soft moving colored glyph edges and responsive controls. Close/reopen and press F9 to check restoration; compare `PhoneEffects=false`.
4. At least thirty seconds after F8, walk on a quiet street or trigger a pooled impact sound. Listen for a quieter repeat about a fifth of a second later. Check `LSD echo observing the native footsteps/impact sound pool`, `LSD echo scheduled game clip`, `LSD echo played a delayed game clip with nonzero output`, and the 30/60-second `LSD echo check` lines. Compare `AudioStrength=0` and `1`, then restore `0.35`. Check F9, mute, pause/resume and repeated previews for immediate silence and no stale replay.
5. Confirm continuous tint across the previously patchy road boundary, upright afterimages, faded figures, three-minute expiration and clean scene/death/load cleanup. Recheck phone/dialogue/voice input, focus loss and transport continuity.

## Artistic references

[Firsthand visual accounts](https://www.reddit.com/r/LSD/comments/cxloux/lsd_visuals/) and the community [drifting description](https://effectindex.com/effects/drifting) informed the restrained breathing/color direction. [Phone-screen accounts](https://www.reddit.com/r/LSD/comments/zwaog5/what_does_your_phone_screen_look_like_on_lsd/) and [screen-visual recreation discussions](https://www.reddit.com/r/LSD/comments/1i9ps2e/attempt_to_recreate_the_visual_effect_when_you/) informed colored glyph fringes. These are varied anecdotal artistic references.

The community [auditory-distortion catalogue](https://effectindex.com/effects/auditory-distortion) includes fleeting reverberation and echoes. A small [2015 study by Kaelen and colleagues](https://pubmed.ncbi.nlm.nih.gov/26257162/) concerned emotional responses to music; it does not establish echo as a typical effect. Audio and character apparitions remain artistic choices for this game experiment.
