using Il2CppInterop.Runtime;
using Il2CppScheduleOne;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VICE.Lifestyle.Television;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Amenities;

// A precise, temporary overhead/face view avoids changing the native camera transform.
internal sealed class AmenityPanel : IDisposable
{
    private readonly AmenitiesFeature Feature;
    private readonly GameObject Root;
    private readonly RectTransform Frame,Arena,Crosshair,AimLine;
    private readonly TextMeshProUGUI Title,Status,Feedback,Message,Gauge;
    private readonly List<GameObject> Markers=new();
    private readonly Dictionary<int,RectTransform> BallMarkers=new();
    private readonly GameInput.ExitDelegate ExitHandler;
    private readonly Sprite Circle;
    private string Table="",Kind="";
    private bool Opened,Holding,PreviousLook,PreviousMove,PreviousHotbar,PreviousEquipping,PreviousTyping;
    private double HoldStarted,NextHeartbeat;
    private double AimX,AimY;
    private AmenitySession? LastState;
    internal AmenityPanel(AmenitiesFeature feature)
    {
        Feature=feature;
        var tex=new Texture2D(32,32,TextureFormat.RGBA32,false);
        for(var y=0;y<32;y++)for(var x=0;x<32;x++)tex.SetPixel(x,y,(x-15.5)*(x-15.5)+(y-15.5)*(y-15.5)<=15*15?Color.white:Color.clear);
        tex.Apply();Circle=Sprite.Create(tex,new Rect(0,0,32,32),new(.5f,.5f));
        Root=new GameObject("VICE home games prototype",new[]{Il2CppType.Of<RectTransform>()});
        var canvas=Root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=95;
        var scaler=Root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);
        Root.AddComponent<GraphicRaycaster>();Frame=TVWidgets.Rect(Root.transform,"Prototype panel",0,0,1400,970);
        Frame.anchorMin=Frame.anchorMax=Frame.pivot=new(.5f,.5f);Frame.anchoredPosition=Vector2.zero;
        Frame.gameObject.AddComponent<Image>().color=new(.035f,.05f,.065f,.99f);
        Title=TVWidgets.Label(Frame,"HOME GAMES · PROTOTYPE CONTROLS",30,16,1150,45,26);
        Button("Leave · Esc",1200,20,170,Close);
        Arena=TVWidgets.Rect(Frame,"Temporary play surface",55,115,640,640);
        Crosshair=TVWidgets.Rect(Arena,"Aim",0,0,12,12);Crosshair.gameObject.AddComponent<Image>().color=new(1,1,1,.8f);Crosshair.gameObject.GetComponent<Image>().raycastTarget=false;
        AimLine=TVWidgets.Rect(Arena,"Cue aim line",0,0,160,3);AimLine.gameObject.AddComponent<Image>().color=new(1,1,1,.65f);AimLine.gameObject.GetComponent<Image>().raycastTarget=false;AimLine.pivot=new(0,.5f);
        Status=TVWidgets.Label(Frame,"",760,110,605,175,22);
        Feedback=TVWidgets.Label(Frame,"",760,290,600,120,23);
        Gauge=TVWidgets.Label(Frame,"",760,415,605,100,22);
        TVWidgets.Label(Frame,"Darts: aim with mouse; hold left click, then release as the timing displacement approaches your target. The white dot shows predicted impact. Release within 3 seconds.\n\nPool: aim from cue ball toward mouse, hold left click to build power, release to strike. On a foul, click clear cloth to place the cue ball.\n\nPlayer 1 restarts. Both players must keep controls open; spectators may leave freely. No wagers.",760,540,605,310,19);
        Button("Solo practice",40,880,190,()=>Send("solo"));Button("Two-player lobby",245,880,220,()=>Send("duel"));Button("Join player 2",480,880,190,()=>Send("join"));Button("Restart / Rack",685,880,205,()=>Send("restart"));
        Message=TVWidgets.Label(Frame,"",40,928,1320,30,18);
        ExitHandler=DelegateSupport.ConvertDelegate<GameInput.ExitDelegate>((Action<ExitAction>)(action=>{if(Opened){action.used=true;Close();}}))!;
        GameInput.RegisterExitListener(ExitHandler,10);Root.SetActive(false);
    }
    private void Button(string text,float x,float y,float width,Action action)=>TVWidgets.Button(Frame,text,x,y,width,40,()=>
    {try{action();}catch(Exception e){Notice(AmenityNetwork.Safe(e));}});
    internal void Notice(string text){if(Message)Message.text=text[..Math.Min(300,text.Length)];}
    internal void Open(string id)
    {
        if(Opened||!Feature.Views.TryGetValue(id,out var view)||!Player.Local||!Feature.CanUse(Player.Local,id))return;
        TelevisionFeature.Current?.Panel?.Close();
        var camera=PlayerCamera.Instance;if(!camera||camera.ActiveUIElementCount>0||!PlayerMovement.Instance||!PlayerInventory.Instance)return;
        Table=id;Kind=AmenityContent.Kind(view.Item.ItemInstance?.ID)!;LastState=null;Holding=false;AimX=0;AimY=Kind=="darts"?.12:.4;
        PreviousLook=camera.CanLook;PreviousMove=PlayerMovement.Instance._canMove;PreviousHotbar=PlayerInventory.Instance.HotbarEnabled;
        PreviousEquipping=PlayerInventory.Instance.EquippingEnabled;PreviousTyping=GameInput.IsTyping;
        camera.AddActiveUIElement("VICEHomeGames");camera.SetCanLook(false);camera.FreeMouse(true);PlayerMovement.Instance._canMove=false;
        PlayerInventory.Instance.SetInventoryEnabled(false);GameInput.IsTyping=true;Opened=true;Root.SetActive(true);
        Title.text=(Kind=="darts"?"DARTS · 301":"POOL · CASUAL EIGHT-BALL")+" · PROTOTYPE CONTROLS";
        DrawSurface();NextHeartbeat=0;Notice("Choose Solo practice or Two-player lobby. Open a busy table to spectate.");
        if(!AmenityNetwork.Host)AmenityNetwork.Submit(new());
    }
    private RectTransform Dot(string name,double x,double y,float size,Color color)
    {
        var dot=TVWidgets.Rect(Arena,name,(float)x-size/2,(float)y-size/2,size,size);var image=dot.gameObject.AddComponent<Image>();image.sprite=Circle;image.color=color;image.raycastTarget=false;Markers.Add(dot.gameObject);return dot;
    }
    private void DrawSurface()
    {
        foreach(var marker in Markers)if(marker)Object.Destroy(marker);Markers.Clear();BallMarkers.Clear();
        var old=Arena.GetComponent<RawImage>();if(old)Object.DestroyImmediate(old);
        var oldImage=Arena.GetComponent<Image>();if(oldImage)Object.DestroyImmediate(oldImage);
        if(Kind=="darts")
        {
            TVWidgets.Place(Arena,55,125,640,640);Arena.gameObject.AddComponent<RawImage>().texture=AmenityContent.BoardTexture();
            int[] wedges={20,1,18,4,13,6,10,15,2,17,3,19,7,16,8,11,14,9,12,5};
            for(var i=0;i<20;i++)
            {
                var angle=i*Math.PI/10;var point=ToPanel(Math.Sin(angle)*.20,Math.Cos(angle)*.20);
                var label=TVWidgets.Label(Arena,wedges[i].ToString(),point.x-16,point.y-16,32,32,21);label.alignment=TextAlignmentOptions.Midline;Markers.Add(label.gameObject);
            }
            Dot("Last impact",0,0,8,new(1,.65f,.12f));
        }
        else
        {
            TVWidgets.Place(Arena,160,105,360,720);Arena.gameObject.AddComponent<Image>().color=new(.045f,.34f,.21f);
            foreach(var pocket in PoolSimulation.Pockets){var point=ToPanel(pocket.X,pocket.Z);Dot("Pocket",point.x,point.y,42,new(.01f,.01f,.015f));}
            for(var n=0;n<16;n++)
            {
                var color=n==0?Color.white:n==8?new Color(.035f,.035f,.035f):Color.HSVToRGB(((n-1)%8)/8f,.8f,1);
                var dot=Dot("Ball "+n,0,0,18.4f,n>=9?Color.white:color);
                if(n>=9){var band=TVWidgets.Rect(dot,"Stripe",0,5,18.4f,8.4f);band.gameObject.AddComponent<Image>().color=color;band.GetComponent<Image>().raycastTarget=false;}
                if(n>0){var label=TVWidgets.Label(dot,n.ToString(),0,0,18.4f,18.4f,11);label.alignment=TextAlignmentOptions.Midline;label.color=n==8?Color.white:Color.black;}
                BallMarkers[n]=dot;
            }
        }
        Crosshair.SetAsLastSibling();AimLine.SetAsLastSibling();
    }
    private Vector2 ToPanel(double x,double y)=>Kind=="darts"?new((float)((x/.45+.5)*640),(float)((.5-y/.45)*640)):new((float)((x/1.12+.5)*360),(float)((.5-y/2.24)*720));
    private void Send(string action,double x=0,double y=0)
    {
        var state=Feature.States.GetValueOrDefault(Table)??throw new InvalidOperationException("Waiting for host table state…");
        AmenityNetwork.Submit(new(){Table=Table,Instance=state.Instance,Revision=state.Revision,Action=action,X=x,Y=y});
    }
    internal void Tick()
    {
        if(!Opened)return;
        if(!Player.Local||!Feature.CanUse(Player.Local,Table)||!Feature.Views.ContainsKey(Table)){Close();return;}
        var state=Feature.States.GetValueOrDefault(Table);
        if(state is null){Status.text="Waiting for host table state…";return;}
        LastState=state;var actor=AmenityNetwork.LocalActor;
        if(AmenityNetwork.Now>=NextHeartbeat){NextHeartbeat=AmenityNetwork.Now+2;if(state.Seats.Contains(actor))Send("heartbeat");}
        var seat=Array.IndexOf(state.Seats,actor);var spectator=seat<0;
        Status.text=$"{state.Mode.ToUpperInvariant()} · {(spectator?"Spectating":"You are player "+(seat+1))}\nSeats: {SeatName(state.Seats[0])} / {SeatName(state.Seats[1])}\n";
        if(state.Kind=="darts")Status.text+=$"Player 1: {state.Darts.Remaining[0]}   Player 2: {state.Darts.Remaining[1]}\nTurn: player {state.Turn+1} · dart {Math.Min(3,state.Darts.Darts+1)} of 3";
        else Status.text+=$"Player 1: {GroupName(state.Pool.Groups[0])} / Player 2: {GroupName(state.Pool.Groups[1])}\nTurn: player {state.Turn+1} · {(state.Moving?"rolling":state.Pool.BallInHand?"ball in hand":"ready")}";
        Feedback.text=state.Feedback;
        var mouse=Mouse.current;if(mouse is null)return;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(Arena,mouse.position.ReadValue(),null,out var local))
        {
            var inside=local.x>=0&&local.x<=Arena.rect.width&&local.y<=0&&local.y>=-Arena.rect.height;
            if(inside&&!Holding)
            {AimX=(local.x/Arena.rect.width-.5)*(Kind=="darts"?.45:1.12);AimY=(local.y/Arena.rect.height+.5)*(Kind=="darts"?.45:2.24);}
            if(inside&&mouse.leftButton.wasPressedThisFrame&&!Holding&&state.Playable&&!state.Finished&&!state.Moving&&seat==state.Turn)
            {
                if(Kind=="pool"&&state.Pool.BallInHand){Send("place",AimX,AimY);}
                else
                {
                    var x=AimX;if(Kind=="pool"){var cue=state.Pool.Simulation.Balls.Single(b=>b.Number==0);x=Math.Atan2(AimX-cue.X,AimY-cue.Z);}
                    Send("charge",x,AimY);Holding=true;HoldStarted=AmenityNetwork.Now;
                }
            }
        }
        if(Holding&&mouse.leftButton.wasReleasedThisFrame){Holding=false;Send("release");}
        if(Holding&&AmenityNetwork.Now-HoldStarted>3){Holding=false;Notice("Charge expired. Release and try again.");}
        var elapsed=Holding?AmenityNetwork.Now-(state.Charging==actor?state.ChargeStarted:HoldStarted):state.Charging>=0?AmenityNetwork.Now-state.ChargeStarted:0;
        Gauge.text=Kind=="darts"?"Timing: "+(elapsed>0?$"{elapsed:0.00}s · displacement follows white dot":"hold then release near your chosen point")
            :$"Power: {(int)(Math.Clamp(elapsed/1.5,0,1)*100)}% · maximum at 1.5s · release before 3s";
        var targetX=AimX;var targetY=AimY;
        if(Kind=="darts"&&Holding&&elapsed>=.1&&elapsed<=3){var hit=DartsRules.Throw(AimX,AimY,elapsed);targetX=hit.X;targetY=hit.Y;}
        var cross=ToPanel(targetX,targetY);Crosshair.anchoredPosition=new(cross.x-6,-cross.y+6);
        Crosshair.gameObject.SetActive(!spectator&&!state.Finished);AimLine.gameObject.SetActive(Kind=="pool"&&!state.Pool.BallInHand&&!state.Moving);
        if(Kind=="pool")
        {
            foreach(var b in state.Pool.Simulation.Balls)
            {var dot=BallMarkers[b.Number];dot.gameObject.SetActive(!b.Pocketed);var point=ToPanel(b.X,b.Z);dot.anchoredPosition=new(point.x-9.2f,-point.y+9.2f);}
            var cue=state.Pool.Simulation.Balls.Single(b=>b.Number==0);var cuePoint=ToPanel(cue.X,cue.Z);
            AimLine.anchoredPosition=new(cuePoint.x,-cuePoint.y);AimLine.localRotation=Quaternion.Euler(0,0,(float)(Math.Atan2(AimY-cue.Z,AimX-cue.X)*180/Math.PI));
        }
        else
        {
            var marker=Markers.FirstOrDefault(m=>m.name=="Last impact");if(marker){marker!.SetActive(state.Darts.Throws>0);var point=ToPanel(state.Darts.LastX,state.Darts.LastY);marker.GetComponent<RectTransform>().anchoredPosition=new(point.x-4,-point.y+4);}
        }
    }
    private static string GroupName(int group)=>group==1?"solids":group==2?"stripes":"open";
    private static string SeatName(int actor)
    {if(actor<0)return "empty";var player=Player.PlayerList.ToArray().FirstOrDefault(p=>p&&p.Owner.ClientId==actor);return player?player!.PlayerName:"player "+actor;}
    internal void Close()
    {
        if(!Opened)return;Opened=false;Holding=false;
        try
        {
            var state=Feature.States.GetValueOrDefault(Table)??LastState;
            if(state is not null&&state.Seats.Contains(AmenityNetwork.LocalActor))
                AmenityNetwork.Submit(new(){Table=Table,Instance=state.Instance,Revision=state.Revision,Action="leave"});
        }
        catch(Exception e){ModSupport.WarnOnce("amenities:leave",e.Message);}
        finally
        {
            if(Root)Root.SetActive(false);var camera=PlayerCamera.Instance;
            if(camera){camera.RemoveActiveUIElement("VICEHomeGames");camera.SetCanLook(PreviousLook);if(camera.ActiveUIElementCount==0)camera.LockMouse(true);}
            if(PlayerMovement.Instance)PlayerMovement.Instance._canMove=PreviousMove;
            if(PlayerInventory.Instance){PlayerInventory.Instance.SetInventoryEnabled(PreviousHotbar);PlayerInventory.Instance.SetEquippingEnabled(PreviousEquipping);}
            GameInput.IsTyping=PreviousTyping;Table="";LastState=null;
        }
    }
    public void Dispose(){Close();GameInput.DeregisterExitListener(ExitHandler);if(Root)Object.Destroy(Root);if(Circle){if(Circle.texture)Object.Destroy(Circle.texture);Object.Destroy(Circle);}}
}
