// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;

namespace S1LSD.Core;

public sealed class Ramp
{
	private bool _primed;

	public float Value { get; private set; }

	public void Reset(float value)
	{
		Value = value;
		_primed = true;
	}

	public float Step(float target, float deltaTime, float transitionSeconds)
	{
		if (!_primed)
		{
			Reset(target);
			return target;
		}
		float num = Math.Max(0.05f, transitionSeconds / 3f);
		Value += (target - Value) * (1f - MathF.Exp((0f - Math.Max(0f, deltaTime)) / num));
		return Value;
	}
}
