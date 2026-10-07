// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Data only; no original content, persistence, or economy runtime is included.
#nullable disable
using Newtonsoft.Json;

namespace S1LSD.Config;

public sealed class AudioConfig
{
	[JsonProperty("filterIntensity")]
	public float FilterIntensity { get; set; } = 1f;

	[JsonProperty("ambienceVolume")]
	public float AmbienceVolume { get; set; } = 0.6f;

	[JsonProperty("transitionSeconds")]
	public float TransitionSeconds { get; set; } = 3f;

	[JsonProperty("reverbMaxRoomMb")]
	public float ReverbMaxRoomMb { get; set; } = -900f;

	[JsonProperty("echoDelayMs")]
	public float[] EchoDelayMs { get; set; } = new float[2] { 180f, 350f };

	[JsonProperty("echoDecay")]
	public float[] EchoDecay { get; set; } = new float[2] { 0.25f, 0.4f };

	[JsonProperty("echoMaxWet")]
	public float EchoMaxWet { get; set; } = 0.35f;

	[JsonProperty("chorusMaxWet")]
	public float ChorusMaxWet { get; set; } = 0.15f;

	[JsonProperty("lowPassMinHz")]
	public float LowPassMinHz { get; set; } = 9000f;

	[JsonProperty("closedEyesLowPassHz")]
	public float ClosedEyesLowPassHz { get; set; } = 6000f;

	[JsonProperty("badTripLowPassHz")]
	public float BadTripLowPassHz { get; set; } = 3500f;

	[JsonProperty("useGameDistortedSnapshot")]
	public bool UseGameDistortedSnapshot { get; set; }
}
