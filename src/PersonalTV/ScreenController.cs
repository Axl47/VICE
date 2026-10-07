using Il2CppInterop.Runtime;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.Core.Audio;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;
using UnityEngine.Video;
using Object=UnityEngine.Object;

namespace PersonalMods.TV;

internal sealed class ScreenController : IDisposable
{
    internal readonly BuildableItem Item;
    internal string Id=>Item.GUID.ToString();
    internal string Status {get;private set;}="Screen off";
    internal Texture? Texture=>Image ? Image : VideoTexture;
    internal bool Seekable=>Player&&NativeVideoState.CanSeek(Player!);
    internal double CheckPosition=>Player?Player!.time:0;
    internal bool CheckPlaying=>Player&&NativeVideoState.IsPlaying(Player!);
    private readonly Material Material;
    private readonly Transform Face;
    private GameObject? Runtime;
    private VideoPlayer? Player;
    private AudioSource? Audio;
    private RenderTexture? VideoTexture;
    private Texture2D? Image;
    private VideoPlayer.ErrorEventHandler? ErrorHandler;
    private string Key="", FailedKey="";
    private bool Prepared;
    private double StartedAt, NextCorrection;
    internal ScreenController(BuildableItem item)
    {
        Item=item;
        var face=item.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="TV display");
        Material=new(face.sharedMaterial);face.sharedMaterial=Material;Face=face.transform;
    }
    internal void Tick()
    {
        var mod=PersonalTVMod.Current!;
        var state=mod.Document.Screens.FirstOrDefault(s=>s.Id==Id);
        if(state is null)return;
        var selected=state.Media;
        var key=selected.Kind+":"+selected.Source+":"+state.PlaybackUrl+":"+state.MediaGeneration;
        var local=PlayerScriptsLocal();
        var distance=local ? Vector3.Distance(local!.transform.position,Item.transform.position) : float.MaxValue;
        var range=Math.Clamp(TVConfig.Distance.Value,10,100);
        if(!Item||Item.IsDestroyed||Item.IsCulled||distance>range)
        {Release();Key="";Status="Playback suspended while distant";return;}
        if(key!=Key){Release();Key=key;FailedKey="";}
        if(selected.Kind=="none"){Status="Screen off";return;}
        if(selected.Kind=="image")
        {
            if(!Image)
            {
                if(!mod.Cache.Has(selected.Source)){Status="Receiving image";TVNetwork.NeedImage(selected.Source);return;}
                Image=TVArtwork.Load(mod.Cache.Read(selected.Source));Bind(Image);
            }
            Status=selected.Title;return;
        }
        if(selected.Kind=="relay"&&state.PlaybackUrl.Length==0){Status=state.Status.Length>0?state.Status:"Select Play to resolve this Relay episode";return;}
        if(FailedKey==key)return;
        if(!Player)
        {
            var url=selected.Kind switch
            {"local"=>new Uri(Path.Combine(TVConfig.Videos,MediaRules.FileName(selected.Source,".mp4"))).AbsoluteUri,"relay"=>state.PlaybackUrl,_=>MediaRules.VideoUrl(selected.Source).AbsoluteUri};
            if(selected.Kind=="local"&&TVNetwork.PeerCount>1){Status="Local videos are available only in single-player";return;}
            Prepare(url);return;
        }
        if(!Prepared)
        {
            if(!NativeVideoState.IsPrepared(Player!))
            {if(TVNetwork.Now-StartedAt>45)Fail("Video preparation timed out. Check the URL or codec.");return;}
            var scale=Math.Min(1.0,Math.Min(4096,SystemInfo.maxTextureSize)/(double)Math.Max(1,Math.Max(Player!.width,Player.height)));
            var width=Math.Max(1,(int)(Player.width*scale));var height=Math.Max(1,(int)(Player.height*scale));
            VideoTexture=new(width,height,0,RenderTextureFormat.ARGB32);
            VideoTexture.filterMode=FilterMode.Bilinear;if(!VideoTexture.Create()){Fail("Cannot allocate the video display.");return;}
            Player.targetTexture=VideoTexture;Bind(VideoTexture);Prepared=true;
            if(TVNetwork.Host&&double.IsFinite(Player.length)&&Player.length>0)mod.SetDuration(Id,Player.length);
            NextCorrection=0;
        }
        var target=state.Target(TVNetwork.Now);
        if(TVNetwork.Now>=NextCorrection)
        {
            var threshold=state.Playing?0.8:0.10;
            if(Math.Abs(Player!.time-target)>threshold&&NativeVideoState.CanSeek(Player))Player.time=target;
            NextCorrection=TVNetwork.Now+1;
        }
        if(state.Playing&&(state.Duration<=0||target<state.Duration-.05)&&!NativeVideoState.IsPlaying(Player!))Player!.Play();
        else if(!state.Playing&&NativeVideoState.IsPlaying(Player!))Player!.Pause();
        var manager=AudioManager.Instance;
        Audio!.volume=state.Volume*Math.Clamp(TVConfig.PersonalVolume.Value,0,1)*(manager?manager.GetVolume(EAudioType.FX,true):1);
        if(manager&&manager.MainGameMixer)Audio.outputAudioMixerGroup=manager.MainGameMixer;
        Status=state.Playing?"Playing":"Paused";
    }
    private static Player? PlayerScriptsLocal()=>Il2CppScheduleOne.PlayerScripts.Player.Local;
    private void Prepare(string url)
    {
        Runtime=new("Personal TV playback");Runtime.transform.SetParent(Item.transform,false);
        Audio=Runtime.AddComponent<AudioSource>();Audio.playOnAwake=false;Audio.spatialBlend=1;Audio.minDistance=2;Audio.maxDistance=22;
        Audio.rolloffMode=AudioRolloffMode.Linear;Audio.dopplerLevel=0;
        Player=Runtime.AddComponent<VideoPlayer>();Player.playOnAwake=false;Player.source=VideoSource.Url;Player.url=url;
        Player.renderMode=VideoRenderMode.RenderTexture;Player.aspectRatio=VideoAspectRatio.FitInside;Player.isLooping=false;
        Player.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;Player.audioOutputMode=VideoAudioOutputMode.AudioSource;
        Player.controlledAudioTrackCount=1;Player.EnableAudioTrack(0,true);Player.SetTargetAudioSource(0,Audio);
        ErrorHandler=DelegateSupport.ConvertDelegate<VideoPlayer.ErrorEventHandler>((Action<VideoPlayer,string>)((_,_)=>Fail("Video could not be played. Check the source or renew Relay playback.")));
        Player.errorReceived+=ErrorHandler;StartedAt=TVNetwork.Now;Status="Preparing video";Player.Prepare();
    }
    internal void Retry(){FailedKey="";Key="";}
    private void Fail(string text){Status=text;FailedKey=Key;Release();PersonalTVMod.Current?.Notice(text);}
    private void Bind(Texture texture)
    {
        Material.SetTexture("_BaseMap",texture);Material.mainTexture=texture;
        var ratio=texture.width/(float)Math.Max(1,texture.height);
        if(Face)Face.localScale=ratio>2.4f/1.35f?new(2.4f,2.4f/ratio,1):new(1.35f*ratio,1.35f,1);
    }
    private void Release()
    {
        if(Player){if(ErrorHandler is not null)Player!.errorReceived-=ErrorHandler;Player!.Stop();}
        if(Audio)Audio!.Stop();if(Runtime)Object.Destroy(Runtime);Runtime=null;Player=null;Audio=null;ErrorHandler=null;Prepared=false;
        if(VideoTexture){VideoTexture!.Release();Object.Destroy(VideoTexture);}VideoTexture=null;
        if(Image)Object.Destroy(Image);Image=null;
        if(Material)Bind(Texture2D.blackTexture);
    }
    public void Dispose(){Release();if(Material)Object.Destroy(Material);}
}
