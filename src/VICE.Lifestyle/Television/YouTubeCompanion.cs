using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace VICE.Lifestyle.Television;

// Independently implemented client for the installed Build-A-Base TV companion.
// Chromium and the author's binaries remain external dependencies.
internal sealed class YouTubeCompanion : IYouTubeBridge
{
    private readonly string Session = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource Lifetime = new();
    private readonly Channel<string> Commands = Channel.CreateBounded<string>(64);
    private readonly ConcurrentQueue<YouTubeStatus> Events = new();
    private readonly Process Child;
    private readonly Task Connection;
    private NamedPipeClientStream? Pipe;
    private MemoryMappedFile? FrameFile;
    private MemoryMappedViewAccessor? Frame;
    private Mutex? FrameLock;
    private volatile bool Disposed, Ready;
    private volatile string Failure = "";
    internal readonly byte[] Pixels = new byte[YouTubeRules.FrameBytes];
    private readonly byte[] Row = new byte[YouTubeRules.Width * 4];
    internal long Sequence { get; private set; }
    public bool IsReady => Ready;
    public string Error => Failure;
    internal int ProcessId => Child.Id;

    internal YouTubeCompanion(string executable)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("YouTube requires the Windows TV companion.");
        if (!File.Exists(executable)) throw new FileNotFoundException("Install the Build-A-Base TV companion before playing YouTube.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(executable)!, RedirectStandardError = true
        };
        start.ArgumentList.Add("--session"); start.ArgumentList.Add(Session);
        start.ArgumentList.Add("--parent-pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
        Child = Process.Start(start) ?? throw new IOException("The YouTube companion could not start.");
        // Drain stderr without retaining browser URLs or profile details in the game log.
        Child.ErrorDataReceived += (_, _) => { };
        Child.BeginErrorReadLine();
        Connection = Connect();
    }
    private async Task Connect()
    {
        Task? writing = null;
        try
        {
            Pipe = new NamedPipeClientStream(".", "bab_tv_" + Session, PipeDirection.InOut, PipeOptions.Asynchronous);
            await Pipe.ConnectAsync(15000, Lifetime.Token).ConfigureAwait(false);
            using var reader = new StreamReader(Pipe, new UTF8Encoding(false), false, 4096, true);
            using var writer = new StreamWriter(Pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
            var first = await ReadLine(reader, Lifetime.Token).WaitAsync(TimeSpan.FromSeconds(30), Lifetime.Token).ConfigureAwait(false);
            if (first is null || !YouTubeStatus.Handshake(first)) throw new InvalidDataException("Unsupported TV companion protocol. Use Build-A-Base 0.4.26.");
            Ready = true;
            writing = WriteCommands(writer);
            while (!Lifetime.IsCancellationRequested)
            {
                var line = await ReadLine(reader, Lifetime.Token).ConfigureAwait(false);
                if (line is null) throw new IOException("The TV companion disconnected. Press Retry.");
                var status = YouTubeStatus.Parse(line);
                if (status is not null)
                {
                    Events.Enqueue(status);
                    while (Events.Count > 64) Events.TryDequeue(out _);
                }
            }
        }
        catch (OperationCanceledException) when (Disposed || Lifetime.IsCancellationRequested) { }
        catch (Exception) { if (!Disposed) Failure = "YouTube companion connection failed. Check its installation and press Retry."; }
        finally
        {
            Ready = false;
            Lifetime.Cancel();
            if (writing is not null) { try { await writing.ConfigureAwait(false); } catch (Exception) { } }
            Pipe?.Dispose();
            // Disconnect/failure must not leave a browser playing through the speakers.
            KillChild();
        }
    }
    private async Task WriteCommands(StreamWriter writer)
    {
        await foreach (var line in Commands.Reader.ReadAllAsync(Lifetime.Token).ConfigureAwait(false))
            await writer.WriteLineAsync(line).WaitAsync(Lifetime.Token).ConfigureAwait(false);
    }
    private static async Task<string?> ReadLine(StreamReader reader, CancellationToken token)
    {
        var line = new StringBuilder();
        var character = new char[1];
        while (await reader.ReadAsync(character, 0, 1).WaitAsync(token).ConfigureAwait(false) != 0)
        {
            if (character[0] == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= 4096) throw new InvalidDataException("Oversized companion message.");
            line.Append(character[0]);
        }
        return null;
    }
    public void Send(object command)
    {
        var json = JsonSerializer.Serialize(command);
        if (json.Length > 4096) throw new InvalidDataException("Oversized YouTube command.");
        if (!Disposed && !Commands.Writer.TryWrite(json)) Failure = "YouTube controls are busy. Press Retry.";
    }
    public bool TryStatus(out YouTubeStatus status) => Events.TryDequeue(out status!);
    internal bool ReadFrame()
    {
        if (!OperatingSystem.IsWindows() || !Ready || Disposed) return false;
        if (Frame is null)
        {
            try
            {
                FrameFile = MemoryMappedFile.OpenExisting("Local\\bab_tv_" + Session + "_frame", MemoryMappedFileRights.Read);
                Frame = FrameFile.CreateViewAccessor(0, YouTubeRules.HeaderBytes + YouTubeRules.FrameBytes, MemoryMappedFileAccess.Read);
                FrameLock = Mutex.OpenExisting("Local\\bab_tv_" + Session + "_frame_lock");
            }
            catch (FileNotFoundException) { CloseFrame(); return false; }
            catch (WaitHandleCannotBeOpenedException) { CloseFrame(); return false; }
        }
        var locked = false;
        try
        {
            try { locked = FrameLock!.WaitOne(0); } catch (AbandonedMutexException) { locked = true; }
            if (!locked) return false;
            if (!YouTubeRules.ValidFrame(Frame!.ReadInt32(0), Frame.ReadInt32(4), Frame.ReadInt32(8), Frame.ReadInt32(12), Frame.ReadInt32(16), Frame.ReadInt32(20)))
                throw new InvalidDataException("Unsupported companion frame.");
            var sequence = Frame.ReadInt64(24);
            if (sequence <= Sequence) return false;
            Frame.ReadArray(YouTubeRules.HeaderBytes, Pixels, 0, Pixels.Length);
            YouTubeRules.OrientFrame(Pixels, Row);
            Sequence = sequence; return true;
        }
        finally { if (locked) FrameLock!.ReleaseMutex(); }
    }
    private void CloseFrame()
    {
        Frame?.Dispose(); FrameFile?.Dispose(); FrameLock?.Dispose();
        Frame = null; FrameFile = null; FrameLock = null;
    }
    private void KillChild()
    {
        try { if (!Child.HasExited) Child.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
    }
    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true; Ready = false; Commands.Writer.TryComplete(); Lifetime.Cancel();
        KillChild(); CloseFrame();
        // Cancellation/source/process are released only after the worker finishes using them.
        _ = Connection.ContinueWith(_ => { Child.Dispose(); Lifetime.Dispose(); }, TaskScheduler.Default);
    }
}
