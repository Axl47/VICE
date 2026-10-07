using Il2CppFishNet;
using Il2CppFishNet.Connection;
using System.Text.Json;
using VICE.Lifestyle.Television;

internal static class VideoProtocolTests
{
    internal static void Run(Action<bool,string> check, Action<Action,string> reject, string root)
    {
        var originals=Path.Combine(root,"originals");Directory.CreateDirectory(originals);
        var bytes=new byte[MediaRules.ChunkBytes*2+31];new Random(41).NextBytes(bytes);
        File.WriteAllBytes(Path.Combine(originals,"sample.mp4"),bytes);
        var media=VideoCache.Describe(originals,"sample.mp4",default);
        var selection=JsonSerializer.Serialize(new TVCommand{Action="videoSelect",Media=media});
        check(selection.Length<1024&&!selection.Contains(Convert.ToBase64String(bytes)),"Video selection contained video bytes instead of metadata.");
        var wire=JsonSerializer.Serialize(new TVCommand{Action="videoChunk",Hash=media.Source,Data=bytes[..MediaRules.ChunkBytes]});
        check(wire.Length<MediaRules.MaxWireChars&&JsonSerializer.Deserialize<TVCommand>(wire)!.Data!.SequenceEqual(bytes[..MediaRules.ChunkBytes]),"Video chunk violated existing wire size or JSON round-trip.");
        check(media.Source==ImageCache.Hash(bytes)&&media.VideoBytes==bytes.Length,"Video identity did not match selected file.");
        reject(()=>new MediaSelection{Kind="video",Source=media.Source,VideoName="../sample.mp4",VideoBytes=100}.Validate(),"Video selection allowed path traversal.");
        reject(()=>new MediaSelection{Kind="video",Source=media.Source,VideoName="sample.mp4",VideoBytes=MediaRules.MaxVideoBytes+1}.Validate(),"Oversized video selection accepted.");
        var cache=new VideoCache(Path.Combine(root,"bounded"),bytes.Length*2);
        var pinned=new HashSet<string>{media.Source};
        using(var transfer=cache.Begin(media,pinned,0))
        using(var sender=new VideoSender(Path.Combine(originals,"sample.mp4"),media))
        {
            check(!cache.Has(media),"Partial video became playable before checksum verification.");
            while(!sender.Complete){var index=sender.Index;var data=sender.Next();var finished=transfer.Add(index,data,1);check(finished==sender.Complete,"Video completion state mismatch.");}
        }
        check(cache.Has(media)&&File.ReadAllBytes(cache.PathFor(media.Source)).SequenceEqual(bytes),"Disk video transfer corrupted content.");
        var other=media.Clone();other.Source=new string('a',64);
        using(var partial=cache.Begin(other,pinned,2))
        {
            reject(()=>partial.Add(1,bytes[..MediaRules.ChunkBytes],3),"Out-of-order video chunk accepted on reliable transport.");
            reject(()=>partial.Add(0,bytes[..MediaRules.ChunkBytes],63),"Stalled video transfer accepted a chunk.");
            var third=media.Clone();third.Source=new string('b',64);
            try{using var invalid=cache.Begin(third,pinned,2);throw new Exception("Active cache reservation did not enforce storage budget.");}catch(InvalidOperationException){check(true,"Cache budget enforced.");}
        }
        check(!Directory.EnumerateFiles(Path.Combine(root,"bounded"),"*.part").Any(),"Cancelled video left a partial file.");
        using(var corrupt=cache.Begin(other,pinned,2))
        {
            corrupt.Add(0,bytes[..MediaRules.ChunkBytes],3);corrupt.Add(1,bytes[MediaRules.ChunkBytes..(MediaRules.ChunkBytes*2)],3);
            reject(()=>corrupt.Add(2,bytes[(MediaRules.ChunkBytes*2)..],3),"Corrupt video checksum accepted.");
        }
        check(!cache.Has(other),"Corrupt video entered the playable cache.");
        File.WriteAllBytes(cache.PathFor(other.Source),bytes);
        var incoming=media.Clone();incoming.Source=new string('b',64);
        using(var transfer=cache.Begin(incoming,pinned,2))check(cache.Has(media)&&!cache.Has(other),"Cache eviction removed a selected video instead of an unused one.");
        cache.Clear();check(!Directory.EnumerateFiles(Path.Combine(root,"bounded")).Any(),"Session cache cleanup failed.");
        var remembered=new ScreenState{Id=Guid.NewGuid().ToString(),Media=media.Clone(),Remember=true,VideoRequested=true,Playing=true};
        var saved=new TVDocument{Screens=new(){remembered}}.SaveJson();
        var restored=TVDocument.Load(saved,100).Screens[0];
        check(restored.Media.Source==media.Source&&!restored.VideoRequested&&!restored.Playing,"Saved video initiated a transfer or autoplay on load.");

        var mod=new TelevisionFeature{Videos=new VideoCache(Path.Combine(root,"protocol"))};TelevisionFeature.Current=mod;
        TVConfig.Videos=originals;TVNetwork.Host=true;
        var first=new NetworkConnection{ClientId=1};var second=new NetworkConnection{ClientId=2};
        InstanceFinder.NetworkManager.ServerManager.Clients.Clear();
        InstanceFinder.NetworkManager.ServerManager.Clients.Add(1,first);InstanceFinder.NetworkManager.ServerManager.Clients.Add(2,second);
        var screen=new ScreenState{Id=Guid.NewGuid().ToString(),Media=media.Clone()};mod.Document.Screens.Add(screen);
        TVVideoNetwork.RegisterLocal(media);TVVideoNetwork.RegisterOwner(media,null);
        TVVideoNetwork.Tick();check(TVNetwork.Messages.Count==0&&TVNetwork.Commands.Count==0,"Selection triggered video traffic before Play.");
        TVVideoNetwork.ReceiveRequest(first,new(){Action="videoNeed",Hash=media.Source});check(TVNetwork.Messages.Count==0,"Paused selection served video content.");
        TVVideoNetwork.Play(screen);TVVideoNetwork.Tick();check(!screen.Playing&&screen.VideoRequested,"Host started before peers were ready.");
        TVVideoNetwork.ReceiveRequest(first,new(){Action="videoReady",Hash=media.Source});TVVideoNetwork.Tick();check(!screen.Playing,"One ready peer started playback while another was missing.");
        TVVideoNetwork.ReceiveRequest(second,new(){Action="videoNeed",Hash=media.Source});
        check(TVNetwork.Messages.Any(m=>m.Peer==second&&m.Message.Type=="videoBegin"),"Play did not authorize an on-demand video transfer.");
        for(var i=0;i<4;i++){Thread.Sleep(20);TVVideoNetwork.Tick();}
        var chunks=TVNetwork.Messages.Where(m=>m.Message.Type=="videoChunk").Select(m=>m.Message.Data!).ToArray();
        check(chunks.SelectMany(b=>b).SequenceEqual(bytes),"Protocol sender omitted or corrupted chunks.");
        TVVideoNetwork.ReceiveRequest(second,new(){Action="videoReady",Hash=media.Source});TVVideoNetwork.Tick();check(screen.Playing,"All-ready barrier did not start shared playback.");
        // A late joiner requests just the active file without resetting existing viewers.
        var late=new NetworkConnection{ClientId=3};InstanceFinder.NetworkManager.ServerManager.Clients.Add(3,late);
        TVVideoNetwork.ReceiveRequest(late,new(){Action="videoNeed",Hash=media.Source});
        check(screen.Playing&&TVNetwork.Messages.Any(m=>m.Peer==late&&m.Message.Type=="videoBegin"),"Late joiner did not receive the currently playing video.");
        InstanceFinder.NetworkManager.ServerManager.Clients.Remove(3);
        var generation=screen.Anchor;TVVideoNetwork.Play(screen);check(screen.Playing&&screen.Anchor==generation,"Duplicate Play restarted shared playback.");
        TVVideoNetwork.Pause(screen);check(!screen.Playing&&!screen.VideoRequested,"Pause did not cancel transfer intent.");
        var previousMessages=TVNetwork.Messages.Count;
        TVVideoNetwork.ReceiveRequest(late,new(){Action="videoNeed",Hash=media.Source});check(TVNetwork.Messages.Count==previousMessages,"Late joiner downloaded a paused video.");
        TVVideoNetwork.Play(screen);TVVideoNetwork.ReceiveRequest(first,new(){Action="videoReady",Hash=media.Source});
        InstanceFinder.NetworkManager.ServerManager.Clients.Remove(2);TVVideoNetwork.Tick();check(screen.Playing,"Disconnected peer blocked playback permanently.");
        TVVideoNetwork.Reset();TVNetwork.Messages.Clear();TVNetwork.Commands.Clear();

        // A guest selection announces only metadata. The host requests its bytes on Play.
        screen.Playing=false;screen.VideoRequested=false;TVVideoNetwork.RegisterOwner(media,first);
        TVVideoNetwork.Tick();check(TVNetwork.Messages.Count==0,"Guest selection uploaded before Play.");
        TVVideoNetwork.Play(screen);TVVideoNetwork.Tick();
        check(TVNetwork.Messages.Any(m=>m.Message.Type=="videoUpload"&&m.Peer==first),"Host did not request the selected guest's video on Play.");
        reject(()=>TVVideoNetwork.ReceiveRequest(second,new(){Action="videoBegin",Hash=media.Source,Length=media.VideoBytes}),"Unrelated peer supplied video upload bytes.");
        TVVideoNetwork.ReceiveRequest(first,new(){Action="videoBegin",Hash=media.Source,Length=media.VideoBytes});
        for(var index=0;index*MediaRules.ChunkBytes<bytes.Length;index++)TVVideoNetwork.ReceiveRequest(first,new(){Action="videoChunk",Hash=media.Source,Index=index,Data=bytes.Skip(index*MediaRules.ChunkBytes).Take(MediaRules.ChunkBytes).ToArray()});
        check(mod.Videos.Has(media),"Host did not verify and cache the guest upload.");
        TVVideoNetwork.ReceiveRequest(first,new(){Action="videoReady",Hash=media.Source});TVVideoNetwork.Tick();check(screen.Playing,"Guest video never became playable on host.");
        TVVideoNetwork.ReceiveRequest(first,new(){Action="videoFailed",Hash=media.Source});check(!screen.Playing&&!screen.VideoRequested&&screen.Status.Length>0,"Peer transfer failure did not pause sharing with a useful status.");
        TVVideoNetwork.Reset();TVNetwork.Messages.Clear();TVNetwork.Commands.Clear();

        TVNetwork.Host=false;screen.VideoRequested=false;
        TVVideoNetwork.Tick();check(TVNetwork.Commands.Count==0,"Client requested selected video before Play.");
        screen.VideoRequested=true;TVVideoNetwork.Tick();check(TVNetwork.Commands.Any(c=>c.Action=="videoNeed"),"Client did not request video after Play.");
        TVVideoNetwork.ReceiveMessage(new(){Type="videoBegin",Hash=media.Source,Length=media.VideoBytes});
        TVVideoNetwork.ReceiveMessage(new(){Type="videoChunk",Hash=media.Source,Index=0,Data=bytes[..MediaRules.ChunkBytes]});
        screen.VideoRequested=false;TVVideoNetwork.Tick();check(!Directory.EnumerateFiles(Path.Combine(root,"protocol"),"*.part").Any(),"Pause left a client download running.");
        screen.VideoRequested=true;TVVideoNetwork.Tick();TVVideoNetwork.ReceiveMessage(new(){Type="videoBegin",Hash=media.Source,Length=media.VideoBytes});
        for(var index=0;index*MediaRules.ChunkBytes<bytes.Length;index++)TVVideoNetwork.ReceiveMessage(new(){Type="videoChunk",Hash=media.Source,Index=index,Data=bytes.Skip(index*MediaRules.ChunkBytes).Take(MediaRules.ChunkBytes).ToArray()});
        TVVideoNetwork.Tick();check(mod.Videos.Has(media)&&TVNetwork.Commands.Any(c=>c.Action=="videoReady"),"Client declared no readiness after verified download.");
        TVVideoNetwork.Reset();check(!Directory.EnumerateFiles(Path.Combine(root,"protocol")).Any(),"Protocol reset retained video downloads.");
        TVNetwork.Messages.Clear();TVNetwork.Commands.Clear();TelevisionFeature.Current=null;
    }
}

// Small transport doubles execute the production video protocol without loading Unity.
namespace Il2CppFishNet.Connection
{
    internal sealed class NetworkConnection { internal int ClientId; internal bool Authenticated=true,IsLocalClient=false; }
}
namespace Il2CppFishNet
{
    internal static class InstanceFinder { internal static readonly Manager NetworkManager=new(); }
    internal sealed class Manager { internal readonly Server ServerManager=new(); }
    internal sealed class Server { internal readonly Dictionary<int,NetworkConnection> Clients=new(); }
}
namespace VICE.Lifestyle.Television
{
    internal static class TVConfig { internal static string Videos=""; }
    internal static class TVNetwork
    {
        internal static bool Host;
        internal static double Now=>PlaybackClock.LocalNow;
        internal static int QueuedMessages=>0;
        internal static readonly List<(NetworkConnection Peer,TVMessage Message)> Messages=new();
        internal static readonly List<TVCommand> Commands=new();
        internal static void Submit(TVCommand command)=>Commands.Add(command);
        internal static void Send(NetworkConnection peer,TVMessage message)=>Messages.Add((peer,message));
        internal static string SafeError(Exception error)=>error.Message;
    }
    internal sealed class TelevisionFeature
    {
        internal static TelevisionFeature? Current;
        internal VideoCache Videos=null!;
        internal readonly TVDocument Document=new();
        internal void Notice(string text) { }
        internal void VideoChanged(ScreenState screen)=>screen.Revision++;
        internal void FindSavedVideo(MediaSelection media)=>TVVideoNetwork.Fail(media.Source,"Source missing.");
    }
}
