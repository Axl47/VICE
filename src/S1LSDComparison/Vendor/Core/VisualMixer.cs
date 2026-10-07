// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using System.Collections.Generic;

namespace S1LSD.Core;

public sealed class VisualMixer
{
	private sealed class StyleTable
	{
		public float[] Blend;

		public float[] Amplitude;

		public float[] NoiseScale;

		public float[] Lens;

		public float[] Fov;

		public float[] Chroma;

		public float[] Exposure;

		public float[] BloomMax;

		public float[] BloomThresholdMin;

		public float[] MotionBlurMax;

		public float SaturationGain;

		public float ContrastGain;
	}

	public const float GazeMax = 1.25f;

	private readonly ValueNoise _noise;

	private static readonly StyleTable ClassicTable = new StyleTable
	{
		Blend = new float[4] { 0.03f, 0.07f, 0.13f, 0.2f },
		Amplitude = new float[4] { 0.15f, 0.22f, 0.3f, 0.36f },
		NoiseScale = new float[4] { 40f, 34f, 28f, 24f },
		Lens = new float[4] { 0.03f, 0.07f, 0.12f, 0.18f },
		Fov = new float[4] { 0.8f, 1.6f, 2.6f, 3.6f },
		Chroma = new float[4] { 0.05f, 0.09f, 0.15f, 0.22f },
		Exposure = new float[4] { 0.25f, 0.35f, 0.5f, 0.6f },
		BloomMax = new float[4] { 0.6f, 0.85f, 1.15f, 1.4f },
		BloomThresholdMin = new float[4] { 0.95f, 0.92f, 0.88f, 0.84f },
		MotionBlurMax = new float[4] { 0.12f, 0.22f, 0.35f, 0.45f },
		SaturationGain = 34f,
		ContrastGain = 22f
	};

	private static readonly StyleTable SubtleTable = new StyleTable
	{
		Blend = new float[4] { 0.002f, 0.008f, 0.013f, 0.018f },
		Amplitude = new float[4] { 0.05f, 0.12f, 0.16f, 0.2f },
		NoiseScale = new float[4] { 55f, 65f, 85f, 95f },
		Lens = new float[4] { 0f, 0.008f, 0.035f, 0.065f },
		Fov = new float[4] { 0f, 0.55f, 1.25f, 1.85f },
		Chroma = new float[4] { 0f, 0.04f, 0.08f, 0.12f },
		Exposure = new float[4] { 0.2f, 0.25f, 0.35f, 0.4f },
		BloomMax = new float[4] { 0.45f, 0.6f, 0.75f, 0.85f },
		BloomThresholdMin = new float[4] { 0.97f, 0.95f, 0.92f, 0.9f },
		MotionBlurMax = new float[4] { 0.06f, 0.12f, 0.2f, 0.25f },
		SaturationGain = 26f,
		ContrastGain = 18f
	};

	private static readonly float[] CrawlPixelsSubtle = new float[4] { 0.65f, 1.3f, 3.9f, 7.8f };

	private static readonly float[] CrawlPixelsClassic = new float[4] { 1f, 2.1f, 5.8f, 11f };

	private static readonly float[] HueDriftDegrees = new float[4] { 5f, 9f, 15f, 22f };

	private static readonly float[] FringePixels = new float[4] { 0.3f, 0.6f, 0.9f, 1.3f };

	private static readonly float[] TrailTauMax = new float[4] { 0.15f, 0.25f, 0.38f, 0.45f };

	private static readonly Dictionary<string, float[]> DefaultPeaks = new Dictionary<string, float[]>
	{
		["textureCrawl"] = new float[4] { 0.1f, 0.3f, 0.6f, 0.85f },
		["warp"] = new float[4] { 0.1f, 0.3f, 0.6f, 0.85f },
		["chroma"] = new float[4] { 0.05f, 0.15f, 0.35f, 0.5f },
		["trails"] = new float[4] { 0.08f, 0.25f, 0.55f, 0.8f }
	};

	public VisualMixer(int seed)
	{
		_noise = new ValueNoise(seed ^ 0x51A1);
	}

	public static int DoseIndex(int dose)
	{
		if (dose <= 140)
		{
			if (dose <= 70)
			{
				return 0;
			}
			return 1;
		}
		if (dose <= 280)
		{
			return 2;
		}
		return 3;
	}

	public static float PeakBlend(int dose, bool classic)
	{
		return (classic ? ClassicTable : SubtleTable).Blend[DoseIndex(dose)];
	}

	public VisualTargets Mix(in VisualInputs input)
	{
		StyleTable styleTable = ((input.Classic && !input.Gentle) ? ClassicTable : SubtleTable);
		int num = DoseIndex(input.Dose);
		ChannelSnapshot channels = input.Channels;
		float num2 = (input.Gentle ? 0.6f : 1f);
		float num3 = ((input.Overdrive && !input.Gentle) ? 1.6f : 1f);
		float num4 = Math.Clamp(input.BadTripBlend, 0f, 1f);
		float num5 = Math.Clamp(input.Darkness, 0f, 1f);
		float num6 = Math.Clamp(input.Breath, 0f, 1f);
		float num7 = Math.Clamp(input.Gaze, 1f, 1.25f);
		float t = Math.Clamp(input.ClosedEyes, 0f, 1f);
		float num8 = Norm(channels.TextureCrawl, PeakFor(num, "textureCrawl"));
		float num9 = Norm(channels.Warp, PeakFor(num, "warp"));
		float num10 = Math.Clamp(channels.Halo, 0f, 1f);
		float num11 = Math.Clamp(channels.Pupil, 0f, 1f);
		float num12 = Math.Clamp(channels.ColorDrift * Math.Clamp(input.ColorVibrancy, 0.5f, 2f), 0f, 1.5f);
		float num13 = Math.Clamp(channels.LocalContrast, 0f, 1f);
		float value = Math.Clamp(channels.Trails, 0f, 1f);
		float num14 = _noise.Fbm(input.Time * 0.031f, 2);
		float num15 = _noise.Fbm(input.Time * 0.017f + 37.1f, 2);
		float num16 = Math.Clamp(input.BassPulse, 0f, 1f);
		float num17 = Math.Clamp(input.MidPulse, 0f, 1f);
		float num18 = Math.Clamp(input.HighPulse, 0f, 1f);
		VisualTargets result = default(VisualTargets);
		float num19 = ((input.CrawlBlendPeak > 0f) ? input.CrawlBlendPeak : styleTable.Blend[num]);
		float num20 = ((input.CrawlAmplitudePeak > 0f) ? input.CrawlAmplitudePeak : styleTable.Amplitude[num]);
		float num21 = ((input.CrawlNoiseScale > 0f) ? input.CrawlNoiseScale : styleTable.NoiseScale[num]);
		float num22 = num8 * num7 * num2 * num3;
		result.PsyBlend = num19 * num8 * num2 * num3 * (1f + 0.25f * num4);
		result.PsyAmplitude = num20 * num22 * (0.85f + 0.3f * num6);
		result.PsyNoiseScale = num21 * (1f - 0.2f * num4);
		if (input.ShaderActive)
		{
			result.PsyBlend = 0f;
			result.PsyAmplitude = 0f;
		}
		float num23 = (num6 - 0.5f) * 2f;
		float num24 = styleTable.Lens[num] * num9 * (0.75f + 0.25f * num23) * num2 * num3;
		num24 += styleTable.Lens[num] * num9 * 0.15f * num16;
		float b = (0f - styleTable.Lens[num]) * (0.6f + 0.15f * num14) * num4;
		result.LensDistortion = Math.Clamp(Lerp(num24, b, num4), -0.3f, 0.3f);
		float value2 = styleTable.Fov[num] * num9 * num23 * 0.6f * num2 * num3;
		result.FovDelta = Math.Clamp(value2, 0f - styleTable.Fov[num], styleTable.Fov[num]);
		float num25 = Math.Clamp(input.Unease, 0f, 1f);
		result.FovDelta -= (input.Gentle ? 1.2f : 2.5f) * Math.Max(num4, 0.3f * num25);
		float num26 = 1f + 0.22f * (0.6f * num16 + 0.4f * num18);
		result.PostExposure = styleTable.Exposure[num] * num11 * (1f - 0.35f * num5) * num26 * num2;
		float num27 = Math.Clamp(input.Euphoria, 0f, 1f);
		result.PostExposure += 0.05f * num27 * (1f - num4);
		result.PostExposure = Lerp(result.PostExposure, -0.25f, num4);
		result.PostExposure = Lerp(result.PostExposure, -2.5f, t);
		float num28 = styleTable.BloomMax[num] * (1f + 0.3f * num5) * Math.Clamp(num10 * 1.2f + num11 * 0.4f, 0f, 1f);
		result.BloomGain = 1f + Math.Clamp(num28 * num2 * num3, 0f, 2f);
		result.BloomGain = Math.Clamp(1f + (result.BloomGain - 1f) * num26, 1f, 3f);
		result.BloomGain = Lerp(result.BloomGain, 1.2f, num4);
		result.BloomThresholdDrop = (1f - styleTable.BloomThresholdMin[num]) * Math.Clamp(num10 + 0.4f * num5, 0f, 1f);
		result.BloomTint = Rgb.Lerp(Rgb.White, new Rgb(1f, 0.95f, 0.88f), num12 * 0.5f);
		result.BloomTint = Rgb.Lerp(result.BloomTint, new Rgb(0.8f, 0.92f, 1f), num4);
		float num29 = styleTable.Chroma[num] * Math.Clamp(Norm(channels.Chroma, PeakFor(num, "chroma")), 0f, 1f);
		num29 *= 1f + 0.22f * num18;
		result.Chroma = Math.Clamp(num29 * num2 + 0.25f * input.SaccadeBurst * styleTable.Chroma[num], 0f, 0.3f);
		if (input.ShaderActive)
		{
			result.Chroma *= 0.25f;
		}
		float num30 = 1f - 0.3f * num5;
		float num31 = styleTable.SaturationGain * num12 * num30 * (1f + 0.22f * num17) * num2;
		num31 += 4f * num15 * num12;
		num31 += 6f * num27 - 8f * num25;
		if (input.ShaderActive)
		{
			num31 *= 0.6f;
		}
		result.Saturation = Lerp(num31, -55f * (input.Gentle ? 0.65f : 1f), num4);
		result.Contrast = Lerp(styleTable.ContrastGain * num13 * num2, 30f, num4);
		result.HueShift = PaletteHue(input.Palette) * Math.Clamp(num12, 0f, 1f);
		(Rgb, Rgb, float, float) tuple = PaletteGrade(input.Palette);
		Rgb item = tuple.Item1;
		Rgb item2 = tuple.Item2;
		float item3 = tuple.Item3;
		float item4 = tuple.Item4;
		float num32 = Math.Clamp(num12, 0f, 1f) * num2;
		result.Shadows = Rgb.Lerp(Rgb.Black, item, num32 * (0.7f + 0.3f * num5));
		result.Highlights = Rgb.Lerp(Rgb.White, item2, num32 * num30);
		result.SplitBalance = 10f * num14 * num32;
		result.Temperature = item3 * num32 + 3f * num15 * num32 + 5f * num27 - 10f * num25;
		result.Tint = item4 * num32;
		result.Shadows = Rgb.Lerp(result.Shadows, new Rgb(0.1f, 0.12f, 0.16f), num4);
		result.Highlights = Rgb.Lerp(result.Highlights, new Rgb(0.86f, 0.92f, 1f), num4);
		result.Temperature = Lerp(result.Temperature, -30f, num4);
		result.Tint = Lerp(result.Tint, 8f, num4);
		result.SplitBalance = Lerp(result.SplitBalance, 20f, num4);
		float num33 = Math.Max(Math.Clamp(channels.Tunnel, 0f, 1f), 0.7f * num4) * (input.Gentle ? 0.65f : 1f);
		if (input.ShaderActive)
		{
			num33 *= 0.35f;
		}
		float num34 = Math.Clamp(input.HeartPulse, 0f, 1f) * 0.06f * Math.Max(num4, 0.4f * num25);
		result.VignetteIntensity = Math.Clamp(0.12f + 0.05f * num8 + num33 * 0.6f + 0.05f * num25 + num34 * (input.Gentle ? 0f : 1f), 0f, 0.8f);
		result.VignetteSmoothness = Lerp(0.45f, 0.3f, num4);
		result.VignetteIntensity = Lerp(result.VignetteIntensity, 0.95f, t);
		result.VignetteSmoothness = Lerp(result.VignetteSmoothness, 0.85f, t);
		result.MotionBlur = Math.Clamp(styleTable.MotionBlurMax[num] * Norm(value, PeakFor(num, "trails")) + 0.15f * input.SaccadeBurst, 0f, styleTable.MotionBlurMax[num]) * num2;
		if (input.ShaderActive)
		{
			result.MotionBlur = 0f;
		}
		result.FilmGrain = Math.Clamp(0.02f + 0.03f * num8 + (input.Gentle ? 0f : (0.15f * num4)), 0f, 0.2f);
		return result;
	}

	public ShaderTargets MixShader(in VisualInputs input)
	{
		bool num = input.Classic && !input.Gentle;
		int num2 = DoseIndex(input.Dose);
		ChannelSnapshot channels = input.Channels;
		float num3 = (input.Gentle ? 0.6f : 1f);
		float num4 = ((input.Overdrive && !input.Gentle) ? 1.4f : 1f);
		float num5 = Math.Clamp(input.BadTripBlend, 0f, 1f);
		float num6 = Math.Clamp(input.Darkness, 0f, 1f);
		float num7 = 1f - Math.Clamp(input.ClosedEyes, 0f, 1f);
		float num8 = Math.Clamp(input.Unease, 0f, 1f);
		float time = input.Time;
		float val = Norm(channels.TextureCrawl, PeakFor(num2, "textureCrawl"));
		float num9 = Norm(channels.Warp, PeakFor(num2, "warp"));
		float val2 = Norm(channels.Trails, PeakFor(num2, "trails"));
		float val3 = Norm(channels.Chroma, PeakFor(num2, "chroma"));
		float val4 = Math.Clamp(channels.ColorDrift * Math.Clamp(input.ColorVibrancy, 0.5f, 2f), 0f, 1.5f);
		float num10 = (Math.Clamp(input.Breath, 0f, 1f) - 0.5f) * 2f;
		ShaderTargets result = default(ShaderTargets);
		float num11 = (num ? CrawlPixelsClassic : CrawlPixelsSubtle)[num2];
		result.Crawl = num11 / 1080f / 1.2f * Math.Min(val, 1f) * num3 * num4 * (1f + 0.3f * num5) * num7;
		result.CrawlSpeed = Lerp(0.05f, 0.03f, num5) * (0.85f + 0.3f * Math.Clamp(input.Breath, 0f, 1f));
		result.Breathe = 0.002f * (float)(num2 + 1) * num9 * num10 * num3;
		result.Gaze = Math.Clamp((input.Gaze - 1f) / 0.25f, 0f, 1f);
		result.Vibrance = Math.Clamp(0.6f * Math.Min(val4, 1f) * num3 * (1f - 0.8f * num5), 0f, 1f);
		float num12 = HueDriftDegrees[num2] * Math.Min(val4, 1f) * num3 * (1f - num5) * (float)Math.PI / 180f;
		result.HueShadows = num12 * _noise.Fbm(time * 0.013f + 3f, 2);
		result.HueMids = num12 * _noise.Fbm(time * 0.011f + 7f, 2);
		result.HueHighs = num12 * _noise.Fbm(time * 0.009f + 13f, 2);
		result.LocalContrast = Math.Clamp(0.4f * channels.LocalContrast * num3 + 0.15f * num5, 0f, 0.5f);
		result.Fringe = FringePixels[num2] * Math.Min(val3, 1.2f) * num3 + 0.4f * input.SaccadeBurst;
		result.Warmth = Math.Clamp(input.Euphoria - num8 - 0.5f * num5, -1f, 1f);
		float num13 = Math.Clamp(channels.Patterns, 0f, 1f) * (0.5f + 0.5f * num6);
		result.Patterns = Math.Clamp(0.35f * num13 * num3 * num7 * num4, 0f, 0.35f);
		float num14 = Weight(time, 1f) * ((num2 <= 1) ? 1.3f : 1f);
		float num15 = Weight(time, 2f) * ((num2 >= 2) ? 1.2f : 0.5f) + 2f * num5;
		float num16 = Weight(time, 3f) * ((num2 >= 2) ? 1f : 0.3f);
		float num17 = Weight(time, 4f) * ((num2 <= 2) ? 1f : 0.7f);
		float num18 = num14 + num15 + num16 + num17;
		result.PatternLattice = num14 / num18;
		result.PatternTunnel = num15 / num18;
		result.PatternSpiral = num16 / num18;
		result.PatternCobweb = num17 / num18;
		result.PatternScale = 1f + 0.12f * num10;
		result.Tunnel = Math.Clamp(Math.Max(channels.Tunnel, 0.75f * num5) * (input.Gentle ? 0.6f : 1f), 0f, 1f);
		result.Shadows = (input.Gentle ? 0f : Math.Clamp(0.8f * num5, 0f, 0.8f));
		result.Heart = Math.Clamp(input.HeartPulse, 0f, 1f) * Math.Max(num5, 0.4f * num8);
		float num19 = TrailTauMax[num2] * Math.Min(val2, 1.2f) + 0.12f * input.SaccadeBurst + 0.08f * num5;
		result.TrailTau = ((num19 < 0.02f) ? 0f : Math.Clamp(num19 * (input.Gentle ? 0.7f : 1f), 0f, 0.5f));
		result.TrailBright = Lerp(0.85f, 0.55f, (float)num2 / 3f);
		return result;
	}

	private float Weight(float time, float salt)
	{
		return MathF.Max(0.05f, 0.6f + _noise.Fbm(time * 0.02f + salt * 31f, 2));
	}

	public float Tremor(float time, float badBlend, bool gentle)
	{
		if (gentle || badBlend <= 0f)
		{
			return 0f;
		}
		return _noise.Fbm(time * 1.6f + 101f, 2) * 0.3f * Math.Clamp(badBlend, 0f, 1f);
	}

	private static float PaletteHue(PsychedelicPalette palette)
	{
		return palette switch
		{
			PsychedelicPalette.GoldenEuphoria => 4f,
			PsychedelicPalette.Cosmic => -6f,
			PsychedelicPalette.Shamanic => 3f,
			_ => 0f,
		};
	}

	private static (Rgb, Rgb, float, float) PaletteGrade(PsychedelicPalette palette)
	{
		return palette switch
		{
			PsychedelicPalette.GoldenEuphoria => (new Rgb(0.22f, 0.1f, 0.36f), new Rgb(1f, 0.86f, 0.55f), 14f, 3f),
			PsychedelicPalette.Prismatic => (new Rgb(0.06f, 0.26f, 0.36f), new Rgb(1f, 0.82f, 0.9f), 4f, 6f),
			PsychedelicPalette.Cosmic => (new Rgb(0.24f, 0.06f, 0.48f), new Rgb(0.72f, 0.94f, 1f), -8f, 6f),
			PsychedelicPalette.Shamanic => (new Rgb(0.08f, 0.2f, 0.1f), new Rgb(1f, 0.86f, 0.6f), 12f, -4f),
			_ => (new Rgb(0.16f, 0.1f, 0.36f), new Rgb(1f, 0.93f, 0.76f), 9f, 0f),
		};
	}

	private static float PeakFor(int doseIndex, string key)
	{
		return DefaultPeaks[key][doseIndex];
	}

	private static float Norm(float value, float peak)
	{
		if (!(peak > 0.0001f))
		{
			return 0f;
		}
		return Math.Clamp(value / peak, 0f, 1.3f);
	}

	private static float Lerp(float a, float b, float t)
	{
		return a + (b - a) * Math.Clamp(t, 0f, 1f);
	}
}
