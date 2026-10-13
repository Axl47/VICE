namespace VICE.YouTube;

internal static class YouTubeRules
{
    internal const int Width = 1280, Height = 720, FrameBytes = Width * Height * 4;
    internal const int HeaderBytes = 64, Magic = 1111577686, Protocol = 1;
    internal static bool ValidFrame(int magic, int protocol, int width, int height, int stride, int bytes)
        => magic == Magic && protocol == Protocol && width == Width && height == Height && stride == Width * 4 && bytes == FrameBytes;
    internal static void OrientFrame(Span<byte> pixels, Span<byte> row)
    {
        const int stride = Width * 4;
        for (var y = 0; y < Height / 2; y++)
        {
            var top = pixels.Slice(y * stride, stride);
            var bottom = pixels.Slice((Height - 1 - y) * stride, stride);
            top.CopyTo(row); bottom.CopyTo(top); row.CopyTo(bottom);
        }
    }
    internal static double Gain(float volume, float personal, float game, float distance)
        => Math.Clamp(volume, 0, 1) * Math.Clamp(personal, 0, 1) * Math.Clamp(game, 0, 1)
            * Math.Clamp((22.0 - distance) / 20.0, 0, 1);
}
