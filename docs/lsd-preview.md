# LSD visual preview 0.9.0

This separate `Personal.LSDPreview.dll` experiment explores perception before adding an LSD product. F8 starts a 180-second preview or fades it out; F9 clears it immediately. No production, sales or save-data integration is added.

## This revision

The 0.8.1 live log verifies delayed footstep output at peak strength: 208 observed starts, 208 scheduled copies and 199 nonzero voices at 60 seconds. However, that implementation never observed shop music or ambient loops. This revision adds continuous copies of those sources and raises the normal return from 14.2–18.9% to **23.5–29.4% of the original source volume**.

Breathing gets two adjustments without raising the flowing shader's pixel limit: its radial expansion/contraction now contributes 65% of the motion instead of 35%, and the fade outside the gaze center reaches full strength much sooner. A restrained native lens pulse also returns, using half the old 0.5.0 intensity coefficient at the usual defaults. This restores the independent breathing cue removed in 0.7.0. A one-time optional offscreen GPU check and live pass diagnostics distinguish a shader loading from it actually changing pixels.

At normal defaults and peak envelope, the flowing shader's vector remains bounded to **3.78–5.4 pixels**, before masking and movement reduction. Its five-degree gaze region remains clear of flowing ripples; the outer transition now ends at normalized radius 0.28 rather than 0.48 at the default field of view. Edges fade to zero to avoid stretched border pixels. The absolute guard for this shader remains eighteen pixels. The separate lens pulse is centered on the gaze and barely affects small central objects; its maximum intensity is 0.055, and default peak intensity is ±0.0165 after overall blending. Thus the combined effect no longer promises an exactly motionless five-degree region or a single pixel bound for both layers. Both breathing cues cycle in approximately 9.7 seconds and obey `RippleStrength` and `MotionStrength`.

The added crouch vignette remains removed. Moving or turning smoothly reduces both displacement layers: at four metres per second they approach 62.5% of stationary strength, with a 25% floor during rapid travel. Standing still restores them. Color breathing, saturation/contrast, outlines, upright afterimages, phone treatment and faded character meshes keep their approved settings.

### Audio: native clip copies

The startup repair from 0.8.1 is retained: no audio source, callback or native playback detour is initialized during normal startup. Only an active preview observes sources. Listener history and mutable clip writes remain unused.

The new route passively reads `SFXManager._soundsInUse` during an active preview. The native pool contains footsteps and impacts with their selected clips, pitch and volume already applied. Newly observed playback schedules **one copy of the same clip at +220 ms from observation**, using Unity's [DSP-clock scheduling API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource.PlayScheduled.html). Original onset detection adds up to an ordinary update interval; the scheduled delay then uses the audio clock. Clip, pitch, source volume, mixer group, stereo pan, position and distance attenuation are copied into an owned return source. Original game sources and mixer settings are not changed, and no native playback method is patched.

At default peak strength, the return varies from **23.5% to 29.4% of the original source volume**, before the same native mixer and spatial attenuation. It remains present between slow swells. Maximum settings permit a 90% single copy. Owned returns never enter discovery or the game's SFX pool, so there is no feedback loop or microphone capture.

Footsteps and impacts keep the bounded pool observer: up to 128 source states and twelve owned copies. Very short events between updates can be missed; late deadlines or excess overlap are dropped. An initial baseline prevents stale short sounds from replaying on activation or resume.

Music and ambient loops use a separate path. Native controllers categorized as Music or Ambient are discovered without sorting once on activation and refreshed every eight seconds, with at most 256 qualifying sources cached. Looping clips and tracks longer than thirty seconds qualify; voice, FX and the footstep pool are excluded. Every half-second, the six strongest eligible sources are selected, preferring nearby spatial audio. Each owned copy begins at the native track's **current cursor minus 220 ms**, accounting for pitch and a 50 ms scheduling lead. This works for music already playing when F8 is pressed, without restarting the song. Loop boundaries wrap correctly. Track changes, seeks, substantial cursor drift and pitch changes resynchronize the copy; compressed seek resolution may affect exact delay. Original volume, routing, position and distance curves are retained. Muted/stopped sources, pause, zero strength, F9 and cleanup silence the copies. A source controlled without a native AudioSourceController is outside this route's coverage.

Logs distinguish observation, scheduling and nonzero output. `LSD music observation`, `LSD music return` and `LSD music return verified nonzero output` identify continuous coverage. The 10/30/60-second echo checks also report active music voices and their measured peak. These readings do not establish subjective audibility at the gas station or shop; a live comparison is still required.

### Retained visual layers

The phone treatment covers active graphics beneath `phoneModel`, including the home screen and apps. Legacy `UnityEngine.UI.Text` labels receive owned, softly moving cyan/purple glyph shadows; TMP labels retain their owned soft font materials. Button positions, layout and hit targets remain native. Closing the phone, switching apps or pressing F9 removes owned glyph effects and restores eligible colors/materials.

Effects and trip time continue through phone use, dialogue, typing, voice input restrictions, transport and focus loss while gameplay runs. Only an actual pause suspends the preview. Loading, death, unconsciousness, player replacement or a change in consumed product clears it. Camera replacements retain trip progress. The game still controls whether it runs in the background.

Continuous global RGB color breathing remains in place, without material discovery, camera rays or per-tile writes. Saturation `56`, contrast `28 ± 4`, exposure `0.15` EV and subtle hue movement retain the approved direction before overall intensity blending. The original 0.5.0 displacement formulas and implementation remain preserved in [baseline commit eb55537](https://github.com/Axl47/ScheduleOne-Mods/commit/eb55537) and the [future alcohol note](https://github.com/Axl47/ScheduleOne-Mods/blob/main/docs/future-alcohol-preview.md). Alcohol remains a future note only.

## Install or update

1. Close the game. From `LSDPreview-0.9.0.zip`, copy only `Mods/Personal.LSDPreview.dll` into the game's `Mods` folder, replacing the previous preview DLL. Keep previous binaries outside `Mods`.
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
| `RippleStrength` | `0.30` | Peripheral flow plus restrained lens breathing; flow peak bound 5.4 pixels. |
| `CenterClarityDegrees` | `5` | Clear radius for flowing ripples, bounded to 0–25 degrees; lens breathing stays gaze-centered. |
| `AfterimageStrength` | `0.625` | Delayed screen images, also scaled by motion. |
| `MaterialColorStrength` | `0.45` | Continuous scene/phone RGB breathing; configuration name retained. |
| `HallucinationStrength` | `0.55` | Character opacity. |
| `AudioStrength` | `0.35` | Delayed footsteps, impacts, music and ambient loops; zero clears them. |
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

Audio owns at most twelve short-sound and six continuous native AudioSources created through type-based IL2CPP component lookup, reused output-probe buffers and bounded trackers. Continuous discovery happens every eight seconds only while the preview runs. It creates no audio assets or detours and retains mixer routing/master mute. At most two rendering-only character figures appear, at least five seconds apart, and fade under direct attention. Owned resources and callbacks are released on reset, expiration, scene unload and deinitialization.

## Verification and live checks

Compilation uses the installed IL2CPP assemblies with warnings treated as errors. Pure checks cover timing, all input/focus/menu/transport visibility combinations, displacement bounds and range, clear-center transitions, movement smoothing across frame rates, color ownership, source-onset detection and limits, delayed DSP deadlines, pitch-adjusted voice lifetimes, bounded overlap, slot reuse, stale-event rejection and reset. The build verifies the embedded authored shader, native pool access and scheduled clip calls. It rejects audio initialization in the startup callback, the former native playback detours, listener capture/PCM writes, stripped echo filters, mixer snapshot replacement and unsafe generic audio component lookups. These checks do not establish live appearance, audio quality or frame-time performance.

First verify that 0.9.0 reaches the menu and logs `LSD startup audio hooks removed`, `LSD preview observed scene loaded` and `LSD preview main-loop heartbeat`. No observation or echo source should be initialized before F8. The explicit launch argument `--lsd-render-check` runs an owned offscreen GPU shader test once in the menu; normal launches perform no image readback. A pass logs changed pixels, time-varying pixels and zero changes inside the shader's protected center.

The additional explicit argument `--lsd-audio-check` exercises only continuous native menu music for three seconds and destroys its owned returns afterward. Normal launches do not run either diagnostic. Both were checked locally on October 6, 2026 at 18:57: startup reached Menu, the GPU test reported 123,964 changed pixels and 140,767 time-varying pixels with zero protected-center changes, and a delayed copy of `MainThemeStartTrack` produced nonzero output with peak 0.00243. Discovery found twenty native music/ambient sources in 1.31 ms in that menu test. Local scene metadata also confirms that the existing music loops use AudioSourceController. No save was loaded. These checks verify GPU motion and the continuous playback route; they do not establish appearance, audibility or discovery cost in the loaded city.

1. Revisit the shop NPC and street. Wait at least 30 seconds after F8, then stand still: surfaces should slowly expand/contract and drift, with much less bending around the gaze. Compare `RippleStrength=0`. Confirm `LSD ripple check` reports an allocated pass, positive material blend and increasing shader elapsed time, without a fallback warning.
2. Walk, sprint, turn and ride; displacement should soften during movement, then recover when stationary. Crouch/stand and confirm only the game's original vignette remains.
3. Open the phone home screen, read labels, then switch apps. Check color breathing, soft moving colored glyph edges and responsive controls. Close/reopen and press F9 to check restoration; compare `PhoneEffects=false`.
4. At least thirty seconds after F8, listen at the gas station and shop, then walk on a quiet street. Music should have a softer delayed layer and footsteps a quieter repeat. Check `LSD music return verified nonzero output` and the 30/60-second echo checks. Compare `AudioStrength=0` and `1`, then restore `0.35`. Check F9, mute, pause/resume, track changes and repeated previews for silence on cleanup and no song restarts.
5. Confirm continuous tint across the previously patchy road boundary, upright afterimages, faded figures, three-minute expiration and clean scene/death/load cleanup. Recheck phone/dialogue/voice input, focus loss and transport continuity.

## Artistic references

[Firsthand visual accounts](https://www.reddit.com/r/LSD/comments/cxloux/lsd_visuals/) and the community [drifting description](https://effectindex.com/effects/drifting) informed the restrained breathing/color direction. [Phone-screen accounts](https://www.reddit.com/r/LSD/comments/zwaog5/what_does_your_phone_screen_look_like_on_lsd/) and [screen-visual recreation discussions](https://www.reddit.com/r/LSD/comments/1i9ps2e/attempt_to_recreate_the_visual_effect_when_you/) informed colored glyph fringes. These are varied anecdotal artistic references.

The community [auditory-distortion catalogue](https://effectindex.com/effects/auditory-distortion) includes fleeting reverberation and echoes. A small [2015 study by Kaelen and colleagues](https://pubmed.ncbi.nlm.nih.gov/26257162/) concerned emotional responses to music; it does not establish echo as a typical effect. Audio and character apparitions remain artistic choices for this game experiment.
