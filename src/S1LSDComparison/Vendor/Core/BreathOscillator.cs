// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using S1LSD.Config;

namespace S1LSD.Core;

public sealed class BreathOscillator
{
	private readonly ValueNoise _noise;

	private BreathConfig _config;

	private float _phase;

	public float Value { get; private set; }

	public float CurrentHz { get; private set; }

	public float RateMultiplier { get; set; } = 1f;

	public BreathOscillator(BreathConfig config, int seed)
	{
		_config = config;
		_noise = new ValueNoise(seed);
		_phase = 0f;
	}

	public void SetConfig(BreathConfig config)
	{
		_config = config;
	}

	public void Reset()
	{
		_phase = 0f;
		Value = 0f;
		CurrentHz = _config.BaseHz;
		RateMultiplier = 1f;
	}

	public void Tick(float deltaTime)
	{
		if (!(deltaTime <= 0f))
		{
			float num = _noise.Fbm(_phase * _config.PerlinFrequency, _config.PerlinOctaves);
			float num2 = _config.BaseHz * (1f + _config.Depth * num) * RateMultiplier;
			if (num2 < 0.01f)
			{
				num2 = 0.01f;
			}
			CurrentHz = num2;
			_phase += num2 * deltaTime;
			if (_phase > 4096f)
			{
				_phase -= 4096f;
			}
			float num3 = 0.5f + 0.5f * MathF.Sin(_phase * ((float)Math.PI * 2f));
			float num4 = _noise.Fbm(_phase * _config.PerlinFrequency * 2.7f, 2) * 0.12f;
			Value = Math.Clamp(num3 + num4, 0f, 1f);
		}
	}
}
