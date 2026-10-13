using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using VICE.Lifestyle.Television;
using Object = UnityEngine.Object;

namespace VICE.YouTube;

internal sealed class YouTubeProvider : ITVYouTubeProvider, IDisposable
{
    private readonly HashSet<Session> Sessions = new();
    private readonly string CompanionPath = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "BaseBuilder", "TvBrowser", "BuildABase.TvBrowser.exe");
    private double NextCheck;
    private bool Installed;
    private bool Disposed;
    public bool IsAvailable
    {
        get
        {
            if(Disposed)return false;
            var now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
            if(now>=NextCheck){NextCheck=now+1;Installed=File.Exists(CompanionPath);}
            return Installed;
        }
    }
    public string UnavailableReason => "VICE YouTube is installed. Install its Build-A-Base TV companion to enable the YouTube option.";
    public ITVYouTubeSession Create(string videoId, double now)
    {
        if(!IsAvailable)throw new InvalidOperationException(UnavailableReason);
        if(!TVYouTube.IsVideoId(videoId))throw new InvalidDataException("Invalid YouTube video identity.");
        var session=new Session(CompanionPath,videoId,now,s=>Sessions.Remove(s));Sessions.Add(session);return session;
    }
    public void Dispose(){if(Disposed)return;Disposed=true;foreach(var session in Sessions.ToArray())session.Dispose();}

    private sealed class Session : ITVYouTubeSession
    {
        private readonly YouTubeCompanion Companion;
        private readonly YouTubePlayback Playback;
        private readonly CancellationTokenSource ChaptersLifetime=new();
        private readonly Action<Session> Released;
        private readonly string VideoId;
        private Task<YouTubeChapterResult>? ChapterTask;
        private GCHandle Pixels;
        private Texture2D? BrowserTexture;
        private bool Disposed;
        public Texture? Texture=>Playback.HasMedia&&!Disposed?BrowserTexture:null;
        public bool HasMedia=>Playback.HasMedia&&!Disposed;
        public bool Failed=>Playback.Failed;
        public string Status=>Playback.Status;
        public bool CanToggleCaptions=>!Disposed&&Playback.CanToggleCaptions;
        public IReadOnlyList<YouTubeChapter> Chapters {get;private set;}=Array.Empty<YouTubeChapter>();
        public string ChapterStatus {get;private set;}="Loading chapters…";
        public long FrameSequence=>Companion.Sequence;
        public int ProcessId=>Companion.ProcessId;
        internal Session(string executable,string videoId,double now,Action<Session> released)
        {
            VideoId=videoId;Released=released;Companion=new(executable);Playback=new(Companion,now);
            try
            {
                Pixels=GCHandle.Alloc(Companion.Pixels,GCHandleType.Pinned);
                BrowserTexture=new(YouTubeRules.Width,YouTubeRules.Height,TextureFormat.BGRA32,false){filterMode=FilterMode.Bilinear};
                ChapterTask=YouTubeChapters.Fetch(videoId,ChaptersLifetime.Token);
            }
            catch{Dispose();throw;}
        }
        public void Tick(ScreenState state,double now,float gain,float distance)
        {
            if(Disposed)throw new ObjectDisposedException(nameof(Session));
            if(state.Media.Kind!="youtube"||state.Media.Source!=VideoId)throw new InvalidOperationException("YouTube selection changed.");
            Playback.Tick(state,now,YouTubeRules.Gain(gain,1,1,distance));
            if(ChapterTask?.IsCompleted==true)
            {
                var result=ChapterTask.GetAwaiter().GetResult();Chapters=result.Chapters;ChapterStatus=result.Status;ChapterTask=null;
            }
            if(!Playback.Failed&&Companion.ReadFrame())
            {
                BrowserTexture!.LoadRawTextureData(Pixels.AddrOfPinnedObject(),Companion.Pixels.Length);
                BrowserTexture.Apply(false,false);
            }
        }
        public void ToggleCaptions(){if(Disposed)throw new ObjectDisposedException(nameof(Session));Playback.ToggleCaptions();}
        public void Dispose()
        {
            if(Disposed)return;Disposed=true;
            ChaptersLifetime.Cancel();ChaptersLifetime.Dispose();ChapterTask=null;Chapters=Array.Empty<YouTubeChapter>();
            Playback.Dispose();if(Pixels.IsAllocated)Pixels.Free();
            if(BrowserTexture)Object.Destroy(BrowserTexture);BrowserTexture=null;Released(this);
        }
    }
}
