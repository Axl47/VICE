using System.Net;
using System.Text;
using System.Text.Json;
using VICE.Lifestyle.Television;

var checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
void Reject(Action action,string message){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception(message);}
var image=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aMs4AAAAASUVORK5CYII=");
var hash=ImageCache.Hash(image);
var id=Guid.NewGuid().ToString();
var state=new ScreenState{Id=id,Media=new(){Kind="relay",Source="episode-7",Title="Episode 7"},Remember=true,Playing=true,Position=12,Anchor=100,Duration=80,PlaybackId="session",PlaybackUrl="https://media.example/grant",ExpiresAt=DateTimeOffset.UtcNow.AddHours(1)};
Check(state.Target(110)==22,"Host timeline did not advance.");Check(state.Target(500)==80,"Timeline escaped duration.");
state.Reanchor(110);state.Playing=false;Check(state.Target(500)==22,"Paused timeline moved.");
var document=new TVDocument{Screens=new(){state,new(){Id=Guid.NewGuid().ToString(),Media=new(){Kind="image",Source=hash}}},Favorites=new(){state.Media.Clone()}};
var saved=document.SaveJson();var restored=TVDocument.Load(saved,900);
Check(!saved.Contains("https://media.example/grant")&&!saved.Contains("session"),"Playback grant leaked into save.");
Check(restored.Screens[0].Media.Source=="episode-7"&&!restored.Screens[0].Playing&&restored.Screens[0].Position==0,"Remembered episode was lost or auto-played.");
Check(restored.Screens[1].Media.Source==hash,"Image was not remembered automatically.");
state.Remember=false;Check(state.ForSave().Media.Kind=="none","Ephemeral media survived save.");
Check(document.Favorites[0].Source=="episode-7","Favorite identity was lost.");
Reject(()=>TVDocument.Load(saved.Replace("\"Version\": 1","\"Version\": 2"),0),"Unsupported save version accepted.");
var duplicate=new TVDocument{Screens=new(){state,state.Clone()}};Reject(duplicate.Validate,"Duplicate screen IDs accepted.");
Reject(()=>MediaRules.FileName("../video.mp4",".mp4"),"Traversal accepted.");Reject(()=>MediaRules.FileName("C:\\secret.mp4",".mp4"),"Absolute file accepted.");
Reject(()=>MediaRules.FileName("x.mp4:stream",".mp4"),"Alternate data stream accepted.");Reject(()=>MediaRules.VideoUrl("file:///C:/secret.mp4"),"File URL accepted.");
Reject(()=>MediaRules.VideoUrl("https://user:password@example.org/a.mp4"),"Embedded credentials accepted.");
Check(MediaRules.FileName("Moon Landing.MP4",".mp4")=="Moon Landing.MP4","Valid local filename rejected.");
var taskRoot=Path.Combine(Path.GetTempPath(),"PersonalTV-tests-"+Guid.NewGuid());
try
{
    var cache=new ImageCache(taskRoot);Check(cache.Put(image)==hash&&cache.Read(hash).SequenceEqual(image),"Cache round-trip failed.");
    Reject(()=>cache.PathFor("../escape"),"Hash traversal accepted.");Reject(()=>cache.Put(image,new string('a',64)),"Cache accepted mismatched content.");
    var payload=new byte[MediaRules.ChunkBytes*2+31];new Random(13).NextBytes(payload);var transfer=new ImageTransfer(ImageCache.Hash(payload),payload.Length,0);
    Check(transfer.Add(2,payload[(MediaRules.ChunkBytes*2)..],1) is null,"Transfer completed early.");
    var first=payload[..MediaRules.ChunkBytes];Check(transfer.Add(0,first,2) is null,"Transfer completed early.");
    Check(transfer.Add(0,first,3) is null,"Identical retransmission was not ignored.");
    Check(transfer.Add(1,payload[MediaRules.ChunkBytes..(MediaRules.ChunkBytes*2)],4)!.SequenceEqual(payload),"Out-of-order transfer corrupted content.");
    var invalid=new ImageTransfer(hash,image.Length,0);Reject(()=>invalid.Add(0,image,91),"Expired chunk accepted.");
    Reject(()=>new ImageTransfer(hash,MediaRules.MaxImageBytes+1,0),"Oversized image allocation accepted.");
    var tampered=(byte[])image.Clone();tampered[^1]^=1;Reject(()=>new ImageTransfer(hash,image.Length,0).Add(0,tampered,1),"Corrupt transfer accepted.");
    VideoProtocolTests.Run(Check,Reject,Path.Combine(taskRoot,"videos"));
}
finally{if(Directory.Exists(taskRoot))Directory.Delete(taskRoot,true);}
var clock=new PlaybackClock();clock.Observe(500,100,102);Check(Math.Abs((clock.Now-PlaybackClock.LocalNow)-399)<.01,"Clock did not use ping midpoint.");
clock.Observe(800,100,109);Check(Math.Abs((clock.Now-PlaybackClock.LocalNow)-399)<.01,"Higher-latency clock sample displaced better sample.");
Reject(()=>new RelayClient("http://public.example","token"),"Cleartext remote Relay accepted.");
var handler=new FakeRelay();using(var relay=new RelayClient("http://127.0.0.1:4012","private-test-token",handler))
{
    var cap=JsonSerializer.Deserialize<RelayCapabilities>(await relay.Query("capabilities","",0,default),RelayClient.Json)!;
    Check(cap.ProtocolVersion==1&&cap.Profiles.Contains("mp4-h264-aac"),"Capabilities contract failed.");
    await relay.Query("search","My Show & Name",0,default);Check(handler.Last!.RequestUri!.Query.Contains("My%20Show%20%26%20Name"),"Search query was not escaped.");
    var descriptor=await relay.Create(new(){Kind="relay",Source="episode-7",SourceId="provider",VersionId="v1"},"request-1",default);descriptor.Validate("episode-7");
    Check(descriptor.Status=="preparing","Preparing descriptor rejected.");
    using(var body=JsonDocument.Parse(handler.Body!))Check(body.RootElement.GetProperty("profile").GetString()=="mp4-h264-aac"&&body.RootElement.GetProperty("subtitleMode").GetString()=="burned","Playback request profile mismatch.");
    descriptor=await relay.Get(descriptor.Id,default);descriptor.Validate("episode-7");Check(descriptor.MediaUrl=="https://media.example/play/grant","Ready descriptor mismatch.");
    var ready=descriptor;ready.EpisodeId="other";Reject(()=>ready.Validate("episode-7"),"Different episode identity accepted.");ready.EpisodeId="episode-7";
    ready.Seekable=false;Reject(()=>ready.Validate("episode-7"),"Unseekable ready stream accepted.");ready.Seekable=true;
    ready.SubtitleMode="none";Reject(()=>ready.ValidateSelection(new(){Kind="relay",Source="episode-7",SubtitleMode="burned"}),"Relay silently dropped subtitles.");ready.SubtitleMode="burned";
    await relay.Refresh("session-1",52.5,default);Check(handler.Body!.Contains("52.5"),"Refresh omitted playback position.");
    Check(await relay.Stop("session-1",default),"204 stop failed.");
    Check(handler.AllAuthenticated,"Control request lost scoped Bearer authentication.");
    handler.Fail=true;
    try{await relay.Query("capabilities","",0,default);throw new Exception("Unauthorized request accepted.");}
    catch(InvalidOperationException error){Check(!error.Message.Contains("private-test-token")&&!error.Message.Contains("secret-body"),"Failure leaked credentials.");}
}
// These examples come from merged Relay PR #9, pinned to d5089752, not our mock API.
using(var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","relay-game-v1.json"))))
{
    var root=fixture.RootElement;
    var episode=root.GetProperty("episodes").GetProperty("items")[0].GetProperty("id").GetString()!;
    var selection=new MediaSelection{Kind="relay",Source=episode,SourceId="example",VersionId="sub-1080p",SubtitleMode="burned"};
    foreach(var status in new[]{"preparing","ready","unsupported","failed","expired"})
    {
        var playback=root.GetProperty(status).Deserialize<RelayPlayback>(RelayClient.Json)!;
        // The public example has a fixed expiry; exercise validation with a fresh test expiry.
        if(status=="ready")playback.ExpiresAt=DateTimeOffset.UtcNow.AddHours(1);
        playback.ValidateSelection(selection);Check(playback.Status==status,"Merged Relay playback state did not decode.");
    }
    var sources=root.GetProperty("sources").Deserialize<RelayList>(RelayClient.Json)!;
    Check(sources.Items[0].Playable&&sources.Items[0].Versions[0].Id==selection.VersionId&&!sources.Items[1].Playable,"Merged source/version eligibility mismatch.");
    var movie=root.GetProperty("movieEpisodes").Deserialize<RelayList>(RelayClient.Json)!;
    Check(movie.Items[0].SeasonNumber==0&&movie.Items[0].Id=="tmdb:movie:456:movie","Movie episode identity mismatch.");
    using var fixtureHandler=new MergedRelayFixture(root);
    using var relay=new RelayClient("https://relay.example.invalid","private-test-token",fixtureHandler);
    var titles=JsonSerializer.Deserialize<RelayList>(await relay.Query("search","Sample",0,default),RelayClient.Json)!;
    Check(titles.Items[0].Id=="tmdb:tv:123","Merged catalog search mismatch.");
    var title=JsonSerializer.Deserialize<RelayTitle>(await relay.Query("title",titles.Items[0].Id,0,default),RelayClient.Json)!;
    Check(title.Seasons[0].Number==1,"Merged title/season mismatch.");
    await relay.Query("episodes",title.Id,1000,default);
    Check(fixtureHandler.LastUri!.Query.Contains("seasonNumber=1000")&&Uri.UnescapeDataString(fixtureHandler.LastUri.AbsolutePath).Contains(title.Id),"Merged catalog route/season boundary mismatch.");
    try{await relay.Query("search",new string('x',201),0,default);throw new Exception("Oversized search accepted.");}catch(InvalidDataException){checks++;}
    var creation=root.GetProperty("createPlayback");
    await relay.Create(selection,creation.GetProperty("requestId").GetString()!,default);
    using var body=JsonDocument.Parse(fixtureHandler.Body!);
    Check(body.RootElement.EnumerateObject().Count()==creation.EnumerateObject().Count()&&creation.EnumerateObject().All(p=>body.RootElement.GetProperty(p.Name).GetString()==p.Value.GetString()),"Playback creation differs from the merged API's strict request contract.");
    fixtureHandler.Conflict=true;
    try{await relay.Create(selection,Guid.NewGuid().ToString(),default);throw new Exception("409 conflict accepted.");}catch(InvalidOperationException error){Check(error.Message.Contains("selection")&&!error.Message.Contains("request_conflict"),"Relay conflict did not receive a safe, useful message.");}
}
Console.WriteLine($"PASS: {checks} TV state, image/video transfer, on-demand playback, cache, clock, path and Relay contract checks.");

sealed class MergedRelayFixture(JsonElement root) : HttpMessageHandler
{
    internal Uri? LastUri;internal string? Body;internal bool Conflict;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
    {
        LastUri=request.RequestUri;Body=request.Content is null?null:await request.Content.ReadAsStringAsync(cancellation);
        var path=Uri.UnescapeDataString(LastUri!.AbsolutePath);
        var key=Conflict?"error":path.EndsWith("/playbacks")?"preparing":path.Contains("/search")?"search":path.EndsWith("/episodes")?"episodes":"title";
        return new(Conflict?HttpStatusCode.Conflict:HttpStatusCode.OK){Content=new StringContent(root.GetProperty(key).GetRawText(),Encoding.UTF8,"application/json")};
    }
}

sealed class FakeRelay : HttpMessageHandler
{
    internal HttpRequestMessage? Last;internal string? Body;internal bool AllAuthenticated=true,Fail;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
    {
        Last=request;Body=request.Content is null?null:await request.Content.ReadAsStringAsync(cancellation);
        AllAuthenticated&=request.Headers.Authorization?.ToString()=="Bearer private-test-token";
        if(Fail)return new(HttpStatusCode.Unauthorized){Content=new StringContent("secret-body private-test-token")};
        var path=request.RequestUri!.AbsolutePath;
        if(path.EndsWith("/stop"))return new(HttpStatusCode.NoContent);
        var body=path.EndsWith("capabilities")?"{\"protocolVersion\":1,\"profiles\":[\"mp4-h264-aac\"],\"subtitleModes\":[\"burned\",\"none\"]}"
            :path.Contains("search")?"{\"items\":[]}"
            :JsonSerializer.Serialize(new RelayPlayback{Id="session-1",EpisodeId="episode-7",Status=request.Method==HttpMethod.Post&&!path.EndsWith("refresh")?"preparing":"ready",Profile="mp4-h264-aac",SubtitleMode="burned",MediaUrl="https://media.example/play/grant",MimeType="video/mp4",DurationSeconds=110,Seekable=true,ExpiresAt=DateTimeOffset.UtcNow.AddHours(1)},RelayClient.Json);
        return new(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};
    }
}
