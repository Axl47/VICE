// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;

namespace S1LSD.Core;

public static class PhaseNames
{
	public static readonly string[] Keys = new string[6] { "onset", "comeUp", "peak", "plateau", "comeDown", "afterglow" };

	public static readonly PhaseId[] All = (PhaseId[])Enum.GetValues(typeof(PhaseId));

	public static string Key(PhaseId phase)
	{
		return Keys[(int)phase];
	}

	public static PhaseId FromKey(string key)
	{
		int num = Array.IndexOf(Keys, key);
		if (num < 0)
		{
			throw new ArgumentException("Unknown phase key '" + key + "'.", "key");
		}
		return (PhaseId)num;
	}
}
