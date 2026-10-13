using System.Text.Json;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Television;

internal sealed class TVPanel : IDisposable
{
    private readonly GameObject Root;
    private readonly RectTransform Frame,Picker;
    private readonly RawImage Preview;
    private readonly TextMeshProUGUI Title,Status,Message,Timing,PlayLabel,RememberLabel,ChapterTitle;
    private readonly Button CaptionsButton,ChaptersButton;
    private readonly List<GameObject> ChapterMarks=new();
    private IReadOnlyList<YouTubeChapter>? ChapterCatalog;
    private IReadOnlyList<YouTubeChapter> VisibleChapters=Array.Empty<YouTubeChapter>();
    private string ChapterSnapshot="";
    private string ChapterMediaSnapshot="";
    private double ChapterDuration=-1;
    private readonly Slider Timeline,Volume;
    private readonly GameInput.ExitDelegate ExitHandler;
    private readonly List<GameObject> PageControls=new();
    private readonly Dictionary<string,(Action<string> Callback,double Started,int Epoch)> Queries=new();
    private string Screen="",Tab="images";
    private int Page,Epoch;
    private bool Opened,Fullscreen,Updating,SeekDirty,VolumeDirty,PreviousLook,PreviousMove,PreviousHotbar,PreviousEquipping,PreviousTyping;
    private List<(string Label,Action Select)> Rows=new();
    private string Heading="";
    private TMP_InputField? Search;
    private RelayItem? SelectedTitle,Episode,Source;
    private string SubtitleMode="burned";
    private string FavoriteSnapshot="";
    internal bool IsOpen=>Opened;
    internal void CheckTab(string tab){if(Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--tv-system-check=")))Switch(tab);}
    internal string CheckHeading=>Heading;
    internal int CheckRowCount=>Rows.Count;
    internal void CheckRow(int index)=>Rows[index].Select();
    internal void CheckSearch(){Search!.text="fixture";CheckClick("Search");}
    internal void CheckClick(string label)=>Root.GetComponentsInChildren<Button>(true).First(b=>b.name==label).onClick.Invoke();
    // Programmatic diagnostic drags include release; a physical mouse held in
    // another app must not leave a diagnostic slider waiting for mouse-up.
    internal void CheckSeek(double position,double duration){Timeline.value=(float)(position/duration);CommitSliders();}
    internal void CheckVolume(float volume){Volume.value=volume;CommitSliders();}
    internal string CheckControls=>$"open={Opened}, volume={Volume.value}, pending={VolumeDirty}, mouseHeld={Mouse.current?.leftButton.isPressed}, notice={Message.text}";
    internal void CheckYouTube(string link){Search!.text=link;CheckClick("Play YouTube");}
    internal int CheckChapterMarks=>ChapterMarks.Count;
    internal string CheckChapterTitle=>ChapterTitle.text;
    internal bool CheckYouTubeSource=>YouTubeTab?.activeSelf==true;
    internal bool CheckYouTubeControls=>CaptionsButton.gameObject.activeSelf||ChaptersButton.gameObject.activeSelf;
    private readonly List<GameObject> Tabs=new();
    private GameObject? YouTubeTab;
    private bool? YouTubeAvailable;
    private double NextFavoritesCheck;

    internal TVPanel()
    {
        Root=new GameObject("Wall Screen controls",new[]{Il2CppType.Of<RectTransform>()});
        var canvas=Root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;
        var scaler=Root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);
        Root.AddComponent<GraphicRaycaster>();
        Frame=TVWidgets.Rect(Root.transform,"Screen panel",0,0,1640,980);Frame.anchorMin=Frame.anchorMax=Frame.pivot=new(.5f,.5f);Frame.anchoredPosition=Vector2.zero;
        Frame.gameObject.AddComponent<Image>().color=new(.025f,.045f,.065f,.98f);
        Title=TVWidgets.Label(Frame,"WALL SCREEN",30,14,990,56,27);
        CaptionsButton=TVWidgets.Button(Frame,"CC · Toggle",1050,18,165,36,()=>Send(new(){Action="youtubeCaptions"}));
        TVWidgets.Button(Frame,"Leave · Esc",1450,18,160,36,Close);
        var preview=TVWidgets.Rect(Frame,"Video preview",30,90,790,444);Preview=preview.gameObject.AddComponent<RawImage>();Preview.raycastTarget=false;Preview.color=Color.white;
        Status=TVWidgets.Label(Frame,"",30,548,790,62,21);
        Picker=TVWidgets.Rect(Frame,"Source picker",860,140,750,650);
        ChaptersButton=TVWidgets.Button(Frame,"Chapters",1225,18,190,36,()=>{Fullscreen=false;Picker.gameObject.SetActive(true);foreach(var tab in Tabs)tab.SetActive(true);Switch("chapters");});
        var tabs=new[]{("images","Images"),("url","Video URL"),("youtube","YouTube"),("relay","Relay"),("favorites","Favorites"),("local","Local MP4")};
        for(var i=0;i<tabs.Length;i++){var tab=tabs[i];var control=TVWidgets.Button(Frame,tab.Item2,860+i*126,88,118,38,()=>Switch(tab.Item1)).gameObject;Tabs.Add(control);if(tab.Item1=="youtube")YouTubeTab=control;}
        var play=TVWidgets.Button(Frame,"Play",30,818,125,40,()=>Send(new(){Action="toggle"}));PlayLabel=play.GetComponentInChildren<TextMeshProUGUI>();
        TVWidgets.Button(Frame,"−10s",165,818,80,40,()=>Seek(-10));TVWidgets.Button(Frame,"+10s",255,818,80,40,()=>Seek(10));
        TVWidgets.Button(Frame,"Stop",345,818,95,40,()=>Send(new(){Action="stop"}));
        TVWidgets.Button(Frame,"Retry",450,818,95,40,()=>Send(new(){Action="retry"}));
        TVWidgets.Button(Frame,"Save favorite",555,818,180,40,()=>Send(new(){Action="favorite"}));
        var remember=TVWidgets.Button(Frame,"Remember: off",745,818,195,40,()=>Send(new(){Action="remember",Remember=State()?.Remember!=true}));RememberLabel=remember.GetComponentInChildren<TextMeshProUGUI>();
        TVWidgets.Button(Frame,"Full screen",950,818,155,40,()=>{Fullscreen=!Fullscreen;Picker.gameObject.SetActive(!Fullscreen);UpdateSourceTabs();});
        TVWidgets.Label(Frame,"Volume",1130,824,85,28,18);Volume=TVWidgets.Slider(Frame,1220,826,175,value=>{if(!Updating)VolumeDirty=true;});
        TVWidgets.Button(Frame,"Mute",1410,818,90,40,()=>Send(new(){Action="volume",Volume=0}));
        Timeline=TVWidgets.Slider(Frame,30,875,1330,value=>{if(!Updating)SeekDirty=true;});
        Timing=TVWidgets.Label(Frame,"",1380,870,230,35,18);
        ChapterTitle=TVWidgets.Label(Frame,"",30,900,1330,23,16);
        Message=TVWidgets.Label(Frame,"",30,920,1580,44,18);
        ExitHandler=DelegateSupport.ConvertDelegate<GameInput.ExitDelegate>((Action<ExitAction>)(action=>{if(Opened){action.used=true;Close();}}))!;
        GameInput.RegisterExitListener(ExitHandler,10);Root.SetActive(false);
    }
    private ScreenState? State()=>TelevisionFeature.Current!.Document.Screens.FirstOrDefault(s=>s.Id==Screen);
    internal void Open(string screen)
    {
        if(Opened)return;
        var world=TelevisionFeature.Current!.World!;
        if(!world.Screens.ContainsKey(screen))return;
        Screen=screen;Epoch++;Fullscreen=false;SeekDirty=VolumeDirty=false;
        ChapterCatalog=null;VisibleChapters=Array.Empty<YouTubeChapter>();
        var initialState=State();if(initialState is not null)UpdateChapters(world.Screens[Screen],initialState,initialState.Target(TVNetwork.Now));
        var camera=PlayerCamera.Instance;var inventory=PlayerInventory.Instance;
        PreviousLook=camera.CanLook;PreviousMove=PlayerMovement.Instance._canMove;PreviousHotbar=inventory.HotbarEnabled;
        PreviousEquipping=inventory.EquippingEnabled;PreviousTyping=GameInput.IsTyping;
        camera.AddActiveUIElement("PersonalTV");camera.SetCanLook(false);camera.FreeMouse(true);
        PlayerMovement.Instance._canMove=false;inventory.SetInventoryEnabled(false);GameInput.IsTyping=true;
        Opened=true;Root.SetActive(true);Picker.gameObject.SetActive(true);UpdateSourceTabs();Notice("Playback continues when you leave. Images are remembered automatically.");Switch(Tab);
    }
    internal void Close()
    {
        if(!Opened)return;Opened=false;Epoch++;Queries.Clear();if(Root)Root.SetActive(false);if(Preview)Preview.texture=null;
        var camera=PlayerCamera.Instance;
        if(camera){camera.RemoveActiveUIElement("PersonalTV");camera.SetCanLook(PreviousLook);if(camera.ActiveUIElementCount==0)camera.LockMouse(true);}
        if(PlayerMovement.Instance)PlayerMovement.Instance._canMove=PreviousMove;
        if(PlayerInventory.Instance){PlayerInventory.Instance.SetInventoryEnabled(PreviousHotbar);PlayerInventory.Instance.SetEquippingEnabled(PreviousEquipping);}
        GameInput.IsTyping=PreviousTyping;
    }
    internal void Notice(string text){if(Message)Message.text=text.Length<=300?text:text[..300];}
    private void Send(TVCommand command){command.Screen=Screen;TVNetwork.Submit(command);}
    private void Select(MediaSelection media)=>Send(new(){Action="select",Media=media});
    private void Seek(double delta){var state=State();if(state is not null)Send(new(){Action="seek",Position=Math.Max(0,state.Target(TVNetwork.Now)+delta)});}
    private void CommitSliders()
    {
        var state=State();if(state is null||!Opened)return;
        if(SeekDirty){SeekDirty=false;Send(new(){Action="seek",Position=Timeline.value*state.Duration});}
        if(VolumeDirty){VolumeDirty=false;Send(new(){Action="volume",Volume=Volume.value});}
    }
    internal void Reply(TVMessage message)
    {
        if(Queries.Remove(message.Token,out var query)&&query.Epoch==Epoch&&Opened)
        {if(message.Success){try{query.Callback(message.Json);}catch(Exception error){Notice(TVNetwork.SafeError(error));}}else Notice(message.Text);}
    }
    private void Query(string operation,string value,Action<string> callback,int season=0)
    {
        var command=new TVCommand{Action="relayQuery",Screen=Screen,Operation=operation,Text=value,Season=season};
        Queries[command.Token]=(callback,Time.realtimeSinceStartup,Epoch);TVNetwork.Submit(command);Notice("Contacting Relay…");
    }
    private T Decode<T>(string json)=>JsonSerializer.Deserialize<T>(json,RelayClient.Json)??throw new InvalidDataException("Relay returned no catalog data.");
    private void Switch(string tab)
    {
        if(tab is "youtube" or "chapters"&&!TVYouTube.Available)tab="images";
        Tab=tab;Page=0;Epoch++;Queries.Clear();Rows.Clear();ClearPage();
        switch(tab)
        {
            case "images":LocalFiles(false);break;
            case "local":LocalFiles(true);break;
            case "url":Url();break;
            case "youtube":YouTube();break;
            case "chapters":Chapters();break;
            case "favorites":Favorites();break;
            case "relay":RelayHome();break;
        }
    }
    private void UpdateSourceTabs()
    {
        var available=TVYouTube.Available;var changed=YouTubeAvailable!=available;YouTubeAvailable=available;var index=0;
        foreach(var tab in Tabs)
        {
            var included=tab!=YouTubeTab||available;tab.SetActive(included&&!Fullscreen);
            if(included)TVWidgets.Place(tab.GetComponent<RectTransform>(),860+index++*126,88,118,38);
        }
        if(changed&&Opened&&Tab is "youtube" or "chapters"&&!available)Switch("images");
        else if(changed&&Opened&&Tab=="favorites")Favorites();
    }
    private void ClearPage(){Search=null;foreach(var control in PageControls)if(control)Object.Destroy(control);PageControls.Clear();}
    private void Track(Component control)=>PageControls.Add(control.gameObject);
    private void Label(string text,float y,float h=45)=>Track(TVWidgets.Label(Picker,text,0,y,750,h,19));
    private void Button(string label,float x,float y,float w,Action action)=>Track(TVWidgets.Button(Picker,label,x,y,w,38,action));
    private TMP_InputField Input(string hint,float y,int limit=256,bool password=false)
    {var input=TVWidgets.Input(Picker,hint,0,y,750,40,limit,password);Track(input);return input;}
    private void LocalFiles(bool video)
    {
        var directory=video?TVConfig.Videos:TVConfig.Images;
        Heading=video?(TVNetwork.PeerCount>1?"Local MP4 · Play to share · up to 512 MiB":"Local MP4 · Play locally · no sharing size limit"):"Images · PNG or JPEG · shared with other players";
        var files=Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly)
            .Where(f=>video?Path.GetExtension(f).Equals(".mp4",StringComparison.OrdinalIgnoreCase):new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(Path.GetFileName).Where(f=>f is not null).Take(1000).OrderBy(f=>f,StringComparer.OrdinalIgnoreCase).ToArray();
        Rows=files.Select(f=>(f!, (Action)(()=>{
            if(video)TelevisionFeature.Current!.SelectLocalVideo(Screen,f!);
            else{Notice("Importing and sharing image…");TVNetwork.UploadImage(Screen,Path.GetFileNameWithoutExtension(f!)[..Math.Min(160,Path.GetFileNameWithoutExtension(f!).Length)],TVArtwork.Import(f!));}
        }))).ToList();
        DrawRows();Label("Put files in: "+directory,525,100);Button("Rescan folder",0,635,180,()=>Switch(Tab));
    }
    private void DrawRows()
    {
        ClearPage();Label(Heading,0,55);
        if(Rows.Count==0)Label("No files or results here yet.",70);
        var offset=Page*8;
        foreach(var (row,index) in Rows.Skip(offset).Take(8).Select((row,i)=>(row,i)))Button(row.Label,0,70+index*49,750,row.Select);
        Button("Previous",0,475,130,()=>{Page=Math.Max(0,Page-1);Redraw();});
        Label($"Page {Page+1} / {Math.Max(1,(Rows.Count+7)/8)}",475,38);
        // The page label is right aligned by position, avoiding overlap with the navigation buttons.
        PageControls[^1].GetComponent<RectTransform>().anchoredPosition=new(155,-475);
        Button("Next",615,475,135,()=>{Page=Math.Min(Math.Max(0,(Rows.Count-1)/8),Page+1);Redraw();});
    }
    private void Redraw(){if(Tab is "images" or "local")LocalFiles(Tab=="local");else if(Tab=="favorites")Favorites();else if(Tab=="chapters")Chapters();else{DrawRows();RelayNavigation();}}
    private void Chapters()
    {
        Heading="YouTube chapters";
        Rows=VisibleChapters.Select(chapter=>($"{Format(chapter.Start)} · {chapter.Title}",(Action)(()=>Send(new(){Action="seek",Position=chapter.Start})))).ToList();
        if(Rows.Count>0){DrawRows();Label("Choose a chapter to jump to its start. Marks on the playback bar show chapter boundaries.",535,100);}
        else{ClearPage();Label(Heading,0);Label(State()?.Media.Kind=="youtube"?(ChapterSnapshot.Length>0?ChapterSnapshot:"Waiting for the video's duration…"):"Choose a YouTube video to see its chapters.",70,150);}
    }
    private void UpdateChapters(ScreenController controller,ScreenState state,double position)
    {
        var youtube=state.Media.Kind=="youtube"&&TVYouTube.Available;
        CaptionsButton.gameObject.SetActive(youtube);CaptionsButton.interactable=youtube&&controller.CanToggleCaptions;
        ChaptersButton.gameObject.SetActive(youtube);
        var catalog=controller.Chapters;var status=controller.ChapterStatus;
        var media=state.Media.Kind+":"+state.Media.Source;
        if(!ReferenceEquals(ChapterCatalog,catalog)||ChapterDuration!=state.Duration||ChapterSnapshot!=status||ChapterMediaSnapshot!=media)
        {
            ChapterCatalog=catalog;ChapterDuration=state.Duration;ChapterSnapshot=status;ChapterMediaSnapshot=media;
            VisibleChapters=youtube&&state.Duration>0?catalog.Where(c=>c.Start<state.Duration).ToArray():Array.Empty<YouTubeChapter>();
            foreach(var mark in ChapterMarks)if(mark)Object.Destroy(mark);ChapterMarks.Clear();
            foreach(var chapter in VisibleChapters)
            {
                var tick=TVWidgets.Rect(Timeline.transform,"Chapter boundary",7+(float)(chapter.Start/state.Duration)*1316,0,2,24);
                var image=tick.gameObject.AddComponent<Image>();image.color=new(1,1,1,.65f);image.raycastTarget=false;ChapterMarks.Add(tick.gameObject);
            }
            if(Tab=="chapters"){Page=Math.Min(Page,Math.Max(0,(VisibleChapters.Count-1)/8));Chapters();}
        }
        var current=youtube?VisibleChapters.LastOrDefault(c=>c.Start<=position):null;
        ChapterTitle.text=current is null?"":$"{Format(current.Start)} · {current.Title}";
    }
    private void Url()
    {
        Label("Direct video URL",0);Label("Use an HTTP(S) media file that the game can decode. MP4 with H.264 video and AAC audio works best.",50,100);
        var input=Input("https://example.com/video.mp4",165,4096);var title=Input("Optional display title",220,160);
        Button("Play URL",0,280,190,()=>Select(new(){Kind="url",Source=MediaRules.VideoUrl(input.text.Trim()).AbsoluteUri,Title=string.IsNullOrWhiteSpace(title.text)?"Video URL":title.text.Trim()}));
        Label("Each viewer downloads this URL directly. Use Remember to keep it with this screen, or Save favorite to reuse it later.",345,110);
    }
    private void YouTube()
    {
        Heading="YouTube · single-player";Label(Heading,0);
        if(TVNetwork.PeerCount>1){Label("YouTube playback is available in single-player only.",60,90);return;}
        if(!TVYouTube.Available){Label(TVYouTube.UnavailableReason,60,150);return;}
        Label("Paste a YouTube video link, then press Play YouTube. One Wall Screen can play YouTube at a time.",55,90);
        var input=Input("https://www.youtube.com/watch?v=…",165,4096);Search=input;
        Button("Play YouTube",0,225,210,()=>{
            var id=YouTubeRules.VideoId(input.text);
            Select(new(){Kind="youtube",Source=id,Title="YouTube · "+id});
        });
        Label("Playback continues when you leave. Use CC · Toggle to turn captions on or off. Open Chapters to jump between sections. Sound becomes quieter as you move away.",295,130);
        Label("Remember and Favorites keep the video selection. Saved screens start paused.",450,90);
    }
    private void Favorites()
    {
        var document=TelevisionFeature.Current!.Document;FavoriteSnapshot=JsonSerializer.Serialize(document.Favorites);Heading="Saved media favorites";
        Rows=document.Favorites.Select((media,index)=>(Media:media,Index:index)).Where(f=>f.Media.Kind!="youtube"||TVYouTube.Available).Select(f=>($"{f.Index+1}. "+(f.Media.Title.Length>0?f.Media.Title:$"{f.Media.Kind}: {f.Media.Source}"),(Action)(()=>Select(f.Media.Clone())))).ToList();DrawRows();
        Label("Select a favorite to play it. Relay episodes resolve a fresh URL each time.",525,50);
        var remove=Input("Favorite number to remove (starting at 1)",580,2);
        Button("Remove",555,630,195,()=>{if(!int.TryParse(remove.text,out var number))throw new InvalidDataException("Enter a favorite number.");Send(new(){Action="deleteFavorite",Index=number-1});Favorites();});
    }
    private void RelayHome()
    {
        Label("Relay · search your catalog",0);Search=Input("Anime or show title",65,200);
        var search=Search;Button("Search",0,125,160,()=>{
            var value=search.text.Trim();if(value.Length==0)throw new InvalidDataException("Enter a title to search.");
            Query("capabilities","",json=>{
                var capabilities=Decode<RelayCapabilities>(json);
                if(capabilities.ProtocolVersion!=1||!capabilities.Profiles.Contains("mp4-h264-aac"))throw new InvalidDataException("Relay does not support the game's playback profile.");
                SubtitleMode=capabilities.SubtitleModes.Contains("burned")?"burned":"none";
                Query("search",value,result=>{
                    Heading="Relay search: "+value;Page=0;Rows=Decode<RelayList>(result).Items.Take(1000).Select(item=>(item.Title+(item.Year is {} year?$" ({year})":""),(Action)(()=>TitleSeasons(item)))).ToList();DrawRows();RelayNavigation();Notice("Choose a title.");
                });
            });
        });
        Label("The host configures Relay once. Everyone nearby can browse and control playback. Create a scoped token in Relay's Settings → Game Integration.",200,130);
        if(TVNetwork.Host)Button("Configure Relay",0,350,240,RelaySetup);
        else Label("Ask the host to configure the API origin and scoped game token.",350,100);
    }
    private void RelaySetup()
    {
        ClearPage();Label("Relay host setup",0);Label("Use the API origin and a scoped game token from Relay. These credentials stay on the host.",50,90);
        var origin=Input("https://relay-api.example.com",155,4096);origin.text=TVConfig.RelayOrigin.Value;
        var token=Input("Scoped game token",210,2048,true);token.text=TVConfig.RelayToken.Value;
        Button("Save & test",0,270,190,()=>{TVConfig.SaveRelay(origin.text.Trim(),token.text.Trim());Query("capabilities","",json=>{var cap=Decode<RelayCapabilities>(json);if(cap.ProtocolVersion!=1||!cap.Profiles.Contains("mp4-h264-aac"))throw new InvalidDataException("Relay's game playback profile is unavailable.");RelayHomeClean();Notice("Relay connection is ready.");});});
        Button("Back",205,270,130,RelayHomeClean);
        Label("The token is stored in the host's local MelonPreferences.cfg. It is never added to a save or sent to other players.",335,110);
    }
    private void RelayHomeClean(){ClearPage();RelayHome();}
    private void TitleSeasons(RelayItem title)
    {
        SelectedTitle=title;Episode=null;Source=null;
        Query("title",title.Id,json=>{
            var details=Decode<RelayTitle>(json);Heading=details.Title+" · seasons";Page=0;
            Rows=details.Seasons.Select(season=>(season.Title.Length>0?season.Title:$"Season {season.Number}",(Action)(()=>Episodes(title,season.Number)))).ToList();DrawRows();RelayNavigation();Notice("Choose a season.");
        });
    }
    private void Episodes(RelayItem title,int season)
    {
        Query("episodes",title.Id,json=>{
            Heading=title.Title+$" · season {season}";Page=0;
            Rows=Decode<RelayList>(json).Items.Take(1000).Select(episode=>($"{episode.Number}. {episode.Title}",(Action)(()=>Sources(episode)))).ToList();DrawRows();RelayNavigation();Notice("Choose an episode.");
        },season);
    }
    private void Sources(RelayItem episode)
    {
        Episode=episode;Source=null;
        Query("sources",episode.Id,json=>{
            Heading=episode.Title+" · sources";Page=0;
            Rows=Decode<RelayList>(json).Items.Select(source=>(source.Name+(source.Playable?"":" · unavailable"),(Action)(()=>{
                if(!source.Playable)throw new InvalidOperationException(source.Reason??"This source is not compatible with the game.");
                Source=source;if(source.Versions.Count==0){PlayEpisode(null);return;}
                Heading=source.Name+" · versions";Page=0;Rows=source.Versions.Select(version=>(version.Label+(version.Playable?"":" · unavailable"),(Action)(()=>{if(!version.Playable)throw new InvalidOperationException(version.Reason??"This version is unavailable.");PlayEpisode(version.Id);}))).ToList();DrawRows();RelayNavigation();
            }))).ToList();DrawRows();RelayNavigation();Notice("Choose a playable source.");
        });
    }
    private void PlayEpisode(string? version)
    {
        if(Episode is null)return;
        Select(new(){Kind="relay",Source=Episode.Id,SourceId=Source?.Id,VersionId=version,Title=(SelectedTitle?.Title+" · "+Episode.Title)[..Math.Min(160,(SelectedTitle?.Title+" · "+Episode.Title).Length)],SubtitleMode=SubtitleMode});Notice("Relay is preparing the episode. You can leave while it loads.");
    }
    private void RelayNavigation()
    {
        Button("New search",0,545,155,RelayHomeClean);
        if(SelectedTitle is {} title)Button("Seasons",165,545,135,()=>TitleSeasons(title));
        if(Episode is not null)Button("Auto source",310,545,170,()=>{Source=null;PlayEpisode(null);});
        Button(SubtitleMode=="burned"?"Subtitles: burned":"Subtitles: none",490,545,260,()=>{SubtitleMode=SubtitleMode=="burned"?"none":"burned";Notice("Subtitle choice: "+SubtitleMode);});
    }
    internal void Tick()
    {
        if(!Opened)return;
        if(YouTubeAvailable!=TVYouTube.Available)UpdateSourceTabs();
        var mod=TelevisionFeature.Current!;var controller=mod.World?.Screens.GetValueOrDefault(Screen);
        if(controller is null||!Player.Local||Player.Local.IsUnconscious||!Player.Local.Health.IsAlive||Vector3.Distance(Player.Local.transform.position,controller.Item.transform.position)>5){Close();return;}
        if(Keyboard.current?.escapeKey.wasPressedThisFrame==true){Close();return;}
        var state=State();if(state is null)return;
        Title.text=state.Media.Title.Length>0?state.Media.Title:"WALL SCREEN";Status.text=controller.Status+(state.Status.Length>0?"\n"+state.Status:"");
        PlayLabel.text=state.Playing||state.VideoRequested?"Pause":"Play";RememberLabel.text=state.Remember?"Remember: on":"Remember: off";
        Preview.texture=controller.Texture;
        var maxW=Fullscreen?1580:790;var maxH=Fullscreen?704:444;var texture=controller.Texture;
        var ratio=texture?texture!.width/(float)Math.Max(1,texture.height):16f/9;
        var width=Math.Min(maxW,maxH*ratio);var height=width/ratio;
        TVWidgets.Place(Preview.rectTransform,30+(maxW-width)/2,90+(maxH-height)/2,width,height);Status.gameObject.SetActive(!Fullscreen);
        var position=state.Target(TVNetwork.Now);Timing.text=$"{Format(position)} / {(state.Duration>0?Format(state.Duration):"--:--")}";
        Updating=true;try{if(!SeekDirty)Timeline.SetValueWithoutNotify(state.Duration>0?(float)(position/state.Duration):0);if(!VolumeDirty)Volume.SetValueWithoutNotify(state.Volume);}finally{Updating=false;}
        Timeline.interactable=(state.Media.Kind is "url" or "relay" or "local" or "youtube")&&state.Duration>0&&controller.Seekable;
        UpdateChapters(controller,state,position);
        if(Mouse.current?.leftButton.isPressed!=true)CommitSliders();
        foreach(var token in Queries.Where(p=>Time.realtimeSinceStartup-p.Value.Started>65).Select(p=>p.Key).ToArray()){Queries.Remove(token);Notice("Relay request timed out. Try again.");}
        if(Tab=="favorites"&&TVNetwork.Now>=NextFavoritesCheck){NextFavoritesCheck=TVNetwork.Now+.5;if(FavoriteSnapshot!=JsonSerializer.Serialize(mod.Document.Favorites))Favorites();}
    }
    private static string Format(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(seconds>=3600?@"h\:mm\:ss":@"mm\:ss");
    public void Dispose(){Close();GameInput.DeregisterExitListener(ExitHandler);if(Root)Object.Destroy(Root);}
}
