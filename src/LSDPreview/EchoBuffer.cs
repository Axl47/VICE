namespace PersonalMods.LSDPreview;

// Clock and sample placement shared by the native adapter and signal tests.
// This is game output only; no microphone, recording or audio file is involved.
internal sealed class EchoClock
{
    internal const int PacketFrames = 512, HistoryFrames = 8192;
    private double LastTime = double.NaN, Pending;
    internal int PendingFrames => (int)Pending;

    internal int Take(double dspTime, int rate)
    {
        if (!double.IsFinite(dspTime) || rate <= 0) { Reset(); return 0; }
        if (!double.IsFinite(LastTime)) { LastTime = dspTime; return 0; }
        var frames = (dspTime - LastTime) * rate;
        LastTime = dspTime;
        if (frames < 0 || frames + Pending > HistoryFrames) { Pending = 0; return -1; }
        Pending += frames;
        var available = (int)(Pending / PacketFrames) * PacketFrames;
        Pending -= available;
        return available;
    }

    internal void Reset() { LastTime = double.NaN; Pending = 0; }
}

internal static class EchoBuffer
{
    internal const float DelaySeconds = 0.22f;

    internal static int Destination(int playbackFrame, int delayFrames, int historyFrames, int packetStart, int loopFrames)
    {
        if (loopFrames <= 0) throw new ArgumentOutOfRangeException(nameof(loopFrames));
        var position = ((long)playbackFrame + delayFrames - historyFrames + packetStart) % loopFrames;
        return (int)(position < 0 ? position + loopFrames : position);
    }

    internal static void CopyStereo(Func<int, float> left, Func<int, float> right, Action<int, float> write,
        int inputStart, int frames)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            write(frame * 2, SafeSample(left(inputStart + frame)));
            write(frame * 2 + 1, SafeSample(right(inputStart + frame)));
        }
    }

    private static float SafeSample(float value) => float.IsFinite(value) ? Math.Clamp(value, -1, 1) : 0;
}
