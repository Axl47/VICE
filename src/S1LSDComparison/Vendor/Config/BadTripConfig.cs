// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class BadTripConfig
{
	[JsonProperty("enabled")]
	public bool Enabled { get; set; } = true;

	[JsonProperty("chanceByDose")]
	public Dictionary<string, float> ChanceByDose { get; set; } = Defaults.BadTripChances();

	[JsonProperty("durationMinutes")]
	public float DurationMinutes { get; set; } = 6f;

	[JsonProperty("minDurationMinutes")]
	public float MinDurationMinutes { get; set; } = 2f;

	[JsonProperty("endOnSleep")]
	public bool EndOnSleep { get; set; } = true;

	[JsonProperty("tunnelPeak")]
	public float TunnelPeak { get; set; } = 0.85f;

	[JsonProperty("blendSeconds")]
	public float BlendSeconds { get; set; } = 20f;

	[JsonProperty("warpMultiplier")]
	public float WarpMultiplier { get; set; } = 1.6f;

	[JsonProperty("haloMultiplier")]
	public float HaloMultiplier { get; set; } = 1.4f;

	[JsonProperty("colorDriftMultiplier")]
	public float ColorDriftMultiplier { get; set; } = 1.8f;

	[JsonProperty("moveSpeedScale")]
	public float MoveSpeedScale { get; set; } = 0.72f;

	[JsonProperty("useSetting")]
	public bool UseSetting { get; set; } = true;

	[JsonProperty("cooldownMinutes")]
	public float CooldownMinutes { get; set; } = 3f;
}
