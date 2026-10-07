// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using System.Linq;

namespace S1LSD.Core;

public readonly struct ChannelSnapshot
{
	private readonly float[] _values;

	public float this[ChannelId id] => _values[(int)id];

	public float Warp => this[ChannelId.Warp];

	public float Trails => this[ChannelId.Trails];

	public float Halo => this[ChannelId.Halo];

	public float Chroma => this[ChannelId.Chroma];

	public float ColorDrift => this[ChannelId.ColorDrift];

	public float LocalContrast => this[ChannelId.LocalContrast];

	public float Afterimage => this[ChannelId.Afterimage];

	public float Patterns => this[ChannelId.Patterns];

	public float ReverbWet => this[ChannelId.ReverbWet];

	public float Echo => this[ChannelId.Echo];

	public float PitchDrift => this[ChannelId.PitchDrift];

	public float LowFreqPulse => this[ChannelId.LowFreqPulse];

	public float Tunnel => this[ChannelId.Tunnel];

	public float TextureCrawl => this[ChannelId.TextureCrawl];

	public float Pupil => this[ChannelId.Pupil];

	public float Synesthesia => this[ChannelId.Synesthesia];

	public float ClosedEyes => this[ChannelId.ClosedEyes];

	public ChannelSnapshot(float[] values)
	{
		_values = values;
	}

	public static ChannelSnapshot Zero()
	{
		return new ChannelSnapshot(new float[17]);
	}

	public float[] ToArray()
	{
		float[] array = new float[17];
		Array.Copy(_values, array, array.Length);
		return array;
	}

	public string FormatLine()
	{
		float[] values = _values;
		return string.Join(" ", ChannelCount.All.Select((ChannelId id) => $"{id}={values[(int)id]:F3}"));
	}
}
