// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class QualityConfig
{
	[JsonProperty("visualStyle")]
	public string VisualStyle { get; set; } = "classic";

	[JsonProperty("visualIntensityScale")]
	public float VisualIntensityScale { get; set; } = 1f;

	[JsonProperty("gentleMode")]
	public bool GentleMode { get; set; }

	[JsonProperty("maxFlickerHz")]
	public float MaxFlickerHz { get; set; } = 3f;

	[JsonProperty("customShaders")]
	public bool CustomShaders { get; set; } = true;

	[JsonProperty("firstLaunchWarningShown")]
	public bool FirstLaunchWarningShown { get; set; }
}
