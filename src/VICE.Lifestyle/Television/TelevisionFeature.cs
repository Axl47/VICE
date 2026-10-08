using System.Collections.Concurrent;
using System.Text.Json;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader;
using S1API.Lifecycle;
using S1API.Building;


namespace VICE.Lifestyle.Television;

internal sealed class TelevisionFeature
{
    internal static TelevisionFeature? Current;
    internal TVDocument Document=new();
    internal TVWorld? World;
    internal TVPanel? Panel;
    internal ImageCache Cache=null!;
    internal VideoCache Videos=null!;
    internal bool Ready;
    private readonly ConcurrentQueue<Action> MainThread=new();
    private readonly Dictionary<string,CancellationTokenSource> RelayJobs=new();
    private readonly Dictionary<string,bool> ResumeIntent=new();
    private readonly Dictionary<string,Func<RelayClient>> RelayOwners=new();
    private readonly HashSet<string> VideoJobs=new();
    private int RelayQueries;
    private readonly HashSet<string> Executed=new();
    private CancellationTokenSource Lifetime=new();
    private string SavePath="", SaveTarget="", SaveSnapshot="";
    private long Revision;
    private double NextPublish;
    private bool LoadFailed;
    private TVDiagnostic? Diagnostic;
    private readonly VICE.Core.SubscriptionScope Subscriptions = new();

    public void Initialize(HarmonyLib.Harmony harmony)
    {
        Current=this;TVConfig.Initialize();Cache=new(Path.Combine(TVConfig.Root,"Cache","Images"));
        Videos=new(Path.Combine(TVConfig.Root,"Cache","Videos"));
        Diagnostic=TVDiagnostic.Create(this);
        TVWorld.Install(harmony);
        GameLifecycle.OnPreLoad+=Preload;GameLifecycle.OnLoadComplete+=Loaded;GameLifecycle.OnPreSceneChange+=ClearSession;
        GameLifecycle.OnSaveComplete+=SaveComplete;
        BuildEvents.OnBuildableItemInitialized+=RestoreFurniture;
        Subscriptions.Add(() => BuildEvents.OnBuildableItemInitialized-=RestoreFurniture);
        Subscriptions.Add(() => GameLifecycle.OnPreLoad-=Preload);
        Subscriptions.Add(() => GameLifecycle.OnLoadComplete-=Loaded);
        Subscriptions.Add(() => GameLifecycle.OnPreSceneChange-=ClearSession);
        Subscriptions.Add(() => GameLifecycle.OnSaveComplete-=SaveComplete);
        ModSupport.Patch(harmony,typeof(SaveManager),"Save",typeof(TelevisionFeature),prefix:nameof(SaveStarted),parameters:new[]{typeof(string)});
        MelonLogger.Msg("Wall Screens ready. Buy a Wall Screen at Herbert's Bleuball's Boutique. Local media folders: UserData/VICE/Lifestyle/Television/Images and Videos.");
    }
    private static void RestoreFurniture(BuildEventArgs args)
    {
        if (args.ItemId!=TVContent.ItemId || !args.GameObject) return;
        try { PersonalMods.Networking.ReplicatedFurnitureVisuals.Ensure(args.GameObject.GetComponent<Il2CppScheduleOne.EntityFramework.BuildableItem>()); }
        catch (Exception error) { ModSupport.WarnOnce("tv:furniture-visuals", "TV furniture visuals could not be restored: "+error); }
    }
    private void Preload()
    {
        ClearSession();LoadFailed=false;
        try
        {
            TVContent.Register();
            var folder=LoadManager.Instance.LoadedGameFolderPath;
            SavePath=string.IsNullOrWhiteSpace(folder)?"":Path.Combine(folder,"Modded","PersonalTV","Screens.json");
            Document=File.Exists(SavePath)?TVDocument.Load(File.ReadAllText(SavePath),TVNetwork.Now):new();
            Revision=Document.Screens.Select(s=>s.Revision).DefaultIfEmpty(0).Max();
            if(SavePath.Length>0)
            {
                var images=Path.Combine(Path.GetDirectoryName(SavePath)!,"Images");
                foreach(var hash in Document.Screens.Select(s=>s.Media).Concat(Document.Favorites).Where(m=>m.Kind=="image").Select(m=>m.Source).Distinct())
                {
                    var file=Path.Combine(images,hash+".png");
                    if(!Cache.Has(hash)&&File.Exists(file)&&new FileInfo(file).Length<=MediaRules.MaxImageBytes)Cache.Put(File.ReadAllBytes(file),hash);
                }
            }
        }
        catch(Exception error){LoadFailed=true;MelonLogger.Error("TV load failed; its saved state will not be overwritten: "+TVNetwork.SafeError(error));}
    }
    private void Loaded()
    {
        if(LoadFailed)return;
        try{TVContent.Register();TVContent.Shops();World=new();Panel=new();TVNetwork.Bind();Ready=true;World.Scan();if(!TVNetwork.Host)TVNetwork.Submit(new());MelonLogger.Msg("TV_READY screens="+Document.Screens.Count);}
        catch(Exception error){Ready=false;MelonLogger.Error("TV initialization failed: "+error.GetBaseException().Message);}
    }
    public void OnUpdate()
    {
        try
        {
            while(MainThread.TryDequeue(out var action))action();
            Diagnostic?.TryLoad();
            if(!Ready||!LoadManager.Instance||LoadManager.Instance.IsLoading||!LoadManager.Instance.IsGameLoaded)return;
            TVNetwork.Tick();World?.Tick();Panel?.Tick();
            Diagnostic?.Tick();
            if(TVNetwork.Host&&TVNetwork.Now>=NextPublish)
            {
                NextPublish=TVNetwork.Now+2;
                foreach(var state in Document.Screens.ToArray())
                {
                    if(state.Media.Kind is "local" or "youtube"&&TVNetwork.PeerCount>1){Stop(state);Notice("Single-player playback stopped because another player joined.");}
                    if(state.Media.Kind!="youtube"&&state.Playing&&state.Duration>0&&state.Target(TVNetwork.Now)>=state.Duration){state.Reanchor(TVNetwork.Now);state.Playing=false;state.VideoRequested=false;Touch(state);}
                    if(state.Media.Kind=="relay"&&state.ExpiresAt is {} expires&&expires<DateTimeOffset.UtcNow.AddMinutes(2)&&!RelayJobs.ContainsKey(state.Id))StartRelay(state,true);
                }
                TVNetwork.Publish();
            }
        }
        catch(Exception error){ModSupport.WarnOnce("tv:update:"+error.GetType().Name,"TV update failed: "+error.GetBaseException().Message);}
    }
    internal ScreenState EnsureScreen(string id)
    {
        var state=Document.Screens.FirstOrDefault(s=>s.Id==id);
        if(state is not null)return state;
        if(Document.Screens.Count>=64)throw new InvalidOperationException("This save already has 64 screens.");
        state=new(){Id=id,Anchor=TVNetwork.Now,Revision=++Revision};Document.Screens.Add(state);return state;
    }
    internal void RequireControl(Player player,string screen)
    {
        if(!Ready||!player||!player.PlayerLoaded||World is null||!World.Screens.TryGetValue(screen,out var controller)
            ||!controller.Item||!controller.Item.ParentProperty||!controller.Item.ParentProperty.IsOwned
            ||UnityEngine.Vector3.Distance(player.transform.position,controller.Item.transform.position)>5)
            throw new InvalidOperationException("Stand near a Wall Screen in an owned property to control it.");
    }
    internal void Execute(TVCommand command)
    {
        if(!Guid.TryParse(command.Token,out _))throw new InvalidDataException("Invalid TV action identity.");
        if(Executed.Contains(command.Token))return;
        var state=EnsureScreen(command.Screen);var now=TVNetwork.Now;
        switch(command.Action)
        {
            case "select":
                var media=command.Media??throw new InvalidDataException("No media was selected.");media.Validate();
                if(media.Kind=="youtube")
                {
                    YouTubeRules.RequireSinglePlayer(TVNetwork.Host,TVNetwork.PeerCount);
                    if(!File.Exists(TVConfig.YouTubeCompanionPath))throw new InvalidOperationException("Install the Build-A-Base TV companion before playing YouTube.");
                    foreach(var other in Document.Screens.Where(s=>s.Id!=state.Id&&s.Media.Kind=="youtube").ToArray()){Stop(other);Touch(other);}
                }
                if(media.Kind=="image"&&!Cache.Has(media.Source))throw new InvalidOperationException("The image is still being transferred.");
                if(media.Kind=="local"&&(TVNetwork.PeerCount>1||!File.Exists(Path.Combine(TVConfig.Videos,media.Source))||(File.GetAttributes(Path.Combine(TVConfig.Videos,media.Source))&FileAttributes.ReparsePoint)!=0))throw new InvalidOperationException("Local videos require single-player and a regular file in the designated Videos folder.");
                if(media.Kind=="relay")using(var setup=TVConfig.Relay()){}
                Stop(state);state.Media=media.Clone();state.Remember=media.Kind=="image"||command.Remember;state.Position=0;state.Anchor=now;state.Duration=0;
                state.Playing=media.Kind is "url" or "youtube";
                if(media.Kind=="relay")StartRelay(state,false);
                break;
            case "toggle": case "play": case "pause":
                if(state.Media.Kind is "none" or "image")throw new InvalidOperationException("Choose a video first.");
                if(state.Media.Kind=="youtube")YouTubeRules.RequireSinglePlayer(TVNetwork.Host,TVNetwork.PeerCount);
                var wanted=command.Action=="play"||(command.Action=="toggle"&&!(state.Media.Kind=="video"?state.VideoRequested:RelayJobs.ContainsKey(state.Id)?ResumeIntent.GetValueOrDefault(state.Id):state.Playing));
                if(state.Media.Kind=="youtube"&&wanted)
                    foreach(var other in Document.Screens.Where(s=>s.Id!=state.Id&&s.Media.Kind=="youtube").ToArray()){Stop(other);Touch(other);}
                if(state.Media.Kind=="video") { if(wanted)TVVideoNetwork.Play(state);else TVVideoNetwork.Pause(state); }
                else if(state.Media.Kind=="relay"&&state.PlaybackUrl.Length==0&&!RelayJobs.ContainsKey(state.Id)){StartRelay(state,false);ResumeIntent[state.Id]=wanted;}
                else if(RelayJobs.ContainsKey(state.Id))ResumeIntent[state.Id]=wanted;
                else{state.Reanchor(now);state.Playing=wanted;}
                break;
            case "seek":
                if(state.Media.Kind is "none" or "image"||!double.IsFinite(command.Position)||command.Position is < 0 or > 604800)throw new InvalidDataException("Invalid seek position.");
                state.Position=Math.Min(command.Position,state.Duration>0?state.Duration:604800);state.Anchor=now;break;
            case "volume":
                if(!float.IsFinite(command.Volume)||command.Volume is < 0 or > 1)throw new InvalidDataException("Invalid volume.");state.Volume=command.Volume;break;
            case "remember":state.Remember=command.Remember||state.Media.Kind=="image";break;
            case "favorite":
                if(state.Media.Kind=="none")throw new InvalidOperationException("Choose media before saving a favorite.");
                if(Document.Favorites.Count>=32)throw new InvalidOperationException("This save already has 32 favorites.");
                if(!Document.Favorites.Any(f=>JsonSerializer.Serialize(f)==JsonSerializer.Serialize(state.Media)))Document.Favorites.Add(state.Media.Clone());break;
            case "deleteFavorite":
                if(command.Index<0||command.Index>=Document.Favorites.Count)throw new InvalidDataException("Unknown favorite.");Document.Favorites.RemoveAt(command.Index);break;
            case "stop":Stop(state);break;
            case "retry":
                if(state.Media.Kind=="youtube")
                {
                    YouTubeRules.RequireSinglePlayer(TVNetwork.Host,TVNetwork.PeerCount);
                    foreach(var other in Document.Screens.Where(s=>s.Id!=state.Id&&s.Media.Kind=="youtube").ToArray()){Stop(other);Touch(other);}
                    state.Playing=true;
                }
                if(state.Media.Kind=="video"){TVVideoNetwork.Pause(state);state.MediaGeneration++;World?.Screens.GetValueOrDefault(state.Id)?.Retry();TVVideoNetwork.Play(state);}
                else if(state.Media.Kind=="relay")StartRelay(state,state.PlaybackId.Length>0);
                else{state.MediaGeneration++;World?.Screens.GetValueOrDefault(state.Id)?.Retry();}break;
            default:throw new InvalidDataException("Unknown TV control.");
        }
        if(Executed.Count>=512)Executed.Clear();Executed.Add(command.Token);Touch(state);
    }
    private void Touch(ScreenState state){state.Revision=++Revision;}
    internal void VideoChanged(ScreenState state){Touch(state);TVNetwork.Publish();}
    internal void SelectLocalVideo(string screen,string name)
    {
        if(VideoJobs.Count>=4)throw new InvalidOperationException("Wait for the current video scan to finish.");
        var key="select:"+Guid.NewGuid();VideoJobs.Add(key);var token=Lifetime.Token;var multiplayer=TVNetwork.PeerCount>1;
        Notice("Checking selected video…");
        _=Task.Run(()=>
        {
            try
            {
                var media=VideoCache.Select(TVConfig.Videos,name,multiplayer,token);
                MainThread.Enqueue(()=>{if(token.IsCancellationRequested)return;VideoJobs.Remove(key);if(media.Kind=="video")TVVideoNetwork.RegisterLocal(media);TVNetwork.Submit(new(){Action=media.Kind=="video"?"videoSelect":"select",Screen=screen,Media=media});Notice(media.Kind=="video"?"Video selected. Press Play to share it.":"Video selected. Press Play for local playback.");});
            }
            catch(Exception error){MainThread.Enqueue(()=>{if(token.IsCancellationRequested)return;VideoJobs.Remove(key);Notice(TVNetwork.SafeError(error));});}
        });
    }
    internal void FindSavedVideo(MediaSelection media)
    {
        if(VideoJobs.Contains(media.Source))return;
        VideoJobs.Add(media.Source);var token=Lifetime.Token;
        _=Task.Run(()=>
        {
            try
            {
                var local=VideoCache.Describe(TVConfig.Videos,media.VideoName,token);
                if(local.Source!=media.Source)throw new InvalidDataException("The saved video changed. Select it again from Local MP4.");
                MainThread.Enqueue(()=>{if(token.IsCancellationRequested)return;VideoJobs.Remove(media.Source);TVVideoNetwork.RegisterLocal(local);});
            }
            catch(Exception error){MainThread.Enqueue(()=>{if(token.IsCancellationRequested)return;VideoJobs.Remove(media.Source);TVVideoNetwork.Fail(media.Source,TVNetwork.SafeError(error));});}
        });
    }
    internal void SetDuration(string id,double duration)
    {
        var state=Document.Screens.FirstOrDefault(s=>s.Id==id);
        if(state is null||duration<=0||duration>604800||Math.Abs(state.Duration-duration)<0.1)return;
        state.Duration=duration;Touch(state);
    }
    internal void RemoveScreen(string id)
    {var state=Document.Screens.FirstOrDefault(s=>s.Id==id);if(state is null)return;Stop(state);Document.Screens.Remove(state);TVNetwork.Publish();}
    private void Stop(ScreenState state)
    {
        TVVideoNetwork.Forget(state.Id);state.VideoRequested=false;
        if(RelayJobs.Remove(state.Id,out var job)){job.Cancel();job.Dispose();}ResumeIntent.Remove(state.Id);
        if(state.PlaybackId.Length>0)ReleaseRelay(state.PlaybackId,RelayOwners.GetValueOrDefault(state.Id));
        RelayOwners.Remove(state.Id);
        state.Media=new();state.Playing=false;state.Position=0;state.Anchor=TVNetwork.Now;state.Duration=0;state.PlaybackId="";state.PlaybackUrl="";state.ExpiresAt=null;state.Status="";state.MediaGeneration++;
    }
    private void ReleaseRelay(string id,Func<RelayClient>? factory=null)
    {
        factory??=TVConfig.RelayFactory();
        _=Task.Run(async()=>{try{using var client=factory();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));await client.Stop(id,timeout.Token);}catch{}});
    }
    internal void QueryRelay(TVCommand command,Action<TVMessage> reply)
    {
        if(Interlocked.Increment(ref RelayQueries)>4){Interlocked.Decrement(ref RelayQueries);throw new InvalidOperationException("Relay is handling other catalog requests. Try again shortly.");}
        var cancellation=Lifetime.Token;var factory=TVConfig.RelayFactory();
        _=Task.Run(async()=>
        {
            try{using var client=factory();var json=await client.Query(command.Operation,command.Text,command.Season,cancellation);MainThread.Enqueue(()=>{if(!cancellation.IsCancellationRequested)reply(new(){Token=command.Token,Json=json,HostTime=TVNetwork.Now});});}
            catch(Exception error){MainThread.Enqueue(()=>{if(!cancellation.IsCancellationRequested)reply(new(){Token=command.Token,Success=false,Text=TVNetwork.SafeError(error)});});}
            finally{Interlocked.Decrement(ref RelayQueries);}
        });
    }
    private void StartRelay(ScreenState state,bool refresh)
    {
        if(RelayJobs.ContainsKey(state.Id))return;
        // Fail setup synchronously, before changing a previously usable session.
        var factory=refresh?RelayOwners.GetValueOrDefault(state.Id)??TVConfig.RelayFactory():TVConfig.RelayFactory();
        using(var setup=factory()){}
        var media=state.Media.Clone();var playbackId=state.PlaybackId;
        var resume=refresh?state.Playing:true;state.Reanchor(TVNetwork.Now);state.Playing=false;state.Status=refresh?"Renewing Relay playback":"Preparing Relay episode";
        var cancellation=CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token);cancellation.CancelAfter(TimeSpan.FromMinutes(31));
        RelayJobs[state.Id]=cancellation;ResumeIntent[state.Id]=resume;RelayOwners[state.Id]=factory;Touch(state);
        var screenId=state.Id;var position=state.Position;var requestId=Guid.NewGuid().ToString();var token=cancellation.Token;
        _=Task.Run(async()=>
        {
            string allocated="";
            try
            {
                using var client=factory();
                var result=refresh&&playbackId.Length>0?await client.Refresh(playbackId,position,token):await client.Create(media,requestId,token);
                allocated=result.Id;
                while(true)
                {
                    result.ValidateSelection(media);var descriptor=result;
                    MainThread.Enqueue(()=>ApplyRelay(screenId,cancellation,descriptor));
                    if(result.Status!="preparing")break;
                    await Task.Delay(2000,token);result=await client.Get(result.Id,token);
                }
            }
            catch(Exception error)
            {
                if(allocated.Length>0)ReleaseRelay(allocated,factory);
                var message=token.IsCancellationRequested?"Relay preparation cancelled or timed out.":TVNetwork.SafeError(error);
                MainThread.Enqueue(()=>{if(RelayJobs.TryGetValue(screenId,out var current)&&current==cancellation){RelayJobs.Remove(screenId);ResumeIntent.Remove(screenId);var screen=Document.Screens.FirstOrDefault(s=>s.Id==screenId);if(screen is not null){screen.Status=message;screen.Playing=false;screen.ExpiresAt=null;screen.PlaybackUrl="";screen.PlaybackId="";Touch(screen);TVNetwork.Publish();Notice(message);}cancellation.Dispose();}});
            }
        });
    }
    private void ApplyRelay(string id,CancellationTokenSource job,RelayPlayback result)
    {
        if(!Ready||!RelayJobs.TryGetValue(id,out var current)||current!=job||job.IsCancellationRequested)return;
        var state=Document.Screens.FirstOrDefault(s=>s.Id==id);if(state is null)return;
        state.PlaybackId=result.Id;state.Status=result.Message is {Length:>0}?result.Message[..Math.Min(256,result.Message.Length)]:result.Status;
        if(result.Status=="ready")
        {
            state.PlaybackUrl=result.MediaUrl!;state.ExpiresAt=result.ExpiresAt;state.Duration=result.DurationSeconds??0;state.Anchor=TVNetwork.Now;
            state.Playing=ResumeIntent.GetValueOrDefault(id);state.MediaGeneration++;state.Status="Ready";
        }
        if(result.Status!="preparing")
        {RelayJobs.Remove(id);ResumeIntent.Remove(id);job.Dispose();if(result.Status!="ready"){state.PlaybackUrl="";state.Playing=false;state.ExpiresAt=null;}}
        Touch(state);TVNetwork.Publish();
    }
    internal void Receive(TVMessage message)
    {
        if(message.Type=="state"&&!TVNetwork.Host)
        {
            if(message.Screens.Count>8||message.AllIds.Count>64)throw new InvalidDataException("Invalid TV snapshot.");
            foreach(var state in message.Screens)
            {
                state.Validate();var old=Document.Screens.FirstOrDefault(s=>s.Id==state.Id);
                if(old is null)Document.Screens.Add(state);else if(state.Revision>=old.Revision){Document.Screens.Remove(old);Document.Screens.Add(state);TVVideoNetwork.ObserveSnapshot(old,state);}
            }
            Document.Screens.RemoveAll(s=>!message.AllIds.Contains(s.Id));
        }
        else if(message.Type=="favorites"&&!TVNetwork.Host)
        {if(message.Index==0)Document.Favorites.Clear();foreach(var favorite in message.Favorites){favorite.Validate();if(Document.Favorites.Count<32)Document.Favorites.Add(favorite);}}
        else Panel?.Reply(message);
        if(!message.Success)Notice(message.Text);
    }
    internal void Notice(string text)=>Panel?.Notice(text);
    private static void SaveStarted(string saveFolderPath)
    {
        var mod=Current;if(mod?.Ready!=true||!TVNetwork.Host)return;
        mod.Document.Validate();mod.SaveSnapshot=mod.Document.SaveJson();mod.SaveTarget=Path.Combine(saveFolderPath,"Modded","PersonalTV","Screens.json");
        var approved=SaveManager.Instance.ApprovedBaseLevelPaths;if(!approved.Contains("Modded"))approved.Add("Modded");
    }
    private void SaveComplete()
    {
        if(SaveTarget.Length==0||!TVNetwork.Host)return;
        try
        {
            var folder=Path.GetDirectoryName(SaveTarget)!;Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(folder,"Images"));
            var saved=TVDocument.Load(SaveSnapshot,TVNetwork.Now);
            foreach(var hash in saved.Screens.Select(s=>s.Media).Concat(saved.Favorites).Where(m=>m.Kind=="image").Select(m=>m.Source).Distinct())
                if(Cache.Has(hash))File.Copy(Cache.PathFor(hash),Path.Combine(folder,"Images",hash+".png"),true);
            File.WriteAllText(SaveTarget+".tmp",SaveSnapshot);
            if(File.Exists(SaveTarget))File.Copy(SaveTarget,SaveTarget+".previous",true);
            File.Move(SaveTarget+".tmp",SaveTarget,true);
        }
        catch(Exception error){MelonLogger.Error("TV state save failed: "+TVNetwork.SafeError(error));Notice("TV state could not be saved.");}
        finally{SaveTarget="";SaveSnapshot="";}
    }
    private void ClearSession()
    {
        TVInteraction.Reset();
        var wasHost=TVNetwork.Host;Ready=false;Lifetime.Cancel();Lifetime.Dispose();Lifetime=new();
        foreach(var job in RelayJobs.Values){job.Cancel();job.Dispose();}RelayJobs.Clear();ResumeIntent.Clear();
        if(wasHost)foreach(var state in Document.Screens)if(state.PlaybackId.Length>0)ReleaseRelay(state.PlaybackId,RelayOwners.GetValueOrDefault(state.Id));
        RelayOwners.Clear();
        VideoJobs.Clear();
        Panel?.Dispose();Panel=null;World?.Dispose();World=null;TVNetwork.Reset();Executed.Clear();
        Document=new();Revision=0;SavePath="";SaveTarget="";SaveSnapshot="";NextPublish=0;
    }
    public void OnDeinitializeMelon()
    {
        Diagnostic?.RestoreConfig();
        Subscriptions.Dispose();
        ClearSession();Lifetime.Dispose();Current=null;
    }
}
