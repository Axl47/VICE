using Il2CppInterop.Runtime;
using Il2CppScheduleOne;
using Il2CppScheduleOne.PlayerScripts;
using PauseMenu=Il2CppScheduleOne.UI.PauseMenu;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VICE.Lifestyle.Television;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Amenities;

// Menus choose seats/options; a real camera and local world guides handle play.
internal sealed class AmenityPanel : IDisposable
{
    private readonly AmenitiesFeature Feature;
    private readonly GameObject Root;
    private readonly RectTransform Dock,Menu,Help;
    private readonly TextMeshProUGUI Title,Status,Feedback,Message,Gauge;
    private readonly Button Solo;
    private readonly Button ViewOption;
    private readonly GameInput.ExitDelegate ExitHandler;
    private AmenityCameraRig? Rig;
    private GameObject? Guides,Target;
    private LineRenderer? AimLine;
    private string Table="",Kind="";
    private bool Opened,Holding,Options,KeyboardAim,BoardArtwork,WasParticipant;
    private double HoldStarted,NextHeartbeat,InputAfter,AimX,AimY;
    private AmenitySession? LastState;
    private bool Focused=true;
    private string NoticeText="";
    private double NoticeUntil;

    internal AmenityPanel(AmenitiesFeature feature)
    {
        Feature=feature;Root=new("VICE home-games options",new[]{Il2CppType.Of<RectTransform>()});
        var canvas=Root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=95;
        var scaler=Root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);
        Root.AddComponent<GraphicRaycaster>();
        var backing=TVWidgets.Rect(Root.transform,"Reserved camera viewport backing",0,0,0,0);
        backing.anchorMin=Vector2.zero;backing.anchorMax=new(1,.18f);backing.offsetMin=backing.offsetMax=Vector2.zero;
        backing.gameObject.AddComponent<Image>().color=new(.025f,.045f,.055f,1);
        Dock=TVWidgets.Rect(Root.transform,"Score and controls",0,0,1560,154);Dock.anchorMin=Dock.anchorMax=Dock.pivot=new(.5f,0);Dock.anchoredPosition=new(0,18);
        Dock.gameObject.AddComponent<Image>().color=new(.025f,.045f,.055f,.83f);
        Title=TVWidgets.Label(Dock,"",22,8,1030,28,22);Status=TVWidgets.Label(Dock,"",22,38,1090,30,24);
        Feedback=TVWidgets.Label(Dock,"",22,72,1090,30,22);Gauge=TVWidgets.Label(Dock,"",22,110,1090,28,20);
        Button(Dock,"Options · Tab",1140,12,190,()=>ShowOptions(!Options));Button(Dock,"Leave · Esc",1340,12,195,Close);
        Button(Dock,"Help · F1",1140,66,190,()=>ToggleHelp());
        Message=TVWidgets.Label(Dock,"",1140,110,390,30,18);
        Menu=TVWidgets.Rect(Root.transform,"Home-game options",0,0,760,382);Menu.anchorMin=Menu.anchorMax=Menu.pivot=new(.5f,.5f);Menu.anchoredPosition=new(0,-60);
        Menu.gameObject.AddComponent<Image>().color=new(.025f,.045f,.065f,.94f);
        TVWidgets.Label(Menu,"CHOOSE A SEAT",25,16,700,45,28);
        Solo=Button(Menu,"Solo practice",25,76,340,()=>{Send("solo");ShowOptions(false);});
        Button(Menu,"Two-player lobby",390,76,340,()=>{Send("duel");ShowOptions(false);});
        Button(Menu,"Join player 2",25,132,340,()=>{Send("join");ShowOptions(false);});
        Button(Menu,"Spectate / Resume",390,132,340,()=>ShowOptions(false));
        Button(Menu,"Restart / Rack",25,188,340,()=>{CancelCharge();Send("restart");ShowOptions(false);});
        ViewOption=Button(Menu,"Angled / overhead",390,188,340,()=>{if(Rig is not null){if(Kind=="pool")Rig.Overhead=!Rig.Overhead;else Rig.Fov=22;}ShowOptions(false);});
        TVWidgets.Label(Menu,"Mouse or arrows aim in the world. Hold left click or Space, then release. Wheel zooms. Esc leaves; Tab opens options and cancels a charge. No wagers.",25,250,705,112,21);
        Help=TVWidgets.Rect(Root.transform,"World-play help",0,0,640,235);Help.anchorMin=Help.anchorMax=Help.pivot=Vector2.one;Help.anchoredPosition=new(-20,-20);
        Help.gameObject.AddComponent<Image>().color=new(.025f,.045f,.065f,.88f);
        TVWidgets.Label(Help,"Darts: the world dot previews the host-timed impact. Release after 0.1s and before 3s.\nPool: aim from cue ball toward the world dot. Full power at 1.5s. With ball in hand, click or press Space to place on clear cloth.\nArrows nudge aim; mouse movement returns to pointer aim. Wheel zooms. Menus support keyboard selection. F1 hides this help.",18,12,604,210,21);
        Help.gameObject.SetActive(false);
        ExitHandler=DelegateSupport.ConvertDelegate<GameInput.ExitDelegate>((Action<ExitAction>)(action=>{if(Opened){action.used=true;Close();}}))!;
        GameInput.RegisterExitListener(ExitHandler,10);Root.SetActive(false);
    }
    private Button Button(Transform parent,string text,float x,float y,float width,Action action)=>TVWidgets.Button(parent,text,x,y,width,44,()=>
    {try{action();}catch(Exception e){Notice(AmenityNetwork.Safe(e));}});
    private void ToggleHelp()=>Help.gameObject.SetActive(!Help.gameObject.activeSelf);
    internal void Notice(string text){NoticeText=text[..Math.Min(160,text.Length)];NoticeUntil=AmenityNetwork.Now+5;if(Message)Message.text="";}
    internal void Open(string id)
    {
        if(Opened||!Feature.Views.TryGetValue(id,out var view)||!Player.Local||!Feature.CanUse(Player.Local,id))return;
        TelevisionFeature.Current?.Panel?.Close();if(!AmenityCameraRig.Available)return;
        Table=id;Kind=AmenityContent.Kind(view.Item.ItemInstance?.ID)!;LastState=null;Holding=false;WasParticipant=false;AimX=0;AimY=Kind=="darts"?.12:.45;KeyboardAim=false;Focused=Application.isFocused;
        try
        {
            Rig=new(Kind);Rig.Pose(view.Visual,Kind);
            BoardArtwork=view.BoardArtwork;Guides=new("VICE local aim guides");Guides.transform.SetParent(view.Visual,false);
            Target=AmenityContent.Primitive(Guides.transform,"World aim / placement",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.009f,AmenityContent.Material("aimguide",new(.7f,.95f,1)));
            var line=new GameObject("Pool world aim line");line.transform.SetParent(Guides.transform,false);AimLine=line.AddComponent<LineRenderer>();
            AimLine.useWorldSpace=false;AimLine.positionCount=2;AimLine.startWidth=AimLine.endWidth=.003f;AimLine.sharedMaterial=AmenityContent.Material("aimline",new(.7f,.95f,1));
            Opened=true;Root.SetActive(true);NextHeartbeat=0;Title.text=Kind=="darts"?"DARTS · 301":"POOL · CASUAL EIGHT-BALL";ShowOptions(true);
            ViewOption.GetComponentInChildren<TextMeshProUGUI>().text=Kind=="pool"?"Angled / overhead":"Reset zoom";
            Notice("Choose a seat, or spectate.");if(!AmenityNetwork.Host)AmenityNetwork.Submit(new());
        }
        catch{Rig?.Dispose();Rig=null;if(Guides)Object.Destroy(Guides);Guides=null;Target=null;AimLine=null;Table="";Opened=false;Holding=false;Root.SetActive(false);throw;}
    }
    private void Send(string action,double x=0,double y=0)
    {
        var state=Feature.States.GetValueOrDefault(Table)??throw new InvalidOperationException("Waiting for host table state…");
        AmenityNetwork.Submit(new(){Table=Table,Instance=state.Instance,Revision=state.Revision,Action=action,X=x,Y=y});
    }
    private void CancelCharge()
    {
        Holding=false;
        if(Feature.States.TryGetValue(Table,out var state)&&state.Charging==AmenityNetwork.LocalActor)Send("cancel");
        // Always send after local charging begins, even before the charge snapshot arrives.
        else if(LastState is not null)Send("cancel");
    }
    private void ShowOptions(bool open)
    {
        if(open&&Opened)CancelCharge();Options=open;Menu.gameObject.SetActive(open);InputAfter=AmenityNetwork.Now+.18;
        if(open)Solo.Select();else if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
    }
    private bool OverUI(Vector2 pointer)=>RectTransformUtility.RectangleContainsScreenPoint(Dock,pointer,null)
        ||Options&&RectTransformUtility.RectangleContainsScreenPoint(Menu,pointer,null)
        ||Help.gameObject.activeSelf&&RectTransformUtility.RectangleContainsScreenPoint(Help,pointer,null);
    internal void Tick()
    {
        if(!Opened)return;
        if(!Player.Local||!Feature.CanUse(Player.Local,Table)||!Feature.Views.TryGetValue(Table,out var view)||Rig?.Valid!=true){Close();return;}
        var state=Feature.States.GetValueOrDefault(Table);
        if(state is null){Status.text="Waiting for host table state…";return;}
        LastState=state;var actor=AmenityNetwork.LocalActor;var seat=Array.IndexOf(state.Seats,actor);var spectator=seat<0;
        if(WasParticipant&&spectator){Close();return;}WasParticipant|=!spectator;
        if(AmenityNetwork.Now>=NextHeartbeat){NextHeartbeat=AmenityNetwork.Now+2;if(!spectator)Send("heartbeat");}
        Status.text=Kind=="darts"?$"P1 {state.Darts.Remaining[0]}   P2 {state.Darts.Remaining[1]}  ·  Player {state.Turn+1}, dart {Math.Min(3,state.Darts.Darts+1)}/3  ·  {(spectator?"Spectating":"Your seat: "+(seat+1))}"
            :$"P1 {Group(state.Pool.Groups[0])}   P2 {Group(state.Pool.Groups[1])}  ·  Player {state.Turn+1}  ·  {(state.Moving?"Rolling":state.Pool.BallInHand?"Ball in hand":"Ready")}  ·  {(spectator?"Spectating":"Your seat: "+(seat+1))}";
        Feedback.text=AmenityNetwork.Now<NoticeUntil?NoticeText:state.Feedback;
        var keyboard=Keyboard.current;var mouse=Mouse.current;
        if(keyboard?.escapeKey.wasPressedThisFrame==true){Close();return;}
        if(PauseMenu.Instance&&PauseMenu.Instance.IsPaused){Close();return;}
        if(!Application.isFocused){if(Focused)CancelCharge();Focused=false;Gauge.text="Game unfocused · refocus to aim and play.";Target!.SetActive(false);AimLine!.enabled=false;return;}
        if(!Focused){Focused=true;InputAfter=AmenityNetwork.Now+.18;}
        if(mouse?.rightButton.wasPressedThisFrame==true){CancelCharge();Notice("Charge cancelled.");}
        if(keyboard?.tabKey.wasPressedThisFrame==true)ShowOptions(!Options);
        if(keyboard?.f1Key.wasPressedThisFrame==true)Help.gameObject.SetActive(!Help.gameObject.activeSelf);
        if(Options){Target!.SetActive(false);AimLine!.enabled=false;return;}
        var nudgeX=(keyboard?.rightArrowKey.isPressed==true?1:0)-(keyboard?.leftArrowKey.isPressed==true?1:0);
        var nudgeY=(keyboard?.upArrowKey.isPressed==true?1:0)-(keyboard?.downArrowKey.isPressed==true?1:0);
        if(!Holding&&(nudgeX!=0||nudgeY!=0))
        {KeyboardAim=true;var aim=WorldPlayGeometry.Nudge(AimX,AimY,WorldPlayGeometry.KeyboardX(nudgeX,Kind=="darts",BoardArtwork),nudgeY,Time.unscaledDeltaTime,Kind=="darts");AimX=aim.X;AimY=aim.Y;}
        if(mouse is not null&&mouse.delta.ReadValue().sqrMagnitude>1)KeyboardAim=false;
        var pointer=mouse?.position.ReadValue()??new Vector2(Screen.width*.5f,Screen.height*.5f);var overUI=OverUI(pointer)||!Rig.View.pixelRect.Contains(pointer);
        if(!Holding&&!KeyboardAim&&!overUI)
        {
            var ray=Rig.View.ScreenPointToRay(pointer);var plane=new Plane(Kind=="darts"?view.Visual.forward:view.Visual.up,
                view.Visual.TransformPoint(Kind=="darts"?new Vector3(0,0,.055f):new Vector3(0,.78f,0)));
            if(plane.Raycast(ray,out var distance)&&distance>0)
            {
                var local=view.Visual.InverseTransformPoint(ray.GetPoint(distance));
                if(Kind=="darts"){var aim=WorldPlayGeometry.AimFromBoard(local.x,local.y,BoardArtwork);AimX=Math.Clamp(aim.X,-.225,.225);AimY=Math.Clamp(aim.Y,-.225,.225);}
                else{AimX=Math.Clamp(local.x,-.56,.56);AimY=Math.Clamp(local.z,-1.12,1.12);}
            }
        }
        if(mouse is not null&&!overUI)Rig.Fov=(float)WorldPlayGeometry.Zoom(Rig.Fov,mouse.scroll.ReadValue().y/120,Kind=="darts");
        var pressed=keyboard?.spaceKey.wasPressedThisFrame==true||!overUI&&mouse?.leftButton.wasPressedThisFrame==true;
        var released=keyboard?.spaceKey.wasReleasedThisFrame==true||mouse?.leftButton.wasReleasedThisFrame==true;
        if(pressed&&!Holding&&AmenityNetwork.Now>=InputAfter&&state.Playable&&!state.Finished&&!state.Moving&&seat==state.Turn)
        {
            if(Kind=="pool"&&state.Pool.BallInHand)Send("place",AimX,AimY);
            else
            {
                var x=AimX;if(Kind=="pool"){var cue=state.Pool.Simulation.Balls.Single(b=>b.Number==0);x=Math.Atan2(AimX-cue.X,AimY-cue.Z);}
                Send("charge",x,AimY);Holding=true;HoldStarted=AmenityNetwork.Now;
            }
        }
        if(Holding&&released){Holding=false;Send("release");}
        if(Holding&&(AmenityNetwork.Now-HoldStarted>3||state.Seats[state.Turn]!=actor||state.Moving||state.Finished))CancelCharge();
        var elapsed=Holding?Math.Max(0,AmenityNetwork.Now-(state.Charging==actor?state.ChargeStarted:HoldStarted)):0;
        Gauge.text=Kind=="darts"?Holding?$"Release timing: {elapsed:0.00}s · world dot previews impact":"Aim with mouse/arrows · hold left click/Space · wheel zooms"
            :$"Power {(int)(Math.Clamp(elapsed/1.5,0,1)*100)}% · hold left click/Space · wheel zooms";
        var targetX=AimX;var targetY=AimY;
        if(Kind=="darts"&&Holding&&elapsed is >=.1 and <=3){var hit=DartsRules.Throw(AimX,AimY,elapsed);targetX=hit.X;targetY=hit.Y;}
        Target!.SetActive(!spectator&&!state.Finished&&!state.Moving);AimLine!.enabled=Kind=="pool"&&!spectator&&!state.Moving&&!state.Pool.BallInHand;
        if(Kind=="darts")
        {var hit=HomeGameVisualContract.Dart(targetX,targetY,BoardArtwork);Target.transform.localScale=Vector3.one*.009f;Target.transform.localPosition=new((float)hit.X,(float)hit.Y,.060f);}
        else
        {
            var cue=state.Pool.Simulation.Balls.Single(b=>b.Number==0);var point=new Vector3((float)targetX,.808575f,(float)targetY);
            Target.transform.localPosition=point;Target.transform.localScale=Vector3.one*(state.Pool.BallInHand?(float)(PoolSimulation.Radius*2):.012f);
            Target.GetComponent<Renderer>().sharedMaterial=AmenityContent.Material(state.Pool.BallInHand&&!state.Pool.Simulation.CanPlace(targetX,targetY)?"badplacement":"aimguide",
                state.Pool.BallInHand&&!state.Pool.Simulation.CanPlace(targetX,targetY)?new Color(1,.2f,.15f):new Color(.7f,.95f,1));
            AimLine.SetPosition(0,new((float)cue.X,.811f,(float)cue.Z));AimLine.SetPosition(1,new((float)targetX,.811f,(float)targetY));
        }
    }
    internal void LateTick(){if(Opened&&Rig is not null&&Feature.Views.TryGetValue(Table,out var view))Rig.Pose(view.Visual,Kind);}
    private static string Group(int group)=>group==1?"solids":group==2?"stripes":"open";
    internal void Close()
    {
        if(!Opened)return;Opened=false;Holding=false;
        try
        {
            var state=Feature.States.GetValueOrDefault(Table)??LastState;
            if(state is not null&&state.Seats.Contains(AmenityNetwork.LocalActor))AmenityNetwork.Submit(new(){Table=Table,Instance=state.Instance,Revision=state.Revision,Action="leave"});
        }
        catch(Exception e){ModSupport.WarnOnce("amenities:leave",e.Message);}
        finally
        {
            try{Rig?.Dispose();}finally{Rig=null;if(Root)Root.SetActive(false);if(Guides)Object.Destroy(Guides);Guides=null;Target=null;AimLine=null;Table="";LastState=null;WasParticipant=false;}
        }
    }
    public void Dispose(){Close();GameInput.DeregisterExitListener(ExitHandler);if(Root)Object.Destroy(Root);}
}
