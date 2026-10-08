using Il2CppFishNet;
using Il2CppFishNet.Connection;

namespace VICE.Lifestyle.Television;

internal static class TVVideoNetwork
{
    private sealed record Sending(NetworkConnection? Peer, VideoSender File, string TransferId);
    private sealed record Incoming(int Owner, VideoTransfer File, string TransferId);
    private sealed class Buffering
    {
        internal readonly double Started = PlaybackClock.LocalNow;
        internal readonly HashSet<int> Ready = new();
    }
    private static readonly Dictionary<string, string> LocalFiles = new();
    private static readonly Dictionary<string, int> Owners = new();
    private static readonly Dictionary<string, Incoming> Downloads = new();
    private static readonly List<Sending> Senders = new();
    private static readonly Dictionary<string, Buffering> Pending = new();
    private static readonly Dictionary<string, double> Requests = new();
    private static readonly Dictionary<string, string> SentTransfers = new();
    private static double NextChunk;
    private static int Cursor;
    private static TelevisionFeature Mod => TelevisionFeature.Current!;
    private static MediaSelection? Active(string hash) => Mod.Document.Screens.FirstOrDefault(s => s.Media.Kind == "video" && s.Media.Source == hash && s.VideoRequested)?.Media;
    private static ISet<string> Pinned() => Mod.Document.Screens.Where(s => s.Media.Kind == "video").Select(s => s.Media.Source).ToHashSet();
    internal static void RegisterLocal(MediaSelection media) => LocalFiles[media.Source] = media.VideoName;
    internal static void RegisterOwner(MediaSelection media, NetworkConnection? peer) => Owners[media.Source] = peer?.ClientId ?? -1;
    internal static string? PathFor(MediaSelection media)
    {
        if (Mod.Videos.Has(media)) return Mod.Videos.PathFor(media.Source);
        if (!LocalFiles.TryGetValue(media.Source, out var name)) return null;
        try
        {
            var path = VideoCache.SourcePath(TVConfig.Videos, name);
            return new FileInfo(path).Length == media.VideoBytes ? path : null;
        }
        catch { return null; }
    }
    internal static string Progress(string hash) => Downloads.TryGetValue(hash, out var incoming)
        ? $"Receiving video · {incoming.File.Received * 100L / incoming.File.Length}%" : "Waiting for shared video";
    internal static void Play(ScreenState screen)
    {
        if (screen.VideoRequested) return;
        if (screen.Duration > 0 && screen.Target(TVNetwork.Now) >= screen.Duration) screen.Position = 0;
        screen.Reanchor(TVNetwork.Now); screen.Playing = false; screen.VideoRequested = true;
        screen.Status = "Sharing video with players"; Pending[screen.Id] = new();
    }
    internal static void Pause(ScreenState screen)
    {
        Pending.Remove(screen.Id); screen.Reanchor(TVNetwork.Now); screen.Playing = false; screen.VideoRequested = false; screen.Status = "Paused";
        if (Active(screen.Media.Source) is null) Cancel(screen.Media.Source);
    }
    internal static void Forget(string screen)
    {
        Pending.Remove(screen);
        var state = Mod.Document.Screens.FirstOrDefault(s => s.Id == screen);
        if (state?.Media.Kind == "video" && !Mod.Document.Screens.Any(s => s.Id != screen && s.VideoRequested && s.Media.Source == state.Media.Source)) Cancel(state.Media.Source);
    }
    internal static void ObserveSnapshot(ScreenState old, ScreenState current)
    {
        if (old.Media.Kind != "video") return;
        if (old.Media.Source == current.Media.Source && old.MediaGeneration != current.MediaGeneration)
        {
            if (!Mod.Document.Screens.Any(s => s.Id != current.Id && s.Media.Kind == "video" && s.VideoRequested && s.Media.Source == old.Media.Source)) Cancel(old.Media.Source);
        }
        else if (Active(old.Media.Source) is null) Cancel(old.Media.Source);
    }
    private static string SendKey(int peer, string hash) => "send:" + peer + ":" + hash;
    private static void Cancel(string hash)
    {
        if (Downloads.Remove(hash, out var incoming)) incoming.File.Dispose();
        foreach (var sending in Senders.Where(s => s.File.Hash == hash).ToArray()) { sending.File.Dispose(); Senders.Remove(sending); }
        foreach (var key in Requests.Keys.Where(k => k == hash || k == "ready:" + hash || k.StartsWith("send:", StringComparison.Ordinal) && k.EndsWith(":" + hash, StringComparison.Ordinal)).ToArray()) Requests.Remove(key);
        foreach (var key in SentTransfers.Keys.Where(k => k.EndsWith(":" + hash, StringComparison.Ordinal)).ToArray()) SentTransfers.Remove(key);
        TVNetwork.CancelVideo(hash);
    }
    internal static void Tick()
    {
        var now = PlaybackClock.LocalNow;
        foreach (var hash in Downloads.Keys.ToArray())
            if (Active(hash) is null || now - Downloads[hash].File.LastActivity > 60)
            {
                var stalled = Active(hash) is not null;
                if (stalled) Fail(hash, "Video transfer stalled. Press Play to retry.");
                else Cancel(hash);
            }
        foreach (var sending in Senders.ToArray())
            if (Active(sending.File.Hash) is null || sending.Peer is { Authenticated: false })
            { sending.File.Dispose(); Senders.Remove(sending); }
        foreach (var key in Requests.Keys.ToArray())
        {
            if (key.StartsWith("send:",StringComparison.Ordinal)) { if (now - Requests[key] > 15) Requests.Remove(key); }
            else if (Active(key.StartsWith("ready:",StringComparison.Ordinal) ? key[6..] : key) is null) Requests.Remove(key);
        }

        foreach (var media in Mod.Document.Screens.Where(s => s.VideoRequested && s.Media.Kind == "video").Select(s => s.Media).DistinctBy(m => m.Source).ToArray())
        {
            if (PathFor(media) is not null)
            {
                var readyKey = "ready:" + media.Source;
                if (!TVNetwork.Host && (!Requests.TryGetValue(readyKey, out var sent) || now - sent >= 5))
                { Requests[readyKey] = now; TVNetwork.Submit(new() { Action = "videoReady", Hash = media.Source }); }
                continue;
            }
            if (Downloads.ContainsKey(media.Source) || Requests.TryGetValue(media.Source, out var last) && now - last < 10) continue;
            Requests[media.Source] = now;
            if (!TVNetwork.Host) TVNetwork.Submit(new() { Action = "videoNeed", Hash = media.Source });
            else if (Owners.TryGetValue(media.Source, out var owner) && owner >= 0)
            {
                var peer = Peer(owner);
                if (peer is null) Fail(media.Source, "The player sharing this video disconnected. Reselect a local file.");
                else TVNetwork.Send(peer, new() { Type = "videoUpload", Hash = media.Source });
            }
            else Mod.FindSavedVideo(media);
        }
        if (TVNetwork.Host)
        {
            foreach (var id in Pending.Keys.ToArray())
            {
                var screen = Mod.Document.Screens.FirstOrDefault(s => s.Id == id);
                if (screen is null || !screen.VideoRequested) { Pending.Remove(id); continue; }
                var pending = Pending[id];
                var timeout = Math.Max(1800, screen.Media.VideoBytes / (1024.0 * 1024) * (TVNetwork.PeerCount + 1) * 2);
                if (now - pending.Started > timeout) { Fail(screen.Media.Source, "Video sharing timed out. Press Play to try again."); continue; }
                if (PathFor(screen.Media) is null) continue;
                var peers = new List<int>();
                foreach (var pair in InstanceFinder.NetworkManager.ServerManager.Clients)
                    if (pair.Value.Authenticated && !pair.Value.IsLocalClient) peers.Add(pair.Key);
                if (peers.All(p => pending.Ready.Contains(p)))
                {
                    Pending.Remove(id); screen.Anchor = TVNetwork.Now; screen.Playing = true; screen.Status = ""; Mod.VideoChanged(screen);
                }
            }
        }
        if (now < NextChunk || TVNetwork.QueuedMessages > 4 || Senders.Count == 0) return;
        // Global 1 MiB/s ceiling and a small queue keep controls responsive.
        var interval = MediaRules.ChunkBytes / (1024.0 * 1024);
        NextChunk = Math.Max(NextChunk, now - interval * 3);
        for (var burst = 0; burst < 4 && now >= NextChunk && TVNetwork.QueuedMessages <= 4 && Senders.Count > 0; burst++)
        {
            NextChunk += interval;
            Cursor %= Senders.Count; var next = Senders[Cursor]; Cursor++;
            try
            {
                var index = next.File.Index; var bytes = next.File.Next();
                if (TVNetwork.Host) TVNetwork.Send(next.Peer!, new() { Type = "videoChunk", Hash = next.File.Hash, TransferId = next.TransferId, Index = index, Data = bytes });
                else TVNetwork.Submit(new() { Action = "videoChunk", Hash = next.File.Hash, TransferId = next.TransferId, Index = index, Data = bytes });
                if (next.File.Complete) { next.File.Dispose(); Senders.Remove(next); }
            }
            catch (Exception error)
            {
                next.File.Dispose(); Senders.Remove(next); Fail(next.File.Hash, TVNetwork.SafeError(error));
            }
        }
    }
    private static NetworkConnection? Peer(int id) => InstanceFinder.NetworkManager.ServerManager.Clients.TryGetValue(id, out var peer) && peer.Authenticated ? peer : null;
    internal static bool ReceiveRequest(NetworkConnection peer, TVCommand command)
    {
        if (!command.Action.StartsWith("video", StringComparison.Ordinal) || command.Action == "videoSelect") return false;
        var media = Active(command.Hash);
        if (media is null) return true;
        if (command.Action == "videoBegin" && (!Owners.TryGetValue(command.Hash, out var owner) || owner != peer.ClientId || command.Length != media.VideoBytes))
            throw new InvalidDataException("Unexpected video upload.");
        try
        {
        switch (command.Action)
        {
            case "videoReady":
                foreach (var screen in Mod.Document.Screens.Where(s => s.Media.Source == command.Hash))
                    if (Pending.TryGetValue(screen.Id, out var pending)) pending.Ready.Add(peer.ClientId);
                break;
            case "videoNeed":
                if (PathFor(media) is { } path) SendFile(peer, media, path);
                break;
            case "videoBegin":
                Begin(peer.ClientId, media, command.TransferId);
                break;
            case "videoChunk":
                if (Downloads.TryGetValue(command.Hash, out var incoming) && incoming.Owner == peer.ClientId && incoming.TransferId == command.TransferId) Add(command.Hash, command.Index, command.Data);
                break;
            case "videoFailed":
                if (command.TransferId.Length > 0 && !(Downloads.TryGetValue(command.Hash, out var failed) && failed.Owner == peer.ClientId && failed.TransferId == command.TransferId)
                    && SentTransfers.GetValueOrDefault(SendKey(peer.ClientId, command.Hash)) != command.TransferId) break;
                Fail(command.Hash, "A player could not receive or share this video. Press Play to retry or reselect the file.");
                break;
        }
        }
        catch (Exception error) { Fail(command.Hash, TVNetwork.SafeError(error)); }
        return true;
    }
    internal static bool ReceiveMessage(TVMessage message)
    {
        if (!message.Type.StartsWith("video", StringComparison.Ordinal)) return false;
        var media = Active(message.Hash);
        if (media is null) return true;
        try
        {
        switch (message.Type)
        {
            case "videoUpload":
                if (PathFor(media) is { } path) SendFile(null, media, path);
                else TVNetwork.Submit(new() { Action = "videoFailed", Hash = media.Source });
                break;
            case "videoBegin":
                if (message.Length != media.VideoBytes) throw new InvalidDataException("Unexpected video length.");
                Begin(-1, media, message.TransferId);
                break;
            case "videoChunk":
                if (Downloads.TryGetValue(message.Hash, out var incoming) && incoming.TransferId == message.TransferId) Add(message.Hash, message.Index, message.Data);
                break;
        }
        }
        catch (Exception error) { Fail(message.Hash, TVNetwork.SafeError(error), message.TransferId); }
        return true;
    }
    private static void SendFile(NetworkConnection? peer, MediaSelection media, string path)
    {
        if (Senders.Any(s => s.Peer?.ClientId == peer?.ClientId && s.File.Hash == media.Source)) return;
        var key = SendKey(peer?.ClientId ?? -1, media.Source);
        if (Requests.TryGetValue(key, out var last) && PlaybackClock.LocalNow - last < 15) return;
        if (Senders.Count >= 8) return;
        var sender = new VideoSender(path, media); var transferId = Guid.NewGuid().ToString();
        Senders.Add(new(peer, sender, transferId)); Requests[key] = PlaybackClock.LocalNow; SentTransfers[key] = transferId;
        if (TVNetwork.Host) TVNetwork.Send(peer!, new() { Type = "videoBegin", Hash = media.Source, TransferId = transferId, Length = media.VideoBytes });
        else TVNetwork.Submit(new() { Action = "videoBegin", Hash = media.Source, TransferId = transferId, Length = media.VideoBytes });
    }
    private static void Begin(int owner, MediaSelection media, string transferId)
    {
        if (!Guid.TryParse(transferId, out _)) throw new InvalidDataException("Invalid video transfer identity.");
        if (Mod.Videos.Has(media)) { Requests.Remove("ready:" + media.Source); return; }
        if (Downloads.TryGetValue(media.Source, out var previous))
        {
            if (previous.TransferId == transferId) return;
            previous.File.Dispose(); Downloads.Remove(media.Source);
        }
        if (Downloads.Count >= 4) throw new InvalidOperationException("Too many incoming videos. Try again shortly.");
        Downloads.Add(media.Source, new(owner, Mod.Videos.Begin(media, Pinned(), PlaybackClock.LocalNow), transferId));
    }
    private static void Add(string hash, int index, byte[]? bytes)
    {
        if (!Downloads.TryGetValue(hash, out var incoming)) return;
        try
        {
            if (!incoming.File.Add(index, bytes ?? Array.Empty<byte>(), PlaybackClock.LocalNow)) return;
            Downloads.Remove(hash); Requests.Remove(hash); Requests.Remove("ready:" + hash);
        }
        catch (Exception error)
        {
            Fail(hash, TVNetwork.SafeError(error));
        }
    }
    internal static void Fail(string hash, string text, string? failedTransferId = null)
    {
        var transferId = !string.IsNullOrEmpty(failedTransferId) ? failedTransferId : Downloads.TryGetValue(hash, out var incoming) ? incoming.TransferId : SentTransfers.GetValueOrDefault(SendKey(-1, hash)) ?? "";
        Cancel(hash);
        if (TVNetwork.Host)
            foreach (var screen in Mod.Document.Screens.Where(s => s.Media.Source == hash))
            { Pause(screen); screen.Status = text; Mod.VideoChanged(screen); }
        else
        {
            Mod.Notice(text);
            TVNetwork.Submit(new() { Action = "videoFailed", Hash = hash, TransferId = transferId });
        }
    }
    internal static void Reset()
    {
        foreach (var download in Downloads.Values) download.File.Dispose();
        foreach (var sender in Senders) sender.File.Dispose();
        Downloads.Clear(); Senders.Clear(); Pending.Clear(); Owners.Clear(); LocalFiles.Clear(); Requests.Clear(); SentTransfers.Clear(); NextChunk = 0; Cursor = 0;
        TelevisionFeature.Current?.Videos?.Clear();
    }
}
