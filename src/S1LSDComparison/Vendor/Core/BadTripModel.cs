// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using S1LSD.Config;

namespace S1LSD.Core;

public static class BadTripModel
{
	public static float Chance(int dose, BadTripConfig config)
	{
		if (!config.Enabled || !config.ChanceByDose.TryGetValue(dose.ToString(), out var value))
		{
			return 0f;
		}
		return Math.Clamp(value, 0f, 1f);
	}

	public static bool Roll(int dose, BadTripConfig config, ref XorShift32 rng)
	{
		float num = Chance(dose, config);
		if (num <= 0f)
		{
			return false;
		}
		if (num >= 1f)
		{
			return true;
		}
		return rng.NextFloat() < num;
	}

	public static float DurationSeconds(float doseSeconds, BadTripConfig config)
	{
		float y = config.DurationMinutes * 60f;
		float x = config.MinDurationMinutes * 60f;
		return MathF.Max(MathF.Min(doseSeconds, y), MathF.Min(x, y));
	}
}
