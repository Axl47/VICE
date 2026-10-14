using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Building;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Amenities;

// Opt-in native diagnostics. Loader isolation and save guards are installed before a load can start.
internal sealed class HomeGamesDiagnostic
{
    private static HomeGamesDiagnostic? Current;
    private static bool Requested;
    private readonly AmenitiesFeature Feature;
    private readonly string Output,Save,Profile;
    private readonly List<string> Checks=new();
    private int Phase,Session;
    private double Next,Deadline;
    private string PendingScreenshot="";
    private BuildableItem? Pool,Board;
    private AmenityCameraRig? TestRig;
    private Controls? Before;
    private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed record Controls(bool Main,bool Overlay,bool Look,bool Move,bool Inventory,bool Equipping,bool Typing,bool CursorVisible,CursorLockMode CursorLock,bool CrosshairActive,bool CrosshairEnabled,float Fov,Vector3 Position,Quaternion Rotation);
    internal static HomeGamesDiagnostic? Create(AmenitiesFeature feature,HarmonyLib.Harmony harmony)
    {
        var args=Environment.GetCommandLineArgs();var root=args.FirstOrDefault(a=>a.StartsWith("--home-games-check="))?[19..];
        if(root is null)return null;
        Requested=true;
        try
        {
        var check=new HomeGamesDiagnostic(feature,root);Current=check;
        check.Check(ModSupport.Patch(harmony,typeof(LoadManager),"StartGame",typeof(HomeGamesDiagnostic),prefix:nameof(GuardLoad)),"Installed load guard");
        check.Check(ModSupport.Patch(harmony,typeof(SaveManager),"Save",typeof(HomeGamesDiagnostic),prefix:nameof(GuardSave),parameters:new[]{typeof(string)}),"Installed explicit save guard");
        check.Check(ModSupport.Patch(harmony,typeof(SaveManager),"Save",typeof(HomeGamesDiagnostic),prefix:nameof(GuardAutoSave),parameters:Type.EmptyTypes),"Installed autosave guard");
        check.Check(ModSupport.Patch(harmony,typeof(SaveManager),"CreateSaveBackup",typeof(HomeGamesDiagnostic),prefix:nameof(NoBackup)),"Installed backup guard");
        Application.runInBackground=true;return check;
        }
        catch(Exception error){MelonLogger.Error("Native diagnostic initialization failed; refusing to run: "+error);Application.Quit();throw;}
    }
    private HomeGamesDiagnostic(AmenitiesFeature feature,string root)
    {
        Feature=feature;root=Path.GetFullPath(root);
        var artifacts=Path.GetFullPath(@"C:\Users\ortaa\Documents\Projects\ScheduleOne-Mods\artifacts")+Path.DirectorySeparatorChar;
        if(!root.StartsWith(artifacts,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Native check must live under workspace artifacts.");
        Output=Path.Combine(root,"Evidence");Save=Path.Combine(root,"CopiedSave","SaveGame_5");Profile=Path.Combine(root,"Profile");
        Directory.CreateDirectory(Output);
        Check(Directory.Exists(Save),"Save 5 copy exists");
        foreach(var pair in new[]{(MelonEnvironment.MelonBaseDirectory,Profile),(MelonEnvironment.ModsDirectory,Path.Combine(Profile,"Mods")),(MelonEnvironment.UserLibsDirectory,Path.Combine(Profile,"UserLibs")),(MelonEnvironment.UserDataDirectory,Path.Combine(Profile,"UserData"))})
            Check(SamePath(pair.Item1,pair.Item2),"Isolated loader path: "+Path.GetFileName(pair.Item2));
        File.WriteAllText(Path.Combine(Output,"loader-paths.json"),JsonSerializer.Serialize(new{MelonEnvironment.MelonBaseDirectory,MelonEnvironment.ModsDirectory,MelonEnvironment.UserLibsDirectory,MelonEnvironment.UserDataDirectory,Copy=Save},new JsonSerializerOptions{WriteIndented=true}));
        Deadline=Time.realtimeSinceStartupAsDouble+240;
    }
    private static bool SamePath(string? a,string b)=>!string.IsNullOrEmpty(a)&&Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase);
    private static bool GuardLoad(SaveInfo __0)=>!Requested||Current is not null&&__0 is not null&&SamePath(__0.SavePath,Current.Save);
    internal static bool SaveAllowed(string path)=>!Requested||Current is not null&&SamePath(path,Current.Save);
    private static bool GuardSave(string __0)=>SaveAllowed(__0);
    private static bool GuardAutoSave()=>!Requested||Current is not null&&LoadManager.Instance&&SamePath(LoadManager.Instance.LoadedGameFolderPath,Current.Save);
    private static bool NoBackup()=>!Requested;
    private void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Checks.Add(message);MelonLogger.Msg("HOME_GAMES_NATIVE_PASS "+message);}
    internal void Tick()
    {
        if(Phase==99)return;
        try
        {
            var now=Time.realtimeSinceStartupAsDouble;
            if(now>Deadline)throw new TimeoutException("Native check timed out at phase "+Phase);
            if(PendingScreenshot.Length>0){if(!File.Exists(PendingScreenshot))return;PendingScreenshot="";}
            var load=LoadManager.Instance;if(!load||now<Next)return;
            if(Phase==0&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Menu"&&!load.IsLoading&&LoadManager.SaveGames is not null)
            {
                Check(LoadManager.TryLoadSaveInfo(Save,5,out var info,true),"Copied save is readable");
                Check(GuardLoad(info)&&!GuardLoad(null!)&&!GuardSave(Path.Combine(Output,"forbidden-save")),"Load/save guard rejects non-copy targets");
                Phase=1;load.StartGame(info,false,false);return;
            }
            if(load.IsGameLoaded&&!load.IsLoading)CheckIsolation();
            if(!Feature.Ready||!load.IsGameLoaded||load.IsLoading||load.TimeSinceGameLoaded<6||!Player.Local||!Player.Local.PlayerLoaded)return;
            if(Phase==1)
            {
                Check(AmenityNetwork.Host,"Native host active");
                if(Session==1){Check(!Object.FindObjectsOfType<Camera>(true).ToArray().Any(c=>c.name=="VICE home-games world camera"),"Scene reload leaves no owned camera");Finish();return;}
                Place();Phase=2;Next=now+1;return;
            }
            var panel=Feature.Panel!;
            switch(Phase)
            {
                case 2:
                    Near(Pool!);Before=Capture();panel.Open(Pool!.GUID.ToString());Check(IsOpen(panel),"Pool world panel opens");
                    Check(!PlayerCamera.Instance.Camera.enabled&&!PlayerMovement.Instance._canMove&&!PlayerCamera.Instance.CanLook&&!PlayerInventory.Instance.HotbarEnabled,"Native camera, movement and inventory leased");
                    Check(EventSystem.current&&EventSystem.current.currentSelectedGameObject,"Menu has keyboard selection");
                    Click(panel,"Solo practice");Check(Feature.States[Pool.GUID.ToString()].Solo,"Native menu chooses solo seat");Phase=3;Next=now+.5;return;
                case 3:
                    Screenshot("pool-angled");CheckView(panel);Phase=4;Next=now+.5;return;
                case 4:
                    Options(panel,true);Click(panel,"Angled / overhead");Phase=5;Next=now+.5;return;
                case 5:
                    Screenshot("pool-overhead");Phase=50;return;
                case 50:
                    var rig=Field<AmenityCameraRig>(panel,"Rig");Check(rig.View.orthographic,"Native overhead is orthographic");CheckView(panel);
                    var state=Feature.States[Pool!.GUID.ToString()];Command(state,"charge",0);Check(state.Charging==AmenityNetwork.LocalActor,"Native host accepts charge");
                    Options(panel,true);Check(state.Charging<0&&!state.Moving,"Opening options cancels charge without shot");Options(panel,false);
                    state.Pool.BallInHand=true;state.Pool.Simulation.Balls.Single(b=>b.Number==0).Pocketed=true;
                    Command(state,"place",.12,-.65);Check(!state.Pool.BallInHand&&!state.Pool.Simulation.Balls.Single(b=>b.Number==0).Pocketed,"Native host ball-in-hand placement accepted");
                    Command(state,"charge",0);Phase=51;Next=now+.4;return;
                case 51:
                    var poolView=Feature.Views[Pool!.GUID.ToString()];var cue=Field<GameObject>(poolView,"Cue");var cueState=Feature.States[Pool.GUID.ToString()];var cueBall=cueState.Pool.Simulation.Balls.Single(b=>b.Number==0);
                    Check(cue.activeSelf&&Math.Abs(cue.transform.localPosition.y-HomeGameVisualContract.BallCentreY)<.0001&&Math.Abs(cue.transform.localPosition.x-cueBall.X)<.0001&&cue.transform.localPosition.z<cueBall.Z,"Native cue aligns with charged ball and shot axis");
                    Screenshot("pool-charge");Phase=52;return;
                case 52:
                    var shot=Feature.States[Pool!.GUID.ToString()];Command(shot,"release");Check(shot.Moving,"Native host pool shot starts fixed simulation");Phase=53;Next=now+.3;return;
                case 53:
                    var moving=Feature.Views[Pool!.GUID.ToString()];var ballMesh=Field<Dictionary<int,GameObject>>(moving,"Balls")[0];
                    Check(Quaternion.Angle(ballMesh.transform.localRotation,Quaternion.identity)>.1,"Native moving ball has rolling presentation");Screenshot("pool-rolling");Phase=54;return;
                case 54:
                    panel.Close();CheckRestored(Before!);Phase=6;Next=now+.5;return;
                case 6:
                    Check(Feature.Views[Board!.GUID.ToString()].BoardArtwork==HomeGameAssets.Enabled,"Native board frame matches artwork/fallback mode");
                    Near(Board!);Before=Capture();Board!.GetComponentsInChildren<Il2CppScheduleOne.Interaction.InteractableObject>(true).First(i=>i.gameObject.activeInHierarchy).StartInteract();
                    Check(IsOpen(panel),"Native darts furniture interaction opens panel");Click(panel,"Solo practice");Phase=7;Next=now+.5;return;
                case 7:
                    Screenshot("darts-world");CheckView(panel);var darts=Feature.States[Board!.GUID.ToString()];Command(darts,"charge",0,.12);Phase=8;Next=now+.45;return;
                case 8:
                    if(!HomeGameAssets.Enabled)CheckPrimitivePixels(panel);
                    var thrown=Feature.States[Board!.GUID.ToString()];Command(thrown,"release");Check(thrown.Darts.Throws==1,"Native timed throw reaches rules");Phase=9;Next=now+.5;return;
                case 9:
                    var hit=Feature.States[Board!.GUID.ToString()];Check(DartsRules.Score(hit.Darts.LastX,hit.Darts.LastY).Number==20,"Native timed top aim scores 20");Command(hit,"charge",.12,0);Phase=81;Next=now+.45;return;
                case 81:
                    Command(Feature.States[Board!.GUID.ToString()],"release");Phase=82;Next=now+.4;return;
                case 82:
                    var right=Feature.States[Board!.GUID.ToString()];Check(DartsRules.Score(right.Darts.LastX,right.Darts.LastY).Number==6,"Native timed right aim scores 6");Command(right,"charge",-.12,0);Phase=83;Next=now+.45;return;
                case 83:
                    Command(Feature.States[Board!.GUID.ToString()],"release");Phase=84;Next=now+.4;return;
                case 84:
                    var left=Feature.States[Board!.GUID.ToString()];Check(DartsRules.Score(left.Darts.LastX,left.Darts.LastY).Number==11,"Native timed left aim scores 11");
                    var boardView=Feature.Views[Board.GUID.ToString()];var expected=HomeGameVisualContract.Dart(left.Darts.LastX,left.Darts.LastY,boardView.BoardArtwork);var handle=Field<GameObject[]>(boardView,"Darts")[2];
                    Check(Vector3.Distance(handle.transform.localPosition,new Vector3((float)expected.X,(float)expected.Y,(float)expected.Z+.08f))<.0001,"Native settled dart handle preserves hit mapping");
                    Screenshot("darts-impact");Phase=90;return;
                case 90:
                    panel.Close();CheckRestored(Before!);
                    // Reject entry without changing native state when another UI owns it.
                    PlayerCamera.Instance.AddActiveUIElement("VICE diagnostic other UI");var blocked=Capture();panel.Open(Board!.GUID.ToString());Check(!IsOpen(panel),"Other UI blocks entry");CheckRestored(blocked);PlayerCamera.Instance.RemoveActiveUIElement("VICE diagnostic other UI");
                    Before=Capture();panel.Open(Board.GUID.ToString());PlayerCamera.Instance.AddActiveUIElement("VICE diagnostic takeover");Phase=10;Next=now+.3;return;
                case 10:
                    Check(!IsOpen(panel),"Other UI takeover closes world play");Check(!PlayerCamera.Instance.CanLook,"Controls remain leased while takeover owns UI");
                    PlayerCamera.Instance.RemoveActiveUIElement("VICE diagnostic takeover");Phase=11;Next=now+.3;return;
                case 11:
                    CheckRestored(Before!);Before=Capture();TestRig=new("darts");PlayerCamera.Instance.OverrideFOV(Before.Fov+5,.1f);TestRig.Dispose();
                    Check(!PlayerCamera.Instance.CanLook,"Native FOV override defers control restoration");PlayerCamera.Instance.StopFOVOverride(.1f);Phase=12;Next=now+.5;return;
                case 12:
                    CheckRestored(Before!);Before=Capture();TestRig=new("darts");var native=PlayerCamera.Instance.Camera;PlayerCamera.Instance.Camera=null!;
                    Check(!TestRig.Valid,"Null replacement camera invalidates view safely");TestRig.Dispose();Check(!PlayerCamera.Instance.ActiveUIElements.Contains("VICEHomeGames"),"UI lease releases with null camera");PlayerCamera.Instance.Camera=native;
                    Phase=13;Next=now+.3;return;
                case 13:
                    CheckRestored(Before!);Near(Board!);Before=Capture();panel.Open(Board!.GUID.ToString());Board.gameObject.SetActive(false);Phase=14;Next=now+.3;return;
                case 14:
                    // CanUse depends on native destruction/placement state, not GameObject activation alone.
                    Board!.gameObject.SetActive(true);Object.Destroy(Board.gameObject);Phase=15;Next=now+.8;return;
                case 15:
                    Check(!IsOpen(panel),"Furniture removal closes world play");CheckRestored(Before!);Near(Pool!);panel.Open(Pool!.GUID.ToString());
                    Session=1;Phase=0;load.ExitToMenu(null,null,false);Next=now+1;return;
            }
        }
        catch(Exception error){var failedPhase=Phase;Phase=99;try{Feature.Panel?.Close();TestRig?.Dispose();}catch{}File.WriteAllText(Path.Combine(Output,"failure.txt"),"Phase "+failedPhase+" failed\n"+error+"\n"+string.Join("\n",Checks));MelonLogger.Error("HOME_GAMES_NATIVE_FAILED "+error);Application.Quit();}
    }
    private void CheckIsolation(){if(!SamePath(LoadManager.Instance.LoadedGameFolderPath,Save))throw new InvalidOperationException("Loaded path left copied save.");}
    private void Place()
    {
        var property=Property.OwnedProperties.ToArray().Where(p=>p&&p.Grids.Count>0).OrderByDescending(p=>p.Grids.ToArray().Max(g=>g.Width*g.Height)).First();
        var grid=property.Grids.ToArray().OrderByDescending(g=>g.Width*g.Height).First();
        var tiles=grid.Tiles.ToArray();var gridCenter=tiles.Aggregate(Vector3.zero,(sum,t)=>sum+t.transform.position)/tiles.Length;
        var tile=tiles.Where(t=>t&&t.CanBeBuiltOn()).OrderBy(t=>Vector3.Distance(t.transform.position,gridCenter)).First();
        Pool=BuildManager.Instance.CreateGridItem(Registry.GetItem(AmenityContent.PoolId).GetDefaultInstance(1).Cast<ItemInstance>(),grid,new Vector2(tile.x,tile.y),0,Guid.NewGuid().ToString(),null);
        Check(Pool&&Pool.Initialized&&Pool.ParentProperty.IsOwned,"Native pool furniture created in owned property");
        var wall=Object.FindObjectsOfType<Surface>(true).ToArray().Where(s=>s&&s.ParentProperty&&s.ParentProperty.IsOwned&&s.ParentProperty.PropertyName=="Motel Room"&&s.SurfaceType==Surface.ESurfaceType.Wall)
            .SelectMany(s=>s.GetComponentsInChildren<Collider>(true).Select(c=>(Surface:s,Collider:c)))
            .Where(w=>w.Collider.bounds.size.y>2&&Math.Max(w.Collider.bounds.size.x,w.Collider.bounds.size.z)>3)
            .OrderByDescending(w=>Math.Max(w.Collider.bounds.size.x,w.Collider.bounds.size.z)*w.Collider.bounds.size.y).First();
        var surface=wall.Surface;var bounds=wall.Collider.bounds;var spawn=surface.ParentProperty.InteriorSpawnPoint.position;
        foreach(var equipment in surface.ParentProperty.BuildableItems.ToArray())if(equipment)equipment.transform.position+=Vector3.up*100;
        var center=bounds.center;var axis=bounds.size.x<bounds.size.z?Vector3.right:Vector3.forward;
        var outward=Vector3.Dot(spawn-center,axis)>=0?axis:-axis;
        var point=center+outward*((bounds.size.x<bounds.size.z?bounds.extents.x:bounds.extents.z)+.08f);point.y=Math.Clamp(spawn.y+1.1f,bounds.min.y+.85f,bounds.max.y-.85f);
        Board=BuildManager.Instance.CreateSurfaceItem(Registry.GetItem(AmenityContent.DartsId).GetDefaultInstance(1).Cast<ItemInstance>(),surface,surface.GetRelativePosition(point),surface.GetRelativeRotation(Quaternion.LookRotation(outward)),Guid.NewGuid().ToString());
        Check(Board&&Board.Initialized&&Board.ParentProperty.IsOwned,"Native wall darts furniture created");
        foreach(var id in new[]{AmenityContent.PoolId,AmenityContent.DartsId})Check(Registry.GetItem(id).Icon&&Registry.GetItem(id).Icon.texture.width==512,"Embedded model inventory icon: "+id);
    }
    private void Near(BuildableItem item){var pos=item.transform.position;pos+=item.ItemInstance.ID==AmenityContent.DartsId?item.transform.forward*2.5f:Vector3.right*2;PlayerMovement.Instance.Teleport(pos,true);}
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags)!.GetValue(owner)!;
    private static bool IsOpen(AmenityPanel panel)=>Field<bool>(panel,"Opened");
    private static void Options(AmenityPanel panel,bool open)=>typeof(AmenityPanel).GetMethod("ShowOptions",Flags)!.Invoke(panel,new object[]{open});
    private void Click(AmenityPanel panel,string title)
    {var root=Field<GameObject>(panel,"Root");var button=root.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>().text==title);button.onClick.Invoke();}
    private void Command(AmenitySession state,string action,double x=0,double y=0)=>Feature.Execute(Player.Local,new(){Table=state.Id,Instance=state.Instance,Revision=state.Revision,Action=action,X=x,Y=y});
    private void CheckView(AmenityPanel panel)
    {
        var rig=Field<AmenityCameraRig>(panel,"Rig");Check(rig.Valid,"Owned native view remains valid");Check(Math.Abs(rig.View.rect.y-.18f)<.001&&Math.Abs(rig.View.rect.height-.82f)<.001,"Native viewport reserves lower score band");
        Check(!Field<Il2CppTMPro.TextMeshProUGUI>(panel,"Feedback").text.Contains("Choose a seat"),"Seated world HUD shows current game feedback");
        Check(rig.View.targetTexture is null,"Native world view renders to screen");
        var c=PlayerCamera.Instance.Camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();var d=rig.View.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        Check(c&&d&&c.antialiasing==d.antialiasing&&c.antialiasingQuality==d.antialiasingQuality,"Native antialiasing inherited");
        var visual=Feature.Views[Field<string>(panel,"Table")].Visual;var point=visual.TransformPoint(Field<string>(panel,"Kind")=="darts"?new Vector3(-.12f,0,.055f):new Vector3(.2f,.78f,.45f));
        var screen=rig.View.WorldToScreenPoint(point);var ray=rig.View.ScreenPointToRay(screen);var plane=new Plane(Field<string>(panel,"Kind")=="darts"?visual.forward:visual.up,point);
        var intersects=plane.Raycast(ray,out var distance);var error=Vector3.Distance(ray.GetPoint(distance),point);
        File.WriteAllText(Path.Combine(Output,"projection-"+Phase+".json"),JsonSerializer.Serialize(new{Point=point.ToString(),Screen=screen.ToString(),RayOrigin=ray.origin.ToString(),RayDirection=ray.direction.ToString(),intersects,distance,ErrorMeters=error}));
        Check(intersects&&error<.002f,"Native world/screen aim ray round trip within 2mm: "+error);
        if(Field<string>(panel,"Kind")=="pool")
            foreach(var x in new[]{-.7f,.7f})foreach(var z in new[]{-1.26f,1.26f})
            {var corner=rig.View.WorldToViewportPoint(visual.TransformPoint(new Vector3(x,.85f,z)));Check(corner.z>0&&corner.x>0&&corner.x<1&&corner.y>0&&corner.y<1,"Pool rail corner fits world viewport: "+corner);}
        File.WriteAllText(Path.Combine(Output,"viewport-"+Phase+".json"),JsonSerializer.Serialize(new{Screen.width,Screen.height,PixelRect=rig.View.pixelRect.ToString(),rig.View.aspect,rig.View.fieldOfView,rig.View.orthographic,AA=d.antialiasing.ToString()}));
    }
    private static Controls Capture()
    {var c=PlayerCamera.Instance;var cross=Il2CppScheduleOne.UI.HUD.Instance.crosshair;return new(c.Camera.enabled,c.OverlayCamera&&c.OverlayCamera.enabled,c.CanLook,PlayerMovement.Instance._canMove,PlayerInventory.Instance.HotbarEnabled,PlayerInventory.Instance.EquippingEnabled,GameInput.IsTyping,Cursor.visible,Cursor.lockState,cross.gameObject.activeSelf,cross.enabled,c.Camera.fieldOfView,c.Camera.transform.localPosition,c.Camera.transform.localRotation);}
    private void CheckRestored(Controls before)
    {
        var after=Capture();Check(after.Main==before.Main&&after.Overlay==before.Overlay&&after.Look==before.Look&&after.Move==before.Move&&after.Inventory==before.Inventory&&after.Equipping==before.Equipping&&after.Typing==before.Typing&&after.CursorVisible==before.CursorVisible&&after.CursorLock==before.CursorLock&&after.CrosshairActive==before.CrosshairActive&&after.CrosshairEnabled==before.CrosshairEnabled&&Math.Abs(after.Fov-before.Fov)<.001,"Native camera/control/cursor/crosshair flags restored exactly");
        var displacement=Vector3.Distance(before.Position,after.Position);var angle=Quaternion.Angle(before.Rotation,after.Rotation);
        File.WriteAllText(Path.Combine(Output,"restored-pose-"+Phase+".json"),JsonSerializer.Serialize(new{LocalDisplacementMeters=displacement,LocalRotationDegrees=angle}));
        Check(displacement<.02f&&angle<.1f,"Native local camera pose remains within 2cm / 0.1 degree: "+displacement+"m, "+angle+"deg");
    }
    private void Screenshot(string name){PendingScreenshot=Path.Combine(Output,name+".png");ScreenCapture.CaptureScreenshot(PendingScreenshot);}
    private void CheckPrimitivePixels(AmenityPanel panel)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            var pixels=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(File.ReadAllBytes(Path.Combine(Output,"darts-world.png")));
            Check(ImageConversion.LoadImage(texture,pixels,false),"Native fallback screenshot decodes");
            var rig=Field<AmenityCameraRig>(panel,"Rig");var visual=Feature.Views[Board!.GUID.ToString()].Visual;
            Color Pixel(double x,double y)
            {var screen=rig.View.WorldToScreenPoint(visual.TransformPoint(new((float)x,(float)y,.055f)));return texture.GetPixel(Math.Clamp((int)screen.x,0,texture.width-1),Math.Clamp((int)screen.y,0,texture.height-1));}
            static float Bright(Color c)=>(c.r+c.g+c.b)/3;
            var white=Pixel(Math.Sin(Math.PI/10)*.12,Math.Cos(Math.PI/10)*.12);var black=Pixel(0,.14);
            Check(Bright(white)>.35f&&Bright(white)-Bright(black)>.2f,"Native fallback pixels show contrasting scoring sectors");
            var minimum=int.MaxValue;
            for(var wedge=0;wedge<20;wedge++)
            {
                var angle=wedge*Math.PI/10;var screen=rig.View.WorldToScreenPoint(visual.TransformPoint(new((float)(Math.Sin(angle)*.194),(float)(Math.Cos(angle)*.194),.055f)));var bright=0;
                for(var dy=-18;dy<=18;dy++)for(var dx=-24;dx<=24;dx++)if(Bright(texture.GetPixel(Math.Clamp((int)screen.x+dx,0,texture.width-1),Math.Clamp((int)screen.y+dy,0,texture.height-1)))>.6f)bright++;
                minimum=Math.Min(minimum,bright);
            }
            Check(minimum>8,"Native fallback screenshot has visible labels in all 20 sectors: min bright pixels="+minimum);
            File.WriteAllText(Path.Combine(Output,"fallback-pixels.json"),JsonSerializer.Serialize(new{White=Bright(white),Black=Bright(black),MinimumLabelBrightPixels=minimum}));
        }
        finally{Object.Destroy(texture);}
    }
    private void Finish(){Phase=99;File.WriteAllText(Path.Combine(Output,"passed.txt"),string.Join("\n",Checks)+"\nSingle native host, copied save 5. Programmatic UI interactions; no two-player/network playtest claim.\n");MelonLogger.Msg("HOME_GAMES_NATIVE_COMPLETE checks="+Checks.Count);Application.Quit();}
}
