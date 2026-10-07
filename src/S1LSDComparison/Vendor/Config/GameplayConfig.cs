// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class GameplayConfig
{
	[JsonProperty("controlPenaltyDoses")]
	public List<int> ControlPenaltyDoses { get; set; } = new List<int> { 280, 360 };

	[JsonProperty("controlPenaltyMoveSpeed")]
	public float ControlPenaltyMoveSpeed { get; set; } = 0.86f;

	[JsonProperty("closedEyesKey")]
	public string ClosedEyesKey { get; set; } = "B";

	[JsonProperty("proceduralIcons")]
	public bool ProceduralIcons { get; set; } = true;

	[JsonProperty("consumeFeedback")]
	public bool ConsumeFeedback { get; set; } = true;

	[JsonProperty("customModel")]
	public bool CustomModel { get; set; } = true;

	[JsonProperty("useEatAnimation")]
	public bool UseEatAnimation { get; set; } = true;

	[JsonProperty("mindfulBreathingKey")]
	public string MindfulBreathingKey { get; set; } = "V";
}
