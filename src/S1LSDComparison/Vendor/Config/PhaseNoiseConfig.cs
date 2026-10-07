// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class PhaseNoiseConfig
{
	[JsonProperty("amplitude")]
	public float Amplitude { get; set; } = 0.06f;

	[JsonProperty("frequency")]
	public float Frequency { get; set; } = 0.9f;
}
