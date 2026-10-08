using System.Security.Cryptography;

namespace VICE.Lifestyle.Television;

// Only received videos use this disposable, bounded cache. Originals stay in Videos.
internal sealed class VideoCache
{
    private readonly string Root;
    private readonly long Limit;
    private readonly Dictionary<string, int> Reservations = new();
    internal VideoCache(string root, long limit = MediaRules.VideoCacheBytes)
    {
        Root = Path.GetFullPath(root); Limit = limit; Directory.CreateDirectory(Root);
        Clear(); // Never retain a previous session's partial or downloaded files.
    }
    internal string PathFor(string hash)
    {
        if (!MediaRules.IsHash(hash)) throw new InvalidDataException("Invalid video identity.");
        return Path.Combine(Root, hash + ".mp4");
    }
    internal bool Has(MediaSelection media) => File.Exists(PathFor(media.Source)) && new FileInfo(PathFor(media.Source)).Length == media.VideoBytes;
    internal VideoTransfer Begin(MediaSelection media, ISet<string> pinned, double now)
    {
        media.Validate();
        if (Reservations.ContainsKey(media.Source)) throw new InvalidOperationException("Video transfer already started.");
        var files = Directory.EnumerateFiles(Root, "*.mp4").Select(f => new FileInfo(f)).OrderBy(f => f.LastWriteTimeUtc).ToList();
        var used = files.Sum(f => f.Length) + Reservations.Values.Sum(v => (long)v);
        foreach (var file in files.Where(f => !pinned.Contains(Path.GetFileNameWithoutExtension(f.Name))))
        {
            if (used + media.VideoBytes <= Limit) break;
            used -= file.Length; file.Delete();
        }
        if (used + media.VideoBytes > Limit) throw new InvalidOperationException("Video cache is full. Stop another video before playing this one.");
        Reservations.Add(media.Source, media.VideoBytes);
        try { return new VideoTransfer(PathFor(media.Source), media.Source, media.VideoBytes, now, () => Reservations.Remove(media.Source)); }
        catch { Reservations.Remove(media.Source); throw; }
    }
    internal void Clear()
    {
        foreach (var file in Directory.EnumerateFiles(Root))
            if (MediaRules.IsHash(Path.GetFileNameWithoutExtension(file)) && Path.GetExtension(file) is ".mp4" or ".part") File.Delete(file);
        Reservations.Clear();
    }
    internal static string SourcePath(string directory, string name)
    {
        var path = Path.Combine(directory, MediaRules.FileName(name, ".mp4"));
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Choose a regular MP4 file from the Videos folder.");
        return path;
    }
    internal static MediaSelection Describe(string directory, string name, CancellationToken cancellation)
    {
        using var file = File.OpenRead(SourcePath(directory, name));
        if (file.Length is < 12 or > MediaRules.MaxVideoBytes) throw new InvalidDataException("Shared videos must be at most 512 MiB.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024]; int read;
        while ((read = file.Read(buffer, 0, buffer.Length)) > 0) { cancellation.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
        var title = Path.GetFileNameWithoutExtension(name);
        return new() { Kind = "video", Source = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), VideoName = name, VideoBytes = (int)file.Length, Title = title[..Math.Min(160, title.Length)] };
    }
    internal static MediaSelection Select(string directory, string name, bool multiplayer, CancellationToken cancellation)
    {
        if (multiplayer) return Describe(directory, name, cancellation);
        cancellation.ThrowIfCancellationRequested();
        var path = SourcePath(directory, name);
        if (new FileInfo(path).Length < 12) throw new InvalidDataException("Choose a nonempty MP4 file.");
        var title = Path.GetFileNameWithoutExtension(name);
        // Local selections store just a filename. They have no transfer-size or
        // 32-bit byte-count restriction and never enter the downloaded cache.
        return new() { Kind = "local", Source = name, Title = title[..Math.Min(160, title.Length)] };
    }
}

// Reliable FishNet messages arrive in order. Write one chunk at a time and hash
// incrementally; never allocate a complete video or put it in the message queue.
internal sealed class VideoTransfer : IDisposable
{
    internal readonly string Hash;
    internal readonly int Length;
    internal double LastActivity { get; private set; }
    internal int Received { get; private set; }
    private readonly string Destination, Partial;
    private readonly FileStream File;
    private readonly IncrementalHash Checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly Action Released;
    private int NextIndex;
    private bool Closed;
    internal VideoTransfer(string destination, string hash, int length, double now, Action released)
    {
        if (!MediaRules.IsHash(hash) || length is < 12 or > MediaRules.MaxVideoBytes) throw new InvalidDataException("Invalid video transfer.");
        Destination = destination; Partial = Path.ChangeExtension(destination, ".part"); Hash = hash; Length = length; LastActivity = now; Released = released;
        File = new(Partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, MediaRules.ChunkBytes);
    }
    internal bool Add(int index, byte[] data, double now)
    {
        if (Closed || now - LastActivity > 60) throw new InvalidDataException("Invalid or stalled video chunk.");
        if (!Closed && index >= 0 && index < NextIndex)
        {
            var position = (long)index * MediaRules.ChunkBytes;
            if (data.Length != Math.Min(MediaRules.ChunkBytes, Length - position)) throw new InvalidDataException("Invalid repeated video chunk.");
            var savedPosition = File.Position;
            try
            {
                File.Position = position;
                var original = new byte[data.Length];
                for (var offset = 0; offset < original.Length;)
                {
                    var read = File.Read(original, offset, original.Length - offset);
                    if (read == 0) throw new InvalidDataException("Repeated video chunk is incomplete.");
                    offset += read;
                }
                if (!original.SequenceEqual(data)) throw new InvalidDataException("Video chunk changed during retransmission.");
            }
            finally { File.Position = savedPosition; }
            return false;
        }
        if (index != NextIndex || data.Length != Math.Min(MediaRules.ChunkBytes, Length - Received))
            throw new InvalidDataException("Invalid or stalled video chunk.");
        File.Write(data, 0, data.Length); Checksum.AppendData(data); Received += data.Length; NextIndex++; LastActivity = now;
        if (Received != Length) return false;
        if (Convert.ToHexString(Checksum.GetHashAndReset()).ToLowerInvariant() != Hash) throw new InvalidDataException("Video checksum mismatch. Reselect the file if it changed.");
        File.Dispose(); System.IO.File.Move(Partial, Destination, true); Dispose(); return true;
    }
    public void Dispose()
    {
        if (Closed) return; Closed = true; File.Dispose(); Checksum.Dispose();
        if (System.IO.File.Exists(Partial)) System.IO.File.Delete(Partial); Released();
    }
}

internal sealed class VideoSender : IDisposable
{
    internal readonly string Hash;
    private readonly FileStream File;
    private readonly int Length;
    internal int Index { get; private set; }
    internal bool Complete => File.Position == Length;
    internal VideoSender(string path, MediaSelection media)
    {
        media.Validate(); Hash = media.Source; Length = media.VideoBytes;
        File = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, MediaRules.ChunkBytes);
        if (File.Length != Length) { File.Dispose(); throw new InvalidDataException("Video changed. Reselect the file."); }
    }
    internal byte[] Next()
    {
        var bytes = new byte[Math.Min(MediaRules.ChunkBytes, Length - (int)File.Position)];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = File.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new InvalidDataException("Video changed during transfer.");
            offset += read;
        }
        Index++; return bytes;
    }
    public void Dispose() => File.Dispose();
}
