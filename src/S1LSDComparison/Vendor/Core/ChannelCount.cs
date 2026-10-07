// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using System.Collections.Generic;

namespace S1LSD.Core;

public static class ChannelCount
{
	public const int Count = 17;

	public static readonly ChannelId[] All = (ChannelId[])Enum.GetValues(typeof(ChannelId));

	public static readonly string[] DoseKeys = new string[16]
	{
		"warp", "trails", "halo", "chroma", "colorDrift", "localContrast", "afterimage", "patterns", "reverbWet", "echo",
		"pitchDrift", "lfPulse", "textureCrawl", "pupil", "synesthesia", "closedEyes"
	};

	private static readonly Dictionary<string, ChannelId> KeyToId = new Dictionary<string, ChannelId>(StringComparer.Ordinal)
	{
		["warp"] = ChannelId.Warp,
		["trails"] = ChannelId.Trails,
		["halo"] = ChannelId.Halo,
		["chroma"] = ChannelId.Chroma,
		["colorDrift"] = ChannelId.ColorDrift,
		["localContrast"] = ChannelId.LocalContrast,
		["afterimage"] = ChannelId.Afterimage,
		["patterns"] = ChannelId.Patterns,
		["reverbWet"] = ChannelId.ReverbWet,
		["echo"] = ChannelId.Echo,
		["pitchDrift"] = ChannelId.PitchDrift,
		["lfPulse"] = ChannelId.LowFreqPulse,
		["tunnel"] = ChannelId.Tunnel,
		["textureCrawl"] = ChannelId.TextureCrawl,
		["pupil"] = ChannelId.Pupil,
		["synesthesia"] = ChannelId.Synesthesia,
		["closedEyes"] = ChannelId.ClosedEyes
	};

	public static readonly string[] RequiredDoseKeys = new string[12]
	{
		"warp", "trails", "halo", "chroma", "colorDrift", "localContrast", "afterimage", "patterns", "reverbWet", "echo",
		"pitchDrift", "lfPulse"
	};

	public static bool TryResolve(string key, out ChannelId id)
	{
		return KeyToId.TryGetValue(key, out id);
	}

	public static ChannelId Resolve(string key)
	{
		if (!KeyToId.TryGetValue(key, out var value))
		{
			throw new ArgumentException("Unknown channel key '" + key + "'.", "key");
		}
		return value;
	}
}
