// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class DoseConfig
{
	[JsonProperty("displayName")]
	public string DisplayName { get; set; } = "Acid";

	[JsonProperty("price")]
	public float Price { get; set; } = 50f;

	[JsonProperty("durationMinutes")]
	public float DurationMinutes { get; set; } = 3.5f;

	[JsonProperty("psyBlend")]
	public float PsyBlend { get; set; } = 0.08f;

	[JsonProperty("psyAmplitude")]
	public float PsyAmplitude { get; set; } = 0.15f;

	[JsonProperty("psyNoiseScale")]
	public float PsyNoiseScale { get; set; } = 24f;

	[JsonProperty("classicBlend")]
	public float? ClassicBlend { get; set; }

	[JsonProperty("classicAmplitude")]
	public float? ClassicAmplitude { get; set; }

	[JsonProperty("classicNoiseScale")]
	public float? ClassicNoiseScale { get; set; }

	[JsonProperty("subtleBlend")]
	public float? SubtleBlend { get; set; }

	[JsonProperty("subtleAmplitude")]
	public float? SubtleAmplitude { get; set; }

	[JsonProperty("subtleNoiseScale")]
	public float? SubtleNoiseScale { get; set; }

	[JsonProperty("peaks")]
	public Dictionary<string, float> Peaks { get; set; } = Defaults.Peaks();

	public float GetBlend(bool isClassic)
	{
		if (!isClassic)
		{
			float? subtleBlend = SubtleBlend;
			if (!subtleBlend.HasValue)
			{
				if (!(PsyBlend <= 0.03f))
				{
					return 0.01f;
				}
				return PsyBlend;
			}
			return subtleBlend.GetValueOrDefault();
		}
		return ClassicBlend ?? PsyBlend;
	}

	public float GetAmplitude(bool isClassic)
	{
		if (!isClassic)
		{
			float? subtleAmplitude = SubtleAmplitude;
			if (!subtleAmplitude.HasValue)
			{
				if (!(PsyAmplitude <= 0.25f))
				{
					return 0.15f;
				}
				return PsyAmplitude;
			}
			return subtleAmplitude.GetValueOrDefault();
		}
		return ClassicAmplitude ?? PsyAmplitude;
	}

	public float GetNoiseScale(bool isClassic)
	{
		if (!isClassic)
		{
			float? subtleNoiseScale = SubtleNoiseScale;
			if (!subtleNoiseScale.HasValue)
			{
				if (!(PsyNoiseScale >= 40f))
				{
					return 65f;
				}
				return PsyNoiseScale;
			}
			return subtleNoiseScale.GetValueOrDefault();
		}
		return ClassicNoiseScale ?? PsyNoiseScale;
	}
}
