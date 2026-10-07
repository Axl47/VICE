// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;

namespace S1LSD.Core;

public sealed class ValueNoise
{
	private readonly int[] _perm;

	public ValueNoise(int seed)
	{
		XorShift32 xorShift = new XorShift32((seed == 0) ? 2654435769u : ((uint)seed));
		int[] array = new int[256];
		for (int i = 0; i < 256; i++)
		{
			array[i] = i;
		}
		for (int num = 255; num > 0; num--)
		{
			int num2 = (int)(xorShift.NextUInt() % (uint)(num + 1));
			ref int reference = ref array[num];
			ref int reference2 = ref array[num2];
			int num3 = array[num2];
			int num4 = array[num];
			reference = num3;
			reference2 = num4;
		}
		_perm = new int[512];
		for (int j = 0; j < 512; j++)
		{
			_perm[j] = array[j & 0xFF];
		}
	}

	public float Noise(float x)
	{
		int num = (int)Math.Floor(x);
		float num2 = x - (float)num;
		int num3 = num & 0xFF;
		int num4 = (num + 1) & 0xFF;
		float t = Fade(num2);
		float a = Grad(_perm[num3], num2);
		float b = Grad(_perm[num4], num2 - 1f);
		return Lerp(a, b, t);
	}

	public float Fbm(float x, int octaves, float lacunarity = 2f, float gain = 0.5f)
	{
		if (octaves < 1)
		{
			octaves = 1;
		}
		float num = 0f;
		float num2 = 1f;
		float num3 = 0f;
		float num4 = 1f;
		for (int i = 0; i < octaves; i++)
		{
			num += Noise(x * num4) * num2;
			num3 += num2;
			num2 *= gain;
			num4 *= lacunarity;
		}
		if (!(num3 > 0f))
		{
			return 0f;
		}
		return num / num3;
	}

	private static float Fade(float t)
	{
		return t * t * t * (t * (t * 6f - 15f) + 10f);
	}

	private static float Grad(int hash, float x)
	{
		int num = hash & 7;
		float num2 = ((num < 4) ? x : (0f - x));
		if ((num & 1) != 0)
		{
			return 0f - num2;
		}
		return num2;
	}

	private static float Lerp(float a, float b, float t)
	{
		return a + t * (b - a);
	}
}
