// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class PhaseConfig
{
	[JsonProperty("weights")]
	public Dictionary<string, float> Weights { get; set; } = Defaults.PhaseWeights();

	[JsonProperty("amplitude")]
	public Dictionary<string, float> Amplitude { get; set; } = Defaults.PhaseAmplitudes();

	[JsonProperty("channelBias")]
	public Dictionary<string, Dictionary<string, float>> ChannelBias { get; set; } = new Dictionary<string, Dictionary<string, float>>();

	[JsonProperty("noise")]
	public PhaseNoiseConfig Noise { get; set; } = new PhaseNoiseConfig();

	[JsonProperty("tripVariation")]
	public float TripVariation { get; set; } = 0.12f;
}
