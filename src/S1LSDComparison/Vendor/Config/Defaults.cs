// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using System;
using System.Collections.Generic;

namespace S1LSD.Config;

public static class Defaults
{
	public const string ModNamespace = "s1lsd";

	public const string ProductKindId = "s1lsd:lsd";

	public static Dictionary<string, float> Peaks()
	{
		return new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["warp"] = 0.1f,
			["trails"] = 0.08f,
			["halo"] = 0.12f,
			["chroma"] = 0.05f,
			["colorDrift"] = 0.2f,
			["localContrast"] = 0.25f,
			["afterimage"] = 0f,
			["patterns"] = 0f,
			["reverbWet"] = 0.05f,
			["echo"] = 0f,
			["pitchDrift"] = 0.05f,
			["lfPulse"] = 0.05f,
			["textureCrawl"] = 0.1f,
			["pupil"] = 0.15f,
			["synesthesia"] = 0.05f,
			["closedEyes"] = 0f
		};
	}

	public static Dictionary<string, float> Peaks(float w, float tr, float ha, float ch, float cd, float lc, float ai, float pa, float rv, float ec, float pd, float lp)
	{
		return new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["warp"] = w,
			["trails"] = tr,
			["halo"] = ha,
			["chroma"] = ch,
			["colorDrift"] = cd,
			["localContrast"] = lc,
			["afterimage"] = ai,
			["patterns"] = pa,
			["reverbWet"] = rv,
			["echo"] = ec,
			["pitchDrift"] = pd,
			["lfPulse"] = lp,
			["textureCrawl"] = w,
			["pupil"] = ha,
			["synesthesia"] = lp,
			["closedEyes"] = 0f
		};
	}

	public static Dictionary<string, float> Peaks(float w, float tr, float ha, float ch, float cd, float lc, float ai, float pa, float rv, float ec, float pd, float lp, float tc, float pu, float syn, float ce)
	{
		return new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["warp"] = w,
			["trails"] = tr,
			["halo"] = ha,
			["chroma"] = ch,
			["colorDrift"] = cd,
			["localContrast"] = lc,
			["afterimage"] = ai,
			["patterns"] = pa,
			["reverbWet"] = rv,
			["echo"] = ec,
			["pitchDrift"] = pd,
			["lfPulse"] = lp,
			["textureCrawl"] = tc,
			["pupil"] = pu,
			["synesthesia"] = syn,
			["closedEyes"] = ce
		};
	}

	public static Dictionary<string, DoseConfig> Doses()
	{
		return new Dictionary<string, DoseConfig>(StringComparer.Ordinal)
		{
			["70"] = new DoseConfig
			{
				DisplayName = "Acid 70",
				Price = 52f,
				DurationMinutes = 3.5f,
				PsyBlend = 0.03f,
				PsyAmplitude = 0.15f,
				PsyNoiseScale = 40f,
				ClassicBlend = 0.03f,
				ClassicAmplitude = 0.15f,
				ClassicNoiseScale = 40f,
				SubtleBlend = 0.002f,
				SubtleAmplitude = 0.05f,
				SubtleNoiseScale = 55f,
				Peaks = Peaks(0.1f, 0.08f, 0.12f, 0.05f, 0.2f, 0.25f, 0f, 0f, 0.05f, 0f, 0.05f, 0.05f, 0.1f, 0.15f, 0.05f, 0f)
			},
			["140"] = new DoseConfig
			{
				DisplayName = "Acid 140",
				Price = 78f,
				DurationMinutes = 5.5f,
				PsyBlend = 0.07f,
				PsyAmplitude = 0.22f,
				PsyNoiseScale = 34f,
				ClassicBlend = 0.07f,
				ClassicAmplitude = 0.22f,
				ClassicNoiseScale = 34f,
				SubtleBlend = 0.008f,
				SubtleAmplitude = 0.12f,
				SubtleNoiseScale = 65f,
				Peaks = Peaks(0.3f, 0.25f, 0.3f, 0.15f, 0.4f, 0.4f, 0.1f, 0.35f, 0.2f, 0.1f, 0.15f, 0.15f, 0.3f, 0.3f, 0.2f, 0f)
			},
			["280"] = new DoseConfig
			{
				DisplayName = "Acid 280",
				Price = 124f,
				DurationMinutes = 8.5f,
				PsyBlend = 0.13f,
				PsyAmplitude = 0.3f,
				PsyNoiseScale = 28f,
				ClassicBlend = 0.13f,
				ClassicAmplitude = 0.3f,
				ClassicNoiseScale = 28f,
				SubtleBlend = 0.013f,
				SubtleAmplitude = 0.16f,
				SubtleNoiseScale = 85f,
				Peaks = Peaks(0.6f, 0.55f, 0.55f, 0.35f, 0.65f, 0.55f, 0.35f, 0.45f, 0.35f, 0.35f, 0.35f, 0.35f, 0.6f, 0.55f, 0.45f, 0f)
			},
			["360"] = new DoseConfig
			{
				DisplayName = "Acid 360",
				Price = 168f,
				DurationMinutes = 11f,
				PsyBlend = 0.2f,
				PsyAmplitude = 0.36f,
				PsyNoiseScale = 24f,
				ClassicBlend = 0.2f,
				ClassicAmplitude = 0.36f,
				ClassicNoiseScale = 24f,
				SubtleBlend = 0.018f,
				SubtleAmplitude = 0.2f,
				SubtleNoiseScale = 95f,
				Peaks = Peaks(0.85f, 0.8f, 0.75f, 0.5f, 0.8f, 0.6f, 0.55f, 0.65f, 0.55f, 0.55f, 0.5f, 0.5f, 0.85f, 0.75f, 0.65f, 0f)
			}
		};
	}

	public static Dictionary<string, float> BadTripChances()
	{
		return new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["70"] = 0.02f,
			["140"] = 0.04f,
			["280"] = 0.07f,
			["360"] = 0.12f
		};
	}

	public static Dictionary<string, float> PhaseWeights()
	{
		return new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["onset"] = 0.1f,
			["comeUp"] = 0.15f,
			["peak"] = 0.2f,
			["plateau"] = 0.3f,
			["comeDown"] = 0.2f,
			["afterglow"] = 0.05f
		};
	}

	public static List<IngredientConfig> Ingredients()
	{
		return new List<IngredientConfig>
		{
			new IngredientConfig
			{
				ItemId = "megabean",
				Quantity = 1
			},
			new IngredientConfig
			{
				ItemId = "acid",
				Quantity = 1
			}
		};
	}

	public static Dictionary<string, float> PhaseAmplitudes()
	{
		return new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["onset"] = 0f,
			["comeUp"] = 0.55f,
			["peak"] = 1f,
			["plateau"] = 0.88f,
			["comeDown"] = 0.42f,
			["afterglow"] = 0.12f
		};
	}
}
