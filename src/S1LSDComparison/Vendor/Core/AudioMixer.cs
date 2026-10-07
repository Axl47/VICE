// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using S1LSD.Config;

namespace S1LSD.Core;

public sealed class AudioMixer
{
	private AudioConfig _config;

	private const float ReverbChannelMax = 0.55f;

	private const float EchoChannelMax = 0.55f;

	private const float PitchChannelMax = 0.5f;

	private const float PulseChannelMax = 0.5f;

	public AudioMixer(AudioConfig config)
	{
		_config = config;
	}

	public void SetConfig(AudioConfig config)
	{
		_config = config;
	}

	public AudioTargets Mix(in AudioInputs input)
	{
		AudioTargets result = default(AudioTargets);
		if (!input.Active)
		{
			result.ReverbRoomMb = -10000f;
			result.LowPassHz = 22000f;
			result.EchoDry = 1f;
			result.ChorusDry = 1f;
			return result;
		}
		AudioConfig config = _config;
		float num = Math.Clamp(config.FilterIntensity, 0f, 1.5f) * (input.Gentle ? 0.6f : 1f);
		float num2 = Math.Clamp(config.AmbienceVolume, 0f, 1f) * (input.Gentle ? 0.6f : 1f);
		float num3 = Math.Clamp(input.BadTripBlend, 0f, 1f);
		float t = Math.Clamp(input.ClosedEyes, 0f, 1f);
		float num4 = Math.Clamp(input.Breath, 0f, 1f);
		ChannelSnapshot channels = input.Channels;
		float val = Math.Clamp(channels.ReverbWet / 0.55f, 0f, 1.2f) * num;
		float num5 = Math.Clamp(channels.Echo / 0.55f, 0f, 1.2f) * num;
		float val2 = Math.Clamp(channels.PitchDrift / 0.5f, 0f, 1.2f) * num;
		float num6 = Math.Clamp(channels.LowFreqPulse / 0.5f, 0f, 1.2f) * num;
		float num7 = Math.Clamp(Math.Max(val, 0.8f * num3), 0f, 1f);
		result.ReverbOn = num7 > 0.02f;
		result.ReverbRoomMb = Lerp(-3500f, config.ReverbMaxRoomMb, num7);
		result.ReverbRoomHfMb = Lerp(-400f, -1500f, num3);
		result.ReverbDecaySeconds = Lerp(1.2f, 3.2f, num7) + 1.3f * num3;
		result.ReverbLevelMb = Lerp(-1400f, -300f, num7);
		result.ReverbDryMb = -150f * num7;
		float num8 = Math.Clamp(Math.Max(num5, 0.7f * num3), 0f, 1f);
		result.EchoOn = num8 > 0.02f;
		float a = ((config.EchoDelayMs.Length != 0) ? config.EchoDelayMs[0] : 180f);
		float num9 = ((config.EchoDelayMs.Length > 1) ? config.EchoDelayMs[1] : 350f);
		float a2 = ((config.EchoDecay.Length != 0) ? config.EchoDecay[0] : 0.25f);
		float b = ((config.EchoDecay.Length > 1) ? config.EchoDecay[1] : 0.4f);
		result.EchoDelayMs = Math.Clamp(Lerp(a, num9, num8) * (1f + 0.1f * input.TimeDilation), 50f, num9 * 1.1f);
		result.EchoDecay = Math.Clamp(Lerp(a2, b, num8), 0f, 0.45f);
		result.EchoWet = Math.Clamp(config.EchoMaxWet * num8, 0f, 0.4f);
		result.EchoDry = 1f - 0.35f * result.EchoWet;
		float num10 = Math.Clamp(Math.Max(val2, 0.6f * num3), 0f, 1f);
		result.ChorusOn = num10 > 0.02f;
		result.ChorusWet = Math.Clamp(config.ChorusMaxWet * num10, 0f, 0.2f);
		result.ChorusDry = 1f - 0.4f * result.ChorusWet;
		result.ChorusDepth = Lerp(0.04f, 0.14f, num10);
		result.ChorusRateHz = Lerp(0.35f, 0.18f, Math.Max(input.TimeDilation, num3));
		float value = num6 * (0.55f + 0.45f * (1f - num4));
		float a3 = ExpLerp(ExpLerp(22000f, config.LowPassMinHz, Math.Clamp(value, 0f, 1f)), config.ClosedEyesLowPassHz, t);
		result.LowPassHz = ExpLerp(a3, config.BadTripLowPassHz, num3);
		result.LowPassOn = result.LowPassHz < 21500f;
		float num11 = Math.Clamp(Math.Max(val, num5), 0f, 1f);
		float num12 = 0.85f + 0.3f * num4;
		result.Drone = 0.1f * num11 * num12 * num2 * (1f - num3);
		float num13 = 0.045f * num11;
		PhaseId phase = input.Phase;
		bool flag = (uint)(phase - 2) <= 1u;
		result.Shimmer = num13 * (flag ? 1f : 0.4f) * num2 * (1f - num3);
		result.ComeUpPulse = ((input.Phase == PhaseId.ComeUp) ? (0.05f * Bell(input.PhaseProgress) * num2) : 0f);
		result.BadDrone = 0.12f * num3 * num2;
		result.Heart = 0.14f * Math.Max(num3, 0.45f * Math.Clamp(input.Unease, 0f, 1f)) * num2;
		result.HeartRate = Math.Clamp(((input.HeartBpm > 0f) ? input.HeartBpm : 64f) / 64f, 0.9f, 2.1f);
		result.Whisper = 0.05f * num3 * num2;
		return result;
	}

	private static float Lerp(float a, float b, float t)
	{
		return a + (b - a) * Math.Clamp(t, 0f, 1f);
	}

	private static float ExpLerp(float a, float b, float t)
	{
		return MathF.Exp(MathF.Log(MathF.Max(a, 1f)) + (MathF.Log(MathF.Max(b, 1f)) - MathF.Log(MathF.Max(a, 1f))) * Math.Clamp(t, 0f, 1f));
	}

	private static float Bell(float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return MathF.Sin((float)Math.PI * t);
	}
}
