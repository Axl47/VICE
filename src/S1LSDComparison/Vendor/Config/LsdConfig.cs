// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class LsdConfig
{
	[JsonProperty("schemaVersion")]
	public int SchemaVersion { get; set; } = 2;

	[JsonProperty("quality")]
	public QualityConfig Quality { get; set; } = new QualityConfig();

	[JsonProperty("visualStyle")]
	public string VisualStyle
	{
		get
		{
			return Quality.VisualStyle;
		}
		set
		{
			Quality.VisualStyle = value;
		}
	}

	[JsonProperty("patches")]
	public PatchesConfig Patches { get; set; } = new PatchesConfig();

	[JsonProperty("audio")]
	public AudioConfig Audio { get; set; } = new AudioConfig();

	[JsonProperty("breath")]
	public BreathConfig Breath { get; set; } = new BreathConfig();

	[JsonProperty("tolerance")]
	public ToleranceConfig Tolerance { get; set; } = new ToleranceConfig();

	[JsonProperty("badTrip")]
	public BadTripConfig BadTrip { get; set; } = new BadTripConfig();

	[JsonProperty("gameplay")]
	public GameplayConfig Gameplay { get; set; } = new GameplayConfig();

	[JsonProperty("doses")]
	public Dictionary<string, DoseConfig> Doses { get; set; } = Defaults.Doses();

	[JsonProperty("phases")]
	public PhaseConfig Phases { get; set; } = new PhaseConfig();

	[JsonProperty("economy")]
	public EconomyConfig Economy { get; set; } = new EconomyConfig();

	public bool ShouldSerializeVisualStyle()
	{
		return false;
	}
}
