// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using S1LSD.Config;

namespace S1LSD.Core;

public static class ToleranceModel
{
	public static float Gain(float dose, ToleranceConfig config)
	{
		if (dose <= 0f)
		{
			return 0f;
		}
		float num = MathF.Max(0.0001f, config.ReferenceDose);
		return config.BaseGain * MathF.Pow(dose / num, config.GainExponent);
	}

	public static float ApplyDose(float current, float dose, ToleranceConfig config)
	{
		if (!config.Enabled)
		{
			return current;
		}
		float num = Math.Clamp(current, 0f, 1f);
		return Math.Clamp(num + Gain(dose, config) * (1f - num), 0f, 1f);
	}

	public static float Decayed(float current, float elapsedGameDays, ToleranceConfig config)
	{
		if (!config.Enabled || config.HalfLifeGameDays <= 0f)
		{
			return Math.Clamp(current, 0f, 1f);
		}
		float num = Math.Clamp(current, 0f, 1f);
		if (elapsedGameDays <= 0f)
		{
			return num;
		}
		float num2 = MathF.Pow(0.5f, elapsedGameDays / config.HalfLifeGameDays);
		return Math.Clamp(num * num2, 0f, 1f);
	}

	public static float DaysBetween(float from, float to, ToleranceConfig config)
	{
		if (config.HalfLifeGameDays <= 0f || from <= 0f)
		{
			return 0f;
		}
		to = Math.Clamp(to, 0f, from);
		if (to <= 0f)
		{
			return float.PositiveInfinity;
		}
		return config.HalfLifeGameDays * MathF.Log2(from / to);
	}

	public static float EffectMultiplier(float tolerance, ToleranceConfig config)
	{
		return Math.Clamp(1f - config.MaxReduction * Math.Clamp(tolerance, 0f, 1f), 0f, 1f);
	}

	public static float DurationMultiplier(float tolerance, ToleranceConfig config)
	{
		return Math.Clamp(1f - config.DurationReductionMax * Math.Clamp(tolerance, 0f, 1f), 0f, 1f);
	}
}
