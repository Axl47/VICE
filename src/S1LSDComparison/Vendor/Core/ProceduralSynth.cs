// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;

namespace S1LSD.Core;

public static class ProceduralSynth
{
	public const int SampleRate = 22050;

	public static float[] Drone(float seconds = 20f)
	{
		float[] x = new float[Length(seconds)];
		float[] freqs = new float[7] { 55f, 55.1f, 82.5f, 82.45f, 110f, 110.15f, 165f };
		float[] amps = new float[7] { 1f, 0.8f, 0.45f, 0.4f, 0.3f, 0.25f, 0.08f };
		AddPartials(x, seconds, freqs, amps);
		SlowSwell(x, seconds, 2, 0.25f, 0.3f);
		return Normalise(x);
	}

	public static float[] Shimmer(float seconds = 20f)
	{
		int num = Length(seconds);
		float[] array = new float[num];
		XorShift32 xorShift = new XorShift32(23070u);
		for (int i = 7; i <= 15; i++)
		{
			float num2 = Quantise(220f * (float)i, seconds);
			int num3 = (int)(1 + xorShift.NextUInt() % 3);
			float num4 = xorShift.NextFloat();
			float num5 = 0.4f / (float)i;
			for (int j = 0; j < num; j++)
			{
				float num6 = (float)j / 22050f;
				float num7 = 0.5f - 0.5f * MathF.Cos((float)Math.PI * 2f * ((float)num3 * num6 / seconds + num4));
				array[j] += num5 * num7 * num7 * MathF.Sin((float)Math.PI * 2f * num2 * num6);
			}
		}
		return Normalise(array);
	}

	public static float[] ComeUpPulse(float seconds = 16f)
	{
		int num = Length(seconds);
		float[] array = new float[num];
		int num2 = (int)MathF.Round(seconds * 0.75f);
		float num3 = seconds / (float)num2;
		float num4 = Quantise(45f, seconds);
		for (int i = 0; i < num; i++)
		{
			float num5 = (float)i / 22050f;
			float num6 = num5 % num3;
			float num7 = MathF.Exp((0f - num6) / 0.18f) * (1f - MathF.Exp((0f - num6) / 0.03f));
			array[i] = num7 * MathF.Sin((float)Math.PI * 2f * num4 * num5);
		}
		return Normalise(array);
	}

	public static float[] BadDrone(float seconds = 20f)
	{
		float[] x = new float[Length(seconds)];
		float[] freqs = new float[6] { 58f, 61.45f, 82f, 82.2f, 116f, 164.1f };
		float[] amps = new float[6] { 1f, 0.55f, 0.6f, 0.5f, 0.3f, 0.12f };
		AddPartials(x, seconds, freqs, amps);
		SlowSwell(x, seconds, 3, 0.35f, 0.7f);
		return Normalise(x);
	}

	public static float[] Heartbeat(float seconds = 15f)
	{
		int num = Length(seconds);
		float[] array = new float[num];
		int num2 = (int)MathF.Round(seconds * 64f / 60f);
		float num3 = seconds / (float)num2;
		float num4 = Quantise(48f, seconds);
		float num5 = Quantise(40f, seconds);
		for (int i = 0; i < num; i++)
		{
			float num6 = (float)i / 22050f;
			float local = num6 % num3;
			float num7 = Thump(local, 0f, 0.09f);
			float num8 = 0.6f * Thump(local, 0.28f, 0.07f);
			array[i] = num7 * MathF.Sin((float)Math.PI * 2f * num4 * num6) + num8 * MathF.Sin((float)Math.PI * 2f * num5 * num6);
		}
		return Normalise(array);
	}

	public static float[] Whisper(float seconds = 20f)
	{
		int num = Length(seconds);
		float[] array = new float[num];
		XorShift32 xorShift = new XorShift32(741031u);
		for (int i = 0; i < num; i++)
		{
			array[i] = xorShift.NextFloat() * 2f - 1f;
		}
		float num2 = OnePoleCoefficient(1500f);
		float num3 = OnePoleCoefficient(4000f);
		float[] array2 = new float[num];
		float num4 = 0f;
		float num5 = 0f;
		float num6 = 0f;
		float num7 = 0f;
		float num8 = 0f;
		float num9 = 0f;
		for (int j = 0; j < 2; j++)
		{
			for (int k = 0; k < num; k++)
			{
				float num10 = array[k];
				num4 = num2 * (num4 + num10 - num8);
				num8 = num10;
				num5 = num2 * (num5 + num4 - num9);
				num9 = num4;
				num6 += (num5 - num6) * (1f - num3);
				num7 = (array2[k] = num7 + (num6 - num7) * (1f - num3));
			}
		}
		for (int l = 0; l < num; l++)
		{
			float num11 = (float)l / (float)num;
			float y = 0.5f + 0.25f * MathF.Cos((float)Math.PI * 6f * num11 + 1.1f) + 0.15f * MathF.Cos((float)Math.PI * 10f * num11 + 2.3f) + 0.1f * MathF.Cos((float)Math.PI * 14f * num11 + 0.4f);
			array2[l] *= MathF.Max(0f, y) * MathF.Max(0f, y);
		}
		return Normalise(array2);
	}

	public static float[] PaperTick(float seconds = 0.45f)
	{
		int num = Length(seconds);
		float[] array = new float[num];
		XorShift32 xorShift = new XorShift32(31413u);
		float num2 = OnePoleCoefficient(1200f);
		float num3 = OnePoleCoefficient(6000f);
		float num4 = 0f;
		float num5 = 0f;
		float num6 = 0f;
		for (int i = 0; i < num; i++)
		{
			float num7 = (float)i / 22050f;
			float num8 = xorShift.NextFloat() * 2f - 1f;
			num4 = num2 * (num4 + num8 - num5);
			num5 = num8;
			num6 += (num4 - num6) * (1f - num3);
			float num9 = MathF.Exp((0f - num7) / 0.05f) + 0.6f * Thump(num7, 0.12f, 0.06f);
			float num10 = Math.Clamp(num7 / 0.005f, 0f, 1f) * Math.Clamp((seconds - num7) / 0.005f, 0f, 1f);
			array[i] = num6 * num9 * num10;
		}
		return Normalise(array);
	}

	private static int Length(float seconds)
	{
		return Math.Max(1, (int)MathF.Round(seconds * 22050f));
	}

	public static float Quantise(float hz, float seconds)
	{
		return MathF.Round(hz * seconds) / seconds;
	}

	private static void AddPartials(float[] x, float seconds, float[] freqs, float[] amps)
	{
		for (int i = 0; i < freqs.Length; i++)
		{
			float num = Quantise(freqs[i], seconds);
			float num2 = (float)Math.PI * 2f * num / 22050f;
			for (int j = 0; j < x.Length; j++)
			{
				x[j] += amps[i] * MathF.Sin(num2 * (float)j + (float)i * 0.7f);
			}
		}
	}

	private static void SlowSwell(float[] x, float seconds, int cycles, float depth, float phase)
	{
		for (int i = 0; i < x.Length; i++)
		{
			float num = (float)i / (float)x.Length;
			x[i] *= 1f - depth + depth * (0.5f + 0.5f * MathF.Cos((float)Math.PI * 2f * ((float)cycles * num + phase)));
		}
	}

	private static float Thump(float local, float start, float length)
	{
		float num = local - start;
		if (num < 0f)
		{
			return 0f;
		}
		return MathF.Exp((0f - num) / length) * (1f - MathF.Exp((0f - num) / 0.012f));
	}

	private static float OnePoleCoefficient(float cutoffHz)
	{
		return MathF.Exp((float)Math.PI * -2f * cutoffHz / 22050f);
	}

	private static float[] Normalise(float[] x)
	{
		double num = 0.0;
		foreach (float num2 in x)
		{
			num += (double)num2;
		}
		num /= (double)x.Length;
		float num3 = 0f;
		for (int j = 0; j < x.Length; j++)
		{
			x[j] -= (float)num;
			num3 = MathF.Max(num3, MathF.Abs(x[j]));
		}
		if (num3 > 1E-09f)
		{
			float num4 = 0.5f / num3;
			for (int k = 0; k < x.Length; k++)
			{
				x[k] *= num4;
			}
		}
		return x;
	}
}
