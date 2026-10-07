# Approved LSD visual/audio baseline: 0.10.1

Recorded October 6, 2026, before the requested 25% strength experiment. The owner described the subtle motion as a good baseline and confirmed the native-bus echo was audible. This preserves that implementation rather than replacing it with the stronger trial.

## Files and exact settings

The source is fixed at [commit b5f003007682d33ba3ff0b9d34b0cdb5e685dbfa](https://github.com/Axl47/ScheduleOne-Mods/commit/b5f003007682d33ba3ff0b9d34b0cdb5e685dbfa). The [captured LSD preference section](baselines/lsd-0.10.1.cfg) contains the actual file values and comments, including floating-point serialization. [Source/resource hashes](baselines/lsd-0.10.1.sha256) record the working files before edits; text-file byte hashes depend on their captured Windows line endings. The Git commit remains the portable source reference.

The installed `Mods/Personal.LSDPreview.dll` matched the packaged 0.10.1 DLL. Its SHA-256 was `ce4e725453dce0c5dd0a8d841550762913d0df04beae5d51902dc634e91421cc`. The existing local `artifacts/LSDPreview-0.10.1.zip` remains intact, with SHA-256 `f7483186e0afaa952214e4953be3780bd4c36fe2f672b351f3827fa6c35e6ce1`. An additional local backup of the DLL and full preferences is in `artifacts/install-backups/lsd-approved-baseline-0.10.1-20261006-215010/`; full preferences are not published because they include other mods.

| Setting | Captured value |
| --- | --- |
| Enabled / WorldVisuals / PhoneEffects | true / true / true |
| Intensity | 0.60 |
| MotionStrength | 0.50 |
| RippleStrength | 0.30 |
| AfterimageStrength | 0.625 |
| MaterialColorStrength | 0.45 |
| HallucinationStrength | 0.55 |
| AudioStrength | 0.35 |
| OutlineStrength | 0.70 |
| TextBlurStrength | 0.375 |
| CenterClarityDegrees | 5; reserved, no active gaze mask |
| DurationSeconds | 180 |
| GhostModelNPC | empty; nearby loaded NPC |
| DefaultsRevision | 4 |

The file also retains `SurfacePatternStrength=0.45` from the abandoned pattern experiment. That entry is unused by 0.10.1 and must not be interpreted as an enabled effect. `TextFocusStrength` does not exist in this baseline.

## Implementation calibration

`RippleRange.cs` bounds displacement in pixels:

```text
amount = tripEnvelope * Intensity
breath(t) = 0.85 + 0.15 * sin(0.23 * t)
pixels(t) = min(18, 60 * amount * RippleStrength * MotionStrength)
            * movementComfort * breath(t)
lensIntensity(t) = min(0.11, 0.11 * RippleStrength / 0.30 * MotionStrength)
                   * movementComfort * sin(0.65 * t)
volumeWeight = amount
```

At the stationary peak, the pixel bound is 3.78–5.4 pixels. Lens intensity amplitude is 0.055 before the 0.60 Volume blend. Moving at 4 m/s approaches 62.5% of stationary strength; rapid motion has a 25% floor. Timing, shader frequencies and movement smoothing are part of the baseline, not just the preferences.

`PeripheralWarp.hlsl` divides movement into 70% uneven flow, 20% drifting-origin ripple and 10% radial breathing. The flow combines 72% broad waves and 28% detail. The completed-frame pass captures and warps two owned GPU textures, then explicitly corrects the Direct3D capture origin during presentation. No material patterns, gaze mask or added vignette are active. Shader bundle SHA-256: `73a97ba1f2be732d8e43e0869dc47b588c08d2d1f73bbb45466b0d859cee07b3`.

`PreviewAudio.cs` leases original main/music/menu bus outputs through owned Echo mixers. Delay is 220 ms, decay 0.18 and dry gain 1. The wet sample is `min(0.90, 1.40 * amount * AudioStrength) * (0.80 + 0.20 * pulse(t)^2)`, with the existing slow pulse defined in `AudioBlend.cs`. At default peak settings, wet return is 23.52–29.4%. Echo bundle SHA-256: `27bddb96a31e514cb2289668cff5de438ed26300bcdfb09433795187c7e6dae2`.

`HudTextBlur.cs` leaves the wording intact and copies font materials for slowly changing outline softness/width. Its envelope is `amount * TextBlurStrength * (0.7 + 0.3 * sin(0.4 * t))`. Phone text additionally receives colored underlay/legacy shadows. There is no word rewriting or new reading-focus burst in this baseline.

Vivid color grading uses saturation 56, contrast `28 + 4 * sin(0.16 * t)`, exposure 0.15, hue `24 * sin(0.18 * t)` and the original continuous RGB filter. Upright afterimages, faint adult-sized character snapshots and F8/F9 cleanup are retained. Exact formulas and ownership behavior are available in the fixed source commit.

## Comparing the stronger trial

In 0.11.0, set `RippleStrength=0.30`, `AudioStrength=0.35` and `TextFocusStrength=0` after the one-time defaults upgrade (`DefaultsRevision=5`) to replay the baseline strengths and text treatment. Other settings above remain the same. Retaining revision 5 prevents a later launch from upgrading deliberately restored values again.

For exact historical behavior, close the game and restore the preserved 0.10.1 DLL plus only the captured `[PersonalLSDPreview]` preference section. Keep unrelated preference sections intact. This approved baseline is separate from the older broad displacement kept in the [future alcohol note](future-alcohol-preview.md).
