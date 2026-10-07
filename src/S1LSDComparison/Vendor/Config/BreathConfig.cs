// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class BreathConfig
{
	[JsonProperty("baseHz")]
	public float BaseHz { get; set; } = 0.22f;

	[JsonProperty("perlinOctaves")]
	public int PerlinOctaves { get; set; } = 3;

	[JsonProperty("perlinFrequency")]
	public float PerlinFrequency { get; set; } = 0.55f;

	[JsonProperty("depth")]
	public float Depth { get; set; } = 0.35f;
}
