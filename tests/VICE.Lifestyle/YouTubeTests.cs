using System.Text.Json;
using VICE.Lifestyle.Television;

internal static class YouTubeTests
{
    internal static void Run(Action<bool,string> check)
    {
        const string id="M7lc1UVf-VE";
        foreach(var link in new[]{"https://www.youtube.com/watch?v="+id+"&t=20s","https://youtu.be/"+id+"?si=share","https://m.youtube.com/watch?v="+id,"https://youtube.com/shorts/"+id,"https://www.youtube.com/live/"+id,"https://www.youtube.com/embed/"+id})
            check(YouTubeRules.VideoId(link)==id,"Supported YouTube link was rejected.");
        foreach(var link in new[]{"https://youtube.com.evil.test/watch?v="+id,"https://evil.youtube.com/watch?v="+id,"https://user:secret@youtube.com/watch?v="+id,"https://youtube.com:444/watch?v="+id,"https://youtube.com/watch?v="+id+"&v="+id,"https://youtu.be/"+id+"/extra","file:///watch?v="+id,"https://youtube.com/playlist?list="+id,"https://youtube.com/watch?v=short"})
            Reject(()=>YouTubeRules.VideoId(link),check,"An unsupported or ambiguous YouTube link was accepted.");
        Reject(()=>new MediaSelection{Kind="youtube",Source="https://youtube.com/watch?v="+id}.Validate(),check,"YouTube selections accepted arbitrary navigation URLs.");
        foreach(var peers in new[]{0,2,8})Reject(()=>YouTubeRules.RequireSinglePlayer(true,peers),check,"YouTube was allowed outside a single local peer.");
        Reject(()=>YouTubeRules.RequireSinglePlayer(false,1),check,"A guest could play YouTube.");
        YouTubeRules.RequireSinglePlayer(true,1);
        check(YouTubeRules.ValidFrame(YouTubeRules.Magic,1,1280,720,5120,3686400),"Known companion frame rejected.");
        check(!YouTubeRules.ValidFrame(YouTubeRules.Magic,2,1280,720,5120,3686400)&&!YouTubeRules.ValidFrame(YouTubeRules.Magic,1,1920,720,5120,3686400),"Unsupported memory layout accepted.");
        var pixels=new byte[YouTubeRules.FrameBytes];
        pixels[0]=17;pixels[YouTubeRules.Width*4-1]=31;pixels[^1]=42;
        YouTubeRules.OrientFrame(pixels,new byte[YouTubeRules.Width*4]);
        check(pixels[YouTubeRules.FrameBytes-YouTubeRules.Width*4]==17&&pixels[^1]==31&&pixels[YouTubeRules.Width*4-1]==42,"Browser rows did not preserve horizontal orientation while flipping vertically.");
        check(YouTubeStatus.Parse("{\"type\":\"state\",\"position\":\"invalid\"}")!.Position==0,"Malformed numeric status was not handled safely.");
        check(YouTubeRules.Gain(1,1,1,22)==0&&YouTubeRules.Gain(1,1,1,2)==1&&YouTubeRules.Gain(1,1,0,2)==0,"Distance/mute gain failed.");
        check(!YouTubeStatus.Handshake("{\"type\":\"ready\",\"protocol\":2,\"width\":1280,\"height\":720,\"audio\":\"speakers\"}"),"Unsupported helper protocol accepted.");
        check(YouTubeStatus.Handshake("{\"type\":\"ready\",\"protocol\":1,\"width\":1280,\"height\":720,\"audio\":\"speakers\"}"),"Known helper handshake rejected.");
        Reject(()=>YouTubeStatus.Parse(new string('x',4097)),check,"Oversized helper status accepted.");
        var state=new ScreenState{Id=Guid.NewGuid().ToString(),Revision=1,Media=new(){Kind="youtube",Source=id},Playing=true,Position=12,Anchor=10,Remember=true};
        check(state.Target(100)==12,"Buffering YouTube advanced a synthetic clock.");
        var restored=TVDocument.Load(new TVDocument{Screens=new(){state},Favorites=new(){state.Media.Clone()}}.SaveJson(),100);
        check(restored.Screens[0].Media.Source==id&&!restored.Screens[0].Playing&&restored.Favorites[0].Source==id,"YouTube save/favorite did not remain paused with stable identity.");
        var bridge=new Bridge();using var playback=new YouTubePlayback(bridge,0);
        playback.Tick(state,1,0);
        check(bridge.Commands.Count==0,"Commands sent before the companion handshake.");
        bridge.IsReady=true;playback.Tick(state,2,.5);
        check(bridge.Commands.Count(c=>Op(c)=="load")==1&&bridge.Commands.First(c=>Op(c)=="load").GetProperty("paused").GetBoolean()==false,"Initial playback request lost its intent.");
        bridge.Events.Enqueue(Report(id,1,13,false));playback.Tick(state,3,.5);
        check(playback.HasMedia&&state.Position==13&&state.Playing,"Browser position did not control the YouTube timeline.");
        state.Playing=false;state.Revision++;
        bridge.Events.Enqueue(Report(id,1,14,false));playback.Tick(state,4,.5);
        check(!state.Playing&&state.Position==13&&bridge.Commands.Any(c=>Op(c)=="pause"),"Old browser report undid a user pause.");
        bridge.Events.Enqueue(Report(id,2,13,true));playback.Tick(state,5,.5);
        state.Position=30;state.Revision++;
        bridge.Events.Enqueue(Report(id,2,13,true));playback.Tick(state,6,.5);
        check(state.Position==30&&bridge.Commands.Any(c=>Op(c)=="seek"&&c.GetProperty("position").GetDouble()==30),"An old report undid a seek.");
        bridge.Events.Enqueue(Report(id,3,30,true));playback.Tick(state,7,0);
        var seeks=bridge.Commands.Count(c=>Op(c)=="seek");state.Volume=.2f;state.Revision++;
        playback.Tick(state,8,.2);
        check(bridge.Commands.Count(c=>Op(c)=="seek")==seeks&&bridge.Commands.Count(c=>Op(c)=="load")==1,"Volume changes restarted or sought the video.");
        bridge.Events.Enqueue(Report("aaaaaaaaaaa",3,99,false));playback.Tick(state,9,.2);
        check(state.Position==30&&!state.Playing,"Another video's telemetry replaced the selected video.");
        bridge.Error="Disconnected";playback.Tick(state,10,.2);check(playback.Failed,"Disconnected companion was not reported.");
        var waiting=new Bridge();using var timeout=new YouTubePlayback(waiting,0);timeout.Tick(state,46,0);check(timeout.Failed,"Companion startup had no deadline.");
    }
    private static string Op(JsonElement command)=>command.GetProperty("op").GetString()!;
    private static YouTubeStatus Report(string id,long revision,double position,bool paused)=>new(){Type="state",VideoId=id,Revision=revision,Position=position,Duration=120,Ready=true,Paused=paused};
    private static void Reject(Action action,Action<bool,string> check,string message)
    {
        try{action();}catch(Exception error)when(error is InvalidDataException or InvalidOperationException){check(true,message);return;}
        check(false,message);
    }
    private sealed class Bridge:IYouTubeBridge
    {
        public bool IsReady{get;set;}
        public string Error{get;set;}="";
        internal readonly List<JsonElement> Commands=new();
        internal readonly Queue<YouTubeStatus> Events=new();
        public void Send(object command)=>Commands.Add(JsonSerializer.SerializeToElement(command));
        public bool TryStatus(out YouTubeStatus status)=>Events.TryDequeue(out status!);
        public void Dispose(){}
    }
}
