// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class ToleranceConfig
{
	[JsonProperty("enabled")]
	public bool Enabled { get; set; } = true;

	[JsonProperty("baseGain")]
	public float BaseGain { get; set; } = 0.35f;

	[JsonProperty("referenceDose")]
	public float ReferenceDose { get; set; } = 140f;

	[JsonProperty("gainExponent")]
	public float GainExponent { get; set; } = 0.5f;

	[JsonProperty("maxReduction")]
	public float MaxReduction { get; set; } = 0.8f;

	[JsonProperty("durationReductionMax")]
	public float DurationReductionMax { get; set; } = 0.2f;

	[JsonProperty("halfLifeGameDays")]
	public float HalfLifeGameDays { get; set; } = 2f;
}
