// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
namespace S1LSD.Core;

public sealed class XorShift32
{
	private uint _state;

	public XorShift32(uint seed)
	{
		_state = ((seed == 0) ? 2738958700u : seed);
	}

	public uint NextUInt()
	{
		uint state = _state;
		state ^= state << 13;
		state ^= state >> 17;
		return _state = state ^ (state << 5);
	}

	public float NextFloat()
	{
		return (float)(NextUInt() >> 8) / 16777216f;
	}
}
