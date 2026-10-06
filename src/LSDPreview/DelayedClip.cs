namespace PersonalMods.LSDPreview;

// Cursor for a continuous delayed copy of an already-playing native clip.
internal static class DelayedClip
{
    internal const double Delay = 0.22, Lead = 0.05;

    internal static bool TryPosition(int sample, int samples, int frequency, float pitch,
        bool loop, double lead, out int position)
    {
        position = 0;
        if (sample < 0 || samples <= 0 || frequency <= 0 || sample >= samples
            || !float.IsFinite(pitch) || pitch < 0.1f || pitch > 3
            || !double.IsFinite(lead) || lead < 0 || lead > Delay) return false;
        var target = sample + (lead - Delay) * frequency * pitch;
        if (loop) target = (target % samples + samples) % samples;
        else if (target < 0 || target >= samples) return false;
        position = (int)target;
        return true;
    }

    internal static long Distance(int actual, int expected, int samples, bool loop)
    {
        var distance = Math.Abs((long)actual - expected);
        return loop && samples > 0 ? Math.Min(distance, Math.Abs(samples - distance)) : distance;
    }
}
