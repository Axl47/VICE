# Future experiment: alcohol visuals

Status: a future design note only. No alcohol effect is implemented or enabled.

Preserve the **LSD preview 0.5.0 displacement range**, before the narrower LSD range introduced in 0.6.0. The 0.5.0 baseline commit contains the original implementation in `src/LSDPreview/SurfaceRipples.cs`; the existing `artifacts/LSDPreview-0.5.0.zip` also remains available locally for comparison.

## Preserved displacement

Let `s` be the old `RippleStrength` range, `m` be `MotionStrength`, `a` be the trip envelope multiplied by `Intensity`, and `t` be elapsed seconds:

```text
s = 0..1, default 0.50
m = 0..1, default 0.50
Intensity = 0.60
wave(t) = 0.008 + 0.006 * (0.5 + 0.5 * sin(0.23 * t))
Amplitude = 0.12675 * wave(t) * s * m
Blend = a * s * m
NoiseScale = 12 + 3 * sin(0.08 * t)
PanSpeed = (0.035, 0.022)
Bounce = true
```

Both amplitude and blend depend on the old strength, so the visible result is not a simple linear scale. At the old defaults and peak envelope, blend is `0.15` and amplitude ranges from `0.0002535` to `0.000443625`. At old strength `1`, blend and amplitude are each twice those values. These are shader parameters, not measured pixel displacements.

Separate camera breathing uses lens intensity `0.11 * m * sin(0.65 * t)` and chromatic intensity `0.09 * m`, blended by overall intensity. Keep these as references rather than silently replacing them with the new LSD calibration.

## Possible later direction

Use this wider range to test an alcohol interpretation: slower sway, intermittent double vision and less vivid color treatment. Start with the preserved 0.5.0 baseline, then tune an independent alcohol range and defaults. Do not enable it through the LSD preview or add a product until that experiment is requested.
