// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
namespace S1LSD.Core;

public struct AudioInputs
{
	public ChannelSnapshot Channels;

	public float Breath;

	public PhaseId Phase;

	public float PhaseProgress;

	public float BadTripBlend;

	public float ClosedEyes;

	public float TimeDilation;

	public float Unease;

	public float HeartBpm;

	public bool Gentle;

	public bool Active;
}
