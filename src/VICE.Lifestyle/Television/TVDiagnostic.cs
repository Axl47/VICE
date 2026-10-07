using Il2CppScheduleOne;
using Il2CppScheduleOne.Building;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI.Shop;
using MelonLoader;
using UnityEngine;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Television;

// Explicit opt-in check; the caller supplies an isolated workspace save copy.
internal sealed class TVDiagnostic
{
    private readonly TelevisionFeature Mod;
    private readonly string Output,Save,Video;
    private readonly bool Reload;
    private readonly bool PromptOnly;
    private bool Started,Done;
    private int Phase;
    private double Next,Deadline,PreviousPosition;
    private SurfaceItem? Item;
    private Vector3 Near;
    private Vector3 PlacementPoint,Outward;
    private double PromptY;
    private bool GhostLogged;
    private string Hash="";
    private readonly string Origin,PreviousOrigin,PreviousToken;
    private bool BeforeLook,BeforeMove,BeforeInventory,BeforeEquipping;
    private ScreenController Controller=>Mod.World!.Screens[Item!.GUID.ToString()];
    private ScreenState State=>Mod.Document.Screens.First(s=>s.Id==Item!.GUID.ToString());
    internal static TVDiagnostic? Create(TelevisionFeature mod)
    {
        var args=Environment.GetCommandLineArgs();var output=args.FirstOrDefault(a=>a.StartsWith("--tv-system-check="))?[18..];
        if(output is null)return null;
        return new(mod,output,args.FirstOrDefault(a=>a.StartsWith("--tv-check-save="))?[16..]??"",args.FirstOrDefault(a=>a.StartsWith("--tv-check-video="))?[17..]??"",args.Contains("--tv-check-reload")||args.Contains("--tv-check-prompt-only"),args.FirstOrDefault(a=>a.StartsWith("--tv-check-origin="))?[18..]??"",args.Contains("--tv-check-prompt-only"));
    }
    private TVDiagnostic(TelevisionFeature mod,string output,string save,string video,bool reload,string origin,bool promptOnly)
    {
        Mod=mod;Output=Path.GetFullPath(output);Save=Path.GetFullPath(save);Video=MediaRules.FileName(video,".mp4");Reload=reload;PromptOnly=promptOnly;
        Origin=origin;PreviousOrigin=TVConfig.RelayOrigin.Value;PreviousToken=TVConfig.RelayToken.Value;
        if(Origin.Length>0&&!MediaRules.VideoUrl(Origin).IsLoopback)throw new InvalidOperationException("Diagnostic fixture must use loopback HTTP.");
        var workspace=Path.GetFullPath(@"C:\Users\ortaa\Documents\Projects\ScheduleOne-Mods\artifacts")+Path.DirectorySeparatorChar;
        if(!Output.StartsWith(workspace,StringComparison.OrdinalIgnoreCase)||!Save.StartsWith(workspace,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(Save)!="SaveGame_5")
            throw new InvalidOperationException("TV diagnostic requires an isolated artifacts/SaveGame_5 copy.");
        Directory.CreateDirectory(Output);
        Application.runInBackground=true;
    }
    internal void TryLoad()
    {
        if(Started&&!Done)Time.timeScale=1;
        if(Started||Done||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Menu")return;
        var manager=LoadManager.Instance;if(!manager||manager.IsLoading||LoadManager.SaveGames is null)return;
        if(!LoadManager.TryLoadSaveInfo(Save,5,out var info,true))return;
        Started=true;Deadline=TVNetwork.Now+120;MelonLogger.Msg("TV_CHECK loading isolated save "+Save);manager.StartGame(info,false,false);
    }
    internal void Tick()
    {
        if(Done||!Started||!Mod.Ready||LoadManager.Instance.TimeSinceGameLoaded<4||TVNetwork.Now<Next||SaveManager.Instance.IsSaving)return;
        try
        {
            Check(Path.GetFullPath(LoadManager.Instance.LoadedGameFolderPath).TrimEnd(Path.DirectorySeparatorChar)==Save,"Diagnostic save isolation failed.");
            if(TVNetwork.Now>Deadline)throw new Exception("TV diagnostic deadline expired at phase "+Phase+"; "+(Item?Controller.Status:"not placed"));
            Run();
        }
        catch(Exception error){Done=true;RestoreConfig();File.WriteAllText(Path.Combine(Output,"failure.txt"),"Phase "+Phase+"\n"+error);MelonLogger.Error("TV_CHECK_FAILED "+error);Application.Quit();}
    }
    private void Run()
    {
        switch(Phase)
        {
            case 0:
                Check(TVNetwork.Host,"Native host is not available.");
                TVNetwork.CheckWire(new TVCommand{Action="seek",Screen=Guid.NewGuid().ToString(),Position=23.5});
                TVNetwork.CheckWire(new TVMessage{Type="imageChunk",Hash=new string('a',64),Data=new byte[MediaRules.ChunkBytes]});
                var shops=ShopInterface.AllShops.ToArray().Where(s=>s&&s.GetListing(TVContent.ItemId) is not null).ToArray();
                Check(shops.Length==1&&shops[0].ShopCode=="boutique"&&shops[0].GetListing(TVContent.ItemId).Price==450,"Wall Screen must be sold only at the boutique.");
                if(Reload)
                {
                    Item=Property.Properties.ToArray().SelectMany(p=>p.BuildableItems.ToArray()).First(i=>i&&i.ItemInstance?.ID==TVContent.ItemId).Cast<SurfaceItem>();
                    if(PromptOnly)foreach(var equipment in Item.ParentProperty.BuildableItems.ToArray())
                        if(equipment&&equipment.ItemInstance?.ID!=TVContent.ItemId)equipment.transform.position+=Vector3.up*100;
                    Check(State.Media.Kind=="image"&&Mod.Cache.Has(State.Media.Source)&&!State.Playing,"Image/save state did not reload.");Frame();Phase=20;Next=TVNetwork.Now+2;return;
                }
                BeginPlacement();Phase=28;Next=TVNetwork.Now+1;return;
            case 28:
                var handler=BuildManager.Instance.currentBuildHandler.GetComponent<BuildUpdate_Surface>();
                Check(handler,"Surface placement handler unavailable.");
                if(!handler.validPosition)
                {
                    if(!GhostLogged){GhostLogged=true;ScreenCapture.CaptureScreenshot(Path.Combine(Output,"placement-ghost.png"));MelonLogger.Msg("TV_CHECK invalid ghost: position="+handler.GhostModel.transform.position+" forward="+handler.GhostModel.transform.forward+" expected="+Outward+" surface="+(handler.hoveredValidSurface?handler.hoveredValidSurface.name:"none")+" camera="+PlayerCamera.Instance.transform.position+" target="+PlacementPoint+" mask="+handler.DetectionMask.value+" range="+handler.detectionRange);}
                    if(TVNetwork.Now>Deadline-65)throw new Exception("Placement ghost invalid: position="+handler.GhostModel.transform.position+" forward="+handler.GhostModel.transform.forward+" expected="+Outward+" surface="+(handler.hoveredValidSurface?handler.hoveredValidSurface.name:"none")+" camera="+PlayerCamera.Instance.transform.position+" target="+PlacementPoint);
                    return;
                }
                Check(Vector3.Dot(handler.GhostModel.transform.forward,Outward)>.95f,"Native surface placement faces in an unexpected direction.");
                MelonLogger.Msg("TV_CHECK ghost forward="+handler.GhostModel.transform.forward+" outward="+Outward);
                // Use the position and orientation produced by the actual player placement handler.
                var parent=handler.hoveredValidSurface;
                Item=BuildManager.Instance.CreateSurfaceItem(handler.ItemInstance,parent,parent.GetRelativePosition(handler.GhostModel.transform.position),parent.GetRelativeRotation(handler.GhostModel.transform.rotation),Guid.NewGuid().ToString());
                BuildManager.Instance.StopBuilding();
                Check(Item&&Item.Initialized&&Item.ParentProperty.IsOwned,"Native wall placement failed.");
                Mod.World!.Scan();Frame();Phase=1;Next=TVNetwork.Now+1;return;
            case 1:
                var texture=new Texture2D(64,32,TextureFormat.RGBA32,false);
                for(var y=0;y<32;y++)for(var x=0;x<64;x++)texture.SetPixel(x,y,x<32?new Color(.1f,.65f,.95f):new Color(.95f,.3f,.1f));texture.Apply();
                var bytes=ImageConversion.EncodeToPNG(texture).ToArray();Object.Destroy(texture);Hash=Mod.Cache.Put(bytes);
                Send(new(){Action="select",Media=new(){Kind="image",Source=Hash,Title="TV diagnostic image"}});Phase=2;Next=TVNetwork.Now+1;return;
            case 2:
                Check(Controller.Texture&&Controller.Texture!.width==64,"Image was not displayed on the native furniture.");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"wall-image.png"));
                Phase=29;Next=TVNetwork.Now+.5;return;
            case 29:
                var prompts=TVInteraction.PromptRects().ToArray();
                Check(prompts.Length>0,"TV hover prompt did not appear; hovered="+(Il2CppScheduleOne.Interaction.InteractionManager.Instance.HoveredValidInteractableObject?Il2CppScheduleOne.Interaction.InteractionManager.Instance.HoveredValidInteractableObject.name:"none")+" item="+Item!.transform.position+" colliderActive="+Item.BoundingCollider.gameObject.activeInHierarchy);
                foreach(var prompt in prompts)Check(Math.Abs(prompt.TransformPoint(prompt.rect.center).x-Screen.width*.5f)<1,"TV hover prompt was not horizontally centered.");
                PromptY=prompts[0].position.y;
                PlayerCamera.Instance.OverrideTransform(PlacementPoint+Outward*2.5f,Quaternion.LookRotation(-Outward)*Quaternion.Euler(0,8,0),.1f,false);
                Phase=30;Next=TVNetwork.Now+.5;return;
            case 30:
                var movedPrompts=TVInteraction.PromptRects().ToArray();
                Check(movedPrompts.Length>0,"TV hover prompt disappeared while looking across the screen.");
                foreach(var prompt in movedPrompts)Check(Math.Abs(prompt.TransformPoint(prompt.rect.center).x-Screen.width*.5f)<1,"TV hover prompt moved horizontally with the view.");
                Check(Math.Abs(movedPrompts[0].position.y-PromptY)<2,"Horizontal prompt correction changed vertical placement.");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"centered-prompt.png"));
                Phase=31;Next=TVNetwork.Now+.2;return;
            case 31:
                // CaptureScreenshot completes on a rendered frame, which can lag while unfocused.
                if(!File.Exists(Path.Combine(Output,"centered-prompt.png")))return;
                Frame();
                BeforeLook=PlayerCamera.Instance.CanLook;BeforeMove=PlayerMovement.Instance._canMove;BeforeInventory=PlayerInventory.Instance.HotbarEnabled;BeforeEquipping=PlayerInventory.Instance.EquippingEnabled;
                Item!.GetComponentsInChildren<Il2CppScheduleOne.Interaction.InteractableObject>(true).First().StartInteract();
                Check(Mod.Panel!.IsOpen,"Native interaction did not open the source picker.");
                Phase=3;Next=TVNetwork.Now+1;return;
            case 3:
                Check(Mod.Panel!.IsOpen,"Source picker closed unexpectedly.");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"source-picker.png"));Phase=21;Next=TVNetwork.Now+.5;return;
            case 21:
                Mod.Panel!.Close();
                Check(PlayerCamera.Instance.CanLook==BeforeLook&&PlayerMovement.Instance._canMove==BeforeMove&&PlayerInventory.Instance.HotbarEnabled==BeforeInventory&&PlayerInventory.Instance.EquippingEnabled==BeforeEquipping,"Interaction did not restore game controls.");
                Send(new(){Action="select",Media=new(){Kind="local",Source=Video,Title="TV diagnostic video"}});Phase=4;Next=TVNetwork.Now+1;return;
            case 4:
                if(!Controller.CheckPlaying)return;
                Check(Controller.Texture&&Controller.Texture!.width>=1280,"Video did not preserve source resolution.");PreviousPosition=Controller.CheckPosition;Phase=5;Next=TVNetwork.Now+2;return;
            case 5:
                Check(Controller.CheckPosition>PreviousPosition+1,"Background playback did not advance.");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"wall-video.png"));Phase=32;Next=TVNetwork.Now+.2;return;
            case 32:
                if(!File.Exists(Path.Combine(Output,"wall-video.png")))return;
                Mod.Panel!.Open(Item!.GUID.ToString());Mod.Panel.CheckClick("Play");Phase=6;Next=TVNetwork.Now+1;return;
            case 6:
                Check(!Controller.CheckPlaying,"Pause failed.");PreviousPosition=Controller.CheckPosition;Phase=7;Next=TVNetwork.Now+1;return;
            case 7:
                // A pending drift-correction seek can finish after Pause; verify the settled host
                // position rather than treating that final correction as resumed playback.
                Check(!Controller.CheckPlaying&&Math.Abs(Controller.CheckPosition-State.Target(TVNetwork.Now))<.2,"Paused decoder did not settle at the shared paused position.");
                PreviousPosition=Controller.CheckPosition;Mod.Panel!.CheckClick("Play");Phase=8;Next=TVNetwork.Now+2;return;
            case 8:
                Check(Controller.CheckPlaying&&Controller.CheckPosition>PreviousPosition+1,"Resume failed.");Mod.Panel!.CheckSeek(42,State.Duration);Mod.Panel.CheckVolume(.25f);Phase=9;Next=TVNetwork.Now+2;return;
            case 9:
                Check(Math.Abs(Controller.CheckPosition-State.Target(TVNetwork.Now))<1.2&&Controller.CheckPosition>42,"Seek/timeline correction failed.");
                Check(Math.Abs(State.Volume-.25)<.01,"UI volume control failed.");Mod.Panel!.Close();
                PlayerMovement.Instance.Teleport(Near+Vector3.up*150,true);Phase=10;Next=TVNetwork.Now+1;return;
            case 10:
                Check(!Controller.Texture&&State.Playing,"Distant display did not release decoder while retaining timeline.");
                PlayerMovement.Instance.Teleport(Near,true);Phase=11;Next=TVNetwork.Now+1;return;
            case 11:
                if(!Controller.CheckPlaying)return;Check(Math.Abs(Controller.CheckPosition-State.Target(TVNetwork.Now))<1.2,"Nearby display did not catch up.");
                if(Origin.Length>0){Send(new(){Action="select",Media=new(){Kind="url",Source=Origin+"/video.mp4",Title="HTTP video fixture"}});Phase=13;Next=TVNetwork.Now+1;return;}
                SaveImage();return;
            case 13:
                if(!Controller.CheckPlaying)return;
                Send(new(){Action="seek",Position=30});Phase=14;Next=TVNetwork.Now+2;return;
            case 14:
                Check(Controller.CheckPlaying&&Math.Abs(Controller.CheckPosition-State.Target(TVNetwork.Now))<1.2&&Controller.CheckPosition>30,"HTTP ranged video/seek failed.");
                TVConfig.RelayOrigin.Value=Origin;TVConfig.RelayToken.Value="tv-loopback-fixture";
                Send(new(){Action="select",Media=new(){Kind="relay",Source="episode-1",SourceId="fixture",VersionId="original",Title="Relay fixture",SubtitleMode="none"}});Phase=15;Next=TVNetwork.Now+1;return;
            case 15:
                if(!Controller.CheckPlaying||State.PlaybackUrl.Length==0)return;
                Send(new(){Action="seek",Position=50});Phase=16;Next=TVNetwork.Now+2;return;
            case 16:
                Check(Controller.CheckPlaying&&Math.Abs(Controller.CheckPosition-State.Target(TVNetwork.Now))<1.2&&Controller.CheckPosition>50,"Relay descriptor did not produce synchronized native playback.");
                PreviousPosition=State.Target(TVNetwork.Now);Send(new(){Action="retry"});Phase=17;Next=TVNetwork.Now+2;return;
            case 17:
                if(!Controller.CheckPlaying||!State.PlaybackUrl.Contains("renewed="))return;
                Check(State.Target(TVNetwork.Now)>=PreviousPosition&&Math.Abs(Controller.CheckPosition-State.Target(TVNetwork.Now))<1.2,"Relay refresh lost playback position.");
                Mod.Panel!.Open(Item!.GUID.ToString());Mod.Panel.CheckTab("relay");Phase=18;Next=TVNetwork.Now+1;return;
            case 18:
                Check(Mod.Panel!.IsOpen,"Relay source picker did not stay open.");ScreenCapture.CaptureScreenshot(Path.Combine(Output,"relay-picker.png"));Mod.Panel.CheckSearch();Phase=22;Next=TVNetwork.Now+.5;return;
            case 19:
                Mod.Panel!.Close();SaveImage();return;
            case 22:
                if(Mod.Panel!.CheckRowCount!=1||!Mod.Panel.CheckHeading.StartsWith("Relay search:"))return;
                Mod.Panel.CheckRow(0);Phase=23;Next=TVNetwork.Now+.2;return;
            case 23:
                if(!Mod.Panel!.CheckHeading.EndsWith("· seasons"))return;
                Mod.Panel.CheckRow(0);Phase=24;Next=TVNetwork.Now+.2;return;
            case 24:
                if(!Mod.Panel!.CheckHeading.EndsWith("· season 1"))return;
                Mod.Panel.CheckRow(0);Phase=25;Next=TVNetwork.Now+.2;return;
            case 25:
                if(!Mod.Panel!.CheckHeading.EndsWith("· sources"))return;
                Mod.Panel.CheckRow(0);Phase=26;Next=TVNetwork.Now+.2;return;
            case 26:
                if(!Mod.Panel!.CheckHeading.EndsWith("· versions"))return;
                Mod.Panel.CheckRow(0);Phase=27;Next=TVNetwork.Now+1;return;
            case 27:
                if(!Controller.CheckPlaying||State.Media.SubtitleMode!="burned"||!State.Media.Title.StartsWith("Loopback TV fixture"))return;
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"relay-catalog.png"));Phase=19;Next=TVNetwork.Now+.5;return;
            case 12:
                var stateFile=Path.Combine(Save,"Modded","PersonalTV","Screens.json");Check(File.Exists(stateFile),"TV save was not written.");
                var saved=TVDocument.Load(File.ReadAllText(stateFile),TVNetwork.Now);Check(saved.Screens.Any(s=>s.Id==State.Id&&s.Media.Source==Hash)&&saved.Favorites.Any(f=>f.Source==Hash),"Saved image/favorite missing.");
                Check(File.Exists(Path.Combine(Save,"Modded","PersonalTV","Images",Hash+".png")),"Saved image asset missing.");
                Pass("Native wall furniture, shop, interaction/control restoration, image display, full-resolution video, background playback, pause/resume, seek, distance suspension/catch-up and save passed."+(Origin.Length>0?" Ranged HTTP MP4, Relay preparation/polling, playback and refresh also passed against the loopback fixture.":""));return;
            case 20:
                Check(Controller.Texture&&Controller.Texture!.width==64,"Saved image failed to display after reload.");
                if(PromptOnly){TVInteraction.Reset();Phase=34;Next=TVNetwork.Now+.15;return;}
                Check(Mod.Document.Favorites.Any(f=>f.Source==State.Media.Source),"Favorite failed to reload.");ScreenCapture.CaptureScreenshot(Path.Combine(Output,"reload-image.png"));Phase=33;Next=TVNetwork.Now+.2;return;
            case 34:
                Check(PromptOpacity()>.9f,"TV hint was not initially visible.");Phase=35;Next=TVNetwork.Now+1.1;return;
            case 35:
                Check(PromptOpacity()<.01f,"TV hint did not disappear after one second.");
                PlayerMovement.Instance.Teleport(Near+Outward*.1f,true);
                PlayerCamera.Instance.OverrideTransform(PlacementPoint+Outward*2.5f,Quaternion.LookRotation(-Outward)*Quaternion.Euler(0,8,0),.1f,false);
                Phase=36;Next=TVNetwork.Now+.3;return;
            case 36:
                Check(PromptOpacity()<.01f,"Moving around the screen restarted the hint.");
                Item!.GetComponentsInChildren<Il2CppScheduleOne.Interaction.InteractableObject>(true).First().StartInteract();
                Check(Mod.Panel!.IsOpen,"The hidden hint disabled native interaction.");Mod.Panel.Close();
                PlayerCamera.Instance.OverrideTransform(PlacementPoint+Outward*2.5f,Quaternion.LookRotation(Outward),.1f,false);
                Phase=37;Next=TVNetwork.Now+.7;return;
            case 37:
                Frame();Phase=38;Next=TVNetwork.Now+.2;return;
            case 38:
                Check(PromptOpacity()>.8f,"Looking away and back did not restore the brief hint.");
                Pass("Native TV hint appears briefly, hides after one second, stays hidden during movement, retains native interaction, and reappears after looking away and back.");return;
            case 33:
                if(!File.Exists(Path.Combine(Output,"reload-image.png")))return;
                Pass("Native wall furniture and selected image/favorite reloaded from the isolated save.");return;
        }
    }
    private void BeginPlacement()
    {
        var surfaces=Object.FindObjectsOfType<Surface>(true).ToArray().Where(s=>s&&s.ParentProperty&&s.ParentProperty.IsOwned&&s.ParentProperty.PropertyName=="Motel Room"&&s.SurfaceType==Surface.ESurfaceType.Wall);
        var wall=surfaces.SelectMany(s=>s.GetComponentsInChildren<Collider>(true).Select(c=>(Surface:s,Collider:c)))
            .Where(w=>w.Collider.bounds.size.y>2&&Math.Max(w.Collider.bounds.size.x,w.Collider.bounds.size.z)>3)
            .OrderByDescending(w=>Math.Max(w.Collider.bounds.size.x,w.Collider.bounds.size.z)*w.Collider.bounds.size.y).First();
        var surface=wall.Surface;var collider=wall.Collider;
        // Clear only the isolated copy's placed equipment so storage prompts cannot block this test.
        foreach(var equipment in surface.ParentProperty.BuildableItems.ToArray())
            if(equipment&&equipment.ItemInstance?.ID!=TVContent.ItemId)equipment.transform.position+=Vector3.up*100;
        var bounds=collider.bounds;var center=bounds.center;var spawn=surface.ParentProperty.InteriorSpawnPoint.position;
        var axis=bounds.size.x<bounds.size.z?Vector3.right:Vector3.forward;
        Outward=Vector3.Dot(spawn-center,axis)>=0?axis:-axis;
        PlacementPoint=center+Outward*((bounds.size.x<bounds.size.z?bounds.extents.x:bounds.extents.z)+.08f);PlacementPoint.y=Math.Clamp(spawn.y+1.1f,bounds.min.y+.85f,bounds.max.y-.85f);
        MelonLogger.Msg("TV_CHECK wall="+surface.name+" bounds="+bounds+" point="+PlacementPoint+" surfacePointValid="+surface.IsPointValid(PlacementPoint,collider));
        Near=PlacementPoint+Outward*2.5f;Near.y=PlacementPoint.y-1.8f;
        PlayerMovement.Instance.Teleport(Near,true);PlayerCamera.Instance.OverrideTransform(PlacementPoint+Outward*2.5f,Quaternion.LookRotation(-Outward),.1f,false);
        BuildManager.Instance.StartBuilding(Registry.GetItem(TVContent.ItemId).GetDefaultInstance(1).Cast<ItemInstance>());
        PlayerCamera.Instance.CanLook=false;
    }
    private void Frame()
    {
        var target=Item!.transform.position;PlacementPoint=target;Outward=Item.transform.forward;Near=target+Outward*2.5f;Near.y=target.y-1.8f;
        PlayerMovement.Instance.Teleport(Near,true);PlayerCamera.Instance.OverrideTransform(target+Outward*2.5f,Quaternion.LookRotation(-Outward),.1f,false);
    }
    private void Send(TVCommand command){command.Screen=Item!.GUID.ToString();TVNetwork.Submit(command);}
    private static float PromptOpacity()
    {
        var prompts=TVInteraction.PromptRects().ToArray();
        Check(prompts.Length>0,"Native TV prompt is missing.");
        return prompts.Min(p=>{var group=p.GetComponent<CanvasGroup>();return group?group.alpha:1;});
    }
    private void SaveImage(){Send(new(){Action="select",Media=new(){Kind="image",Source=Hash,Title="TV diagnostic image"}});Send(new(){Action="favorite"});Check(State.Remember,"Image not remembered automatically.");SaveManager.Instance.Save(Save);Phase=12;Next=TVNetwork.Now+3;}
    internal void RestoreConfig(){TVConfig.RelayOrigin.Value=PreviousOrigin;TVConfig.RelayToken.Value=PreviousToken;}
    private void Pass(string message){Done=true;RestoreConfig();File.WriteAllText(Path.Combine(Output,Reload?"reload-passed.txt":"result.txt"),message+"\nTwo-client multiplayer and live Relay require separate tests.\n");MelonLogger.Msg("TV_CHECK_PASSED "+message);Application.Quit();}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
