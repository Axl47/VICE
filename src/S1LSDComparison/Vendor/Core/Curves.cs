// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;

namespace S1LSD.Core;

public static class Curves
{
	public static float Lerp(float a, float b, float t)
	{
		return a + t * (b - a);
	}

	public static float Smoothstep(float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	public static float Smoothstep(float edge0, float edge1, float x)
	{
		return Smoothstep((x - edge0) / MathF.Max(1E-06f, edge1 - edge0));
	}

	public static float Smootherstep(float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return t * t * t * (t * (t * 6f - 15f) + 10f);
	}

	public static float Bell(float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return Smoothstep(t) * Smoothstep(1f - t) * 4f;
	}
}
