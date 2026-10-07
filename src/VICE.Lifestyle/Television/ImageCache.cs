using System.Buffers.Binary;
using System.Security.Cryptography;

namespace VICE.Lifestyle.Television;

internal sealed class ImageCache
{
    internal readonly string DirectoryPath;
    internal ImageCache(string directory) { DirectoryPath = Path.GetFullPath(directory); Directory.CreateDirectory(DirectoryPath); }
    internal string PathFor(string hash)
    {
        if (!MediaRules.IsHash(hash)) throw new InvalidDataException("Invalid image identity.");
        return Path.Combine(DirectoryPath, hash + ".png");
    }
    internal bool Has(string hash) => File.Exists(PathFor(hash));
    internal byte[] Read(string hash) => File.ReadAllBytes(PathFor(hash));
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal string Put(byte[] bytes, string? expected = null)
    {
        var dimensions = ImageDimensions.Read(bytes);
        if (bytes.Length > MediaRules.MaxImageBytes || !ImageDimensions.IsPng(bytes) || dimensions.Width > 2048 || dimensions.Height > 2048)
            throw new InvalidDataException("Shared images must be normalized PNGs, at most 2048 pixels and 4 MiB.");
        var hash = Hash(bytes);
        if (expected is not null && hash != expected) throw new InvalidDataException("Image checksum mismatch.");
        var path = PathFor(hash);
        if (!File.Exists(path)) { var temporary = path + ".tmp"; File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        return hash;
    }
}

internal static class ImageDimensions
{
    internal static bool IsPng(byte[] bytes) => bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
    internal static (int Width, int Height) Read(byte[] bytes)
    {
        int width = 0, height = 0;
        if (IsPng(bytes))
        { width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4)); height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4)); }
        else if (bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216)
        {
            for (var position = 2; position + 9 < bytes.Length;)
            {
                if (bytes[position++] != 255) continue;
                while (position < bytes.Length && bytes[position] == 255) position++;
                if (position >= bytes.Length) break;
                var marker = bytes[position++];
                if (marker is 0xD8 or 0xD9 or 0x01 || marker is >= 0xD0 and <= 0xD7) continue;
                if (position + 2 > bytes.Length) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position,2));
                if (length < 2 || position + length > bytes.Length) break;
                if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC) && length >= 8)
                { height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position+3,2)); width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position+5,2)); break; }
                position += length;
            }
        }
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192 || (long)width * height > 16777216)
            throw new InvalidDataException("Choose a valid PNG/JPEG of at most 16 megapixels.");
        return (width, height);
    }
}

internal sealed class ImageTransfer
{
    internal readonly string Hash;
    internal readonly int Length;
    internal readonly double Started;
    private readonly byte[] Bytes;
    private readonly bool[] Received;
    private int Count;
    internal ImageTransfer(string hash, int length, double now)
    {
        if (!MediaRules.IsHash(hash) || length is < 24 or > MediaRules.MaxImageBytes) throw new InvalidDataException("Invalid image transfer.");
        Hash = hash; Length = length; Started = now; Bytes = new byte[length]; Received = new bool[(length + MediaRules.ChunkBytes - 1) / MediaRules.ChunkBytes];
    }
    internal byte[]? Add(int index, byte[] chunk, double now)
    {
        if (now - Started > 90 || index < 0 || index >= Received.Length || chunk.Length != Math.Min(MediaRules.ChunkBytes, Length - index * MediaRules.ChunkBytes))
            throw new InvalidDataException("Invalid or expired image chunk.");
        if (Received[index])
        { if (!Bytes.AsSpan(index * MediaRules.ChunkBytes, chunk.Length).SequenceEqual(chunk)) throw new InvalidDataException("Conflicting duplicate image chunk."); return null; }
        Buffer.BlockCopy(chunk, 0, Bytes, index * MediaRules.ChunkBytes, chunk.Length); Received[index] = true;
        if (++Count != Received.Length) return null;
        if (ImageCache.Hash(Bytes) != Hash) throw new InvalidDataException("Image checksum mismatch.");
        return Bytes;
    }
}
