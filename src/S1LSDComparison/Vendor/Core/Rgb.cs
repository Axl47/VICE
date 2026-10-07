// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;

namespace S1LSD.Core;

public readonly struct Rgb
{
	public readonly float R;

	public readonly float G;

	public readonly float B;

	public static readonly Rgb White = new Rgb(1f, 1f, 1f);

	public static readonly Rgb Black = new Rgb(0f, 0f, 0f);

	public Rgb(float r, float g, float b)
	{
		R = r;
		G = g;
		B = b;
	}

	public static Rgb Lerp(Rgb a, Rgb b, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
	}
}
