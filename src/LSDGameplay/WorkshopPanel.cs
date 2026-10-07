using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace PersonalMods.LSDGameplay;

internal sealed class WorkshopPanel : IDisposable
{
    private readonly GameObject Root;
    private readonly RectTransform Dialog;
    private readonly TextMeshProUGUI Title,Status,Message,TaskStatus;
    private readonly RawImage Preview;
    private readonly TMP_InputField NameInput;
    private readonly List<GameObject> Controls=new();
    private readonly GameObject Guides;
    private readonly RectTransform TargetMarker,ControlMarker,ProgressBar;
    private TextMeshProUGUI? StageControls;
    private readonly Texture2D DrawingTexture;
    private readonly Stack<byte[]> Undo=new();
    private readonly GameInput.ExitDelegate ExitHandler;
    private BuildableItem? Equipment;
    private string MachineId="";
    private int SelectedArt;
    private bool Editing,Repeated,Open,Busy,PreviousLook,PreviousMove,PreviousHotbar,PreviousTyping,Stroke;
    private int Brush=2,Ink=3,LastX=-1,LastY=-1;
    private byte[] Pixels=new byte[4096];
    private string Task="";
    private float Elapsed,Score,ControlX=0.5f,ControlY=0.5f;
    private StudioCommand? Pending;
    private float SubmittedAt;
    private long SeenRevision=-1;

    internal WorkshopPanel()
    {
        Root=new GameObject("Off Register workshop",new[] { Il2CppType.Of<RectTransform>() });
        var canvas=Root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;
        var scaler=Root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);
        Root.AddComponent<GraphicRaycaster>();
        Dialog=StudioWidgets.Rect(Root.transform,"Print studio",0,0,1000,680);
        Dialog.anchorMin=Dialog.anchorMax=Dialog.pivot=new(0.5f,0.5f);Dialog.anchoredPosition=Vector2.zero;
        var background=Dialog.gameObject.AddComponent<Image>();background.color=new(0.035f,0.055f,0.07f,0.97f);
        Title=StudioWidgets.Label(Dialog,"Off Register",26,22,820,45,30);
        StudioWidgets.Button(Dialog,"Close",882,22,92,36,Close);
        Status=StudioWidgets.Label(Dialog,"",26,76,948,78,19);
        Preview=StudioWidgets.Rect(Dialog,"Edition preview",26,172,384,320).gameObject.AddComponent<RawImage>();
        Preview.texture=StudioAssets.Texture(0);Preview.raycastTarget=true;
        Guides=StudioWidgets.Rect(Preview.transform,"Registration guides",0,0,384,320).gameObject;
        for (var i=1;i<8;i++) Guide(Guides.transform,i*48,0,1,320,new(1,1,1,0.25f));
        for (var i=1;i<5;i++) Guide(Guides.transform,0,i*64,384,1,new(1,1,1,0.25f));
        TargetMarker=Guide(Guides.transform,192,160,30,2,new(0.9f,1,0.3f));
        Guide(TargetMarker,14,-14,2,30,new(0.9f,1,0.3f));
        ControlMarker=Guide(Guides.transform,192,160,26,2,new(1,0.25f,0.22f));
        Guide(ControlMarker,12,-12,2,26,new(1,0.25f,0.22f));
        ProgressBar=Guide(Dialog,438,536,1,8,new(0.3f,0.85f,0.7f));
        Guides.SetActive(false);ProgressBar.gameObject.SetActive(false);
        TaskStatus=StudioWidgets.Label(Dialog,"",26,500,384,110,18);
        NameInput=StudioWidgets.Input(Dialog,"Edition name",438,172,530,38);NameInput.gameObject.SetActive(false);
        Message=StudioWidgets.Label(Dialog,"",26,624,948,42,17);
        DrawingTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);DrawingTexture.filterMode=FilterMode.Point;
        ExitHandler=DelegateSupport.ConvertDelegate<GameInput.ExitDelegate>((Action<ExitAction>)(action=>{
            if (!Open) return;action.used=true;Close();
        })) ?? throw new InvalidOperationException("Could not bind workshop exit.");
        GameInput.RegisterExitListener(ExitHandler,10);Root.SetActive(false);
    }

    internal void ShowIntroduction()
    {
        Equipment=null;MachineId="";Show();Refresh();
    }
    internal void OpenMachine(BuildableItem equipment)
    {
        if (Open) return;
        Equipment=equipment;MachineId=equipment.GUID.ToString();
        var machine=LSDGameplayMod.Current!.State.Machines.FirstOrDefault(m=>m.Id==MachineId);
        if (machine is not null && machine.Stage!=MachineStage.Empty) SelectedArt=machine.ArtSlot;
        Show();Refresh();
    }
    private void Show()
    {
        if (Open) return;
        var camera=PlayerCamera.Instance;PreviousLook=camera.CanLook;PreviousMove=PlayerMovement.Instance._canMove;
        PreviousHotbar=PlayerInventory.Instance.HotbarEnabled;PreviousTyping=GameInput.IsTyping;
        camera.AddActiveUIElement("OffRegister");camera.SetCanLook(false);camera.FreeMouse(true);
        PlayerMovement.Instance._canMove=false;PlayerInventory.Instance.SetInventoryEnabled(false);GameInput.IsTyping=true;
        Open=true;Root.SetActive(true);Message.text="";
    }
    internal void Close()
    {
        if (!Open) return;
        if (MachineId.Length>0 && LSDGameplayMod.Current?.Ready==true && !Busy)
            WorkshopNetwork.Submit(new StudioCommand { Action="release",Machine=MachineId });
        Open=false;Task="";Editing=false;Equipment=null;Root.SetActive(false);
        var camera=PlayerCamera.Instance;
        if (camera) { camera.RemoveActiveUIElement("OffRegister");camera.SetCanLook(PreviousLook);if (camera.ActiveUIElementCount==0) camera.LockMouse(true); }
        if (PlayerMovement.Instance) PlayerMovement.Instance._canMove=PreviousMove;
        if (PlayerInventory.Instance) PlayerInventory.Instance.SetInventoryEnabled(PreviousHotbar);
        GameInput.IsTyping=PreviousTyping;
    }
    internal void ShowMessage(string message) { if (Message) Message.text=message;CollectiveCatalogue.UpdateStatus(); }

    private void Send(StudioCommand command)
    {
        if (Busy) return;
        command.Machine=MachineId;Pending=command;Busy=true;SubmittedAt=Time.realtimeSinceStartup;
        WorkshopNetwork.Submit(command);
    }
    internal void OnReply(StudioReply reply)
    {
        var command=Pending;
        if (command is not null && reply.Token==command.Token)
        {
            Busy=false;Pending=null;
            if (reply.Success && command.Action is "start" or "prepare" or "cut" or "resume")
            {
                var stage=LSDGameplayMod.Current!.State.Machines.FirstOrDefault(m=>m.Id==MachineId)?.Stage;
                Task=stage switch { MachineStage.Preparing=>"prepare",MachineStage.Printing=>"print",MachineStage.Cutting=>"finishcut",_=>"" };
                Elapsed=Score=0;ControlX=ControlY=0.5f;
                BuildTaskControls();
            }
            if (reply.Success && command.Action=="draw")
            { Editing=false;SelectedArt=LSDGameplayMod.Current!.State.Artworks.Last().Slot; }
            Refresh();
        }
        CollectiveCatalogue.UpdateStatus();
    }

    internal void Refresh()
    {
        if (!Open || Task.Length>0) { CollectiveCatalogue.UpdateStatus();return; }
        ClearControls();
        Guides.SetActive(MachineId.Length>0);TargetMarker.gameObject.SetActive(false);ControlMarker.gameObject.SetActive(false);
        ProgressBar.gameObject.SetActive(false);Preview.uvRect=new(0,0,1,1);
        var state=LSDGameplayMod.Current!.State;SeenRevision=state.Revision;
        Title.text=MachineId.Length==0 ? "OFF REGISTER" : Equipment?.ItemInstance.ID==WorkshopRules.Cabinet ? "DRYING CABINET" : "BLOTTER BENCH";
        NameInput.gameObject.SetActive(Editing);TaskStatus.text="";
        if (MachineId.Length==0)
        {
            Preview.texture=StudioAssets.Texture(2);
            Status.text=state.Introduced ? "The collective is on your phone. Build your printing studio in this office." : "A sticker from an underground print collective. They know a place to get specialist material.";
            AddButton(state.Introduced ? "Open phone catalogue" : "Meet the collective",438,184,530,44,()=>{
                if (state.Introduced) { Close();Il2CppScheduleOne.UI.Phone.Phone.Instance.SetIsOpen(true); }
                else Send(new StudioCommand { Action="introduce" });
            });
            AddLabel("One recipe. Your artwork. Physical sheets, a drying cabinet, and tabs you can sell.\n\nEquipment and paper: warehouse.\nPrint cartridges: hardware store.\nPrism Base parcels: Off Register phone catalogue.",438,254,530,210);
            return;
        }
        var machine=state.Machines.FirstOrDefault(m=>m.Id==MachineId);
        var stage=machine?.Stage ?? MachineStage.Empty;
        if (machine is not null && stage!=MachineStage.Empty) SelectedArt=machine.ArtSlot;
        var art=state.Artworks.FirstOrDefault(a=>a.Slot==SelectedArt) ?? state.Artworks[0];SelectedArt=art.Slot;
        Preview.texture=Editing ? DrawingTexture : StudioAssets.Texture(SelectedArt);
        Preview.uvRect=!Editing && art.Repeated ? new(0,0,8,5) : new(0,0,1,1);
        Status.text=stage switch {
            MachineStage.Empty=>"Ready for a new batch. Choose a saved edition. Materials set quality; mistakes only reduce usable tabs.",
            MachineStage.Preparing=>"Supplies are loaded. Resume preparation; the supplies will not be charged again.",
            MachineStage.Printing=>"Preparation is complete. Resume sheet alignment and printing.",
            MachineStage.WetOutput=>$"Wet sheet ready · {machine!.Yield} usable tabs. Carry it to the drying cabinet.",
            MachineStage.Drying=>$"Drying · {art.Name} · {Math.Max(0,machine!.DueMinute-LSDGameplayMod.Minute)} game minutes remaining. You can leave or sleep.",
            MachineStage.DryOutput=>$"Ready sheet · {art.Name} · {machine!.Yield} tabs before cutting. Carry it back to the bench.",
            MachineStage.Cutting=>"Ready sheet loaded. Resume cutting without consuming another sheet.",
            _=>$"Finished tabs · {art.Name} · {machine!.Yield} Standard-quality sale units. Collect into your inventory."
        };
        TaskStatus.text=art.Name+"\n"+WorkshopRules.EditionLabel(art.Slot)+" · "+(art.Repeated ? "Repeated motif" : "Whole-sheet artwork")+"\nGrid: 8 × 5 tabs";
        if (Editing) { BuildDrawingControls();return; }
        if (Equipment?.ItemInstance.ID==WorkshopRules.Cabinet)
        {
            if (stage==MachineStage.Empty) AddButton("Load a wet sheet",438,184,530,46,()=>Send(new() { Action="dry" }));
            else if (stage==MachineStage.DryOutput) AddButton("Collect ready sheet",438,184,530,46,()=>Send(new() { Action="collect" }));
            AddLabel("One batch tray. Drying takes two game hours and preserves Standard quality and artwork. The ready sheet stays here until you have inventory space.",438,254,530,150);
            return;
        }
        if (stage is MachineStage.Preparing or MachineStage.Printing or MachineStage.Cutting)
        {
            AddButton("Resume manual stage",438,184,530,46,()=>Send(new() { Action="resume" }));return;
        }
        if (stage is MachineStage.WetOutput or MachineStage.TabOutput)
        { AddButton("Collect output",438,184,530,46,()=>Send(new() { Action="collect" }));return; }
        var y=172f;
        foreach (var choice in state.Artworks)
        {
            var slot=choice.Slot;
            AddButton((slot==SelectedArt ? "● " : "")+choice.Name+" · "+WorkshopRules.EditionLabel(slot),438,y,530,33,()=>{ SelectedArt=slot;Refresh(); });y+=37;
        }
        AddButton("Prepare and print batch",438,y+4,260,40,()=>Send(new() { Action="start",ArtSlot=SelectedArt }));
        AddButton("Load ready sheet for cutting",708,y+4,260,40,()=>Send(new() { Action="cut" }));
        AddButton("Draw a new edition",438,y+52,530,36,StartDrawing);
        for (var i=0;i<3;i++)
        {
            var count=new[] { 1,5,20 }[i];
            AddButton($"Pack {count} tab{(count==1 ? "" : "s")}",438+i*180,y+100,170,36,()=>Send(new() { Action="pack",ArtSlot=SelectedArt,Quantity=count }));
        }
    }

    private void StartDrawing()
    {
        Pixels=(byte[])LSDGameplayMod.Current!.State.Artworks.Single(a=>a.Slot==SelectedArt).Pixels.Clone();
        Repeated=LSDGameplayMod.Current.State.Artworks.Single(a=>a.Slot==SelectedArt).Repeated;
        Editing=true;NameInput.text="";Undo.Clear();UploadDrawing();Refresh();
    }
    private void BuildDrawingControls()
    {
        for (var i=0;i<8;i++)
        {
            var color=i;var button=AddButton(i==0 ? "Erase" : "●",438+i*66,224,60,38,()=>Ink=color);
            button.targetGraphic.color=StudioAssets.Palette[i];
        }
        AddButton("Brush −",438,278,160,38,()=>Brush=Math.Max(1,Brush-1));
        AddButton("Brush +",612,278,160,38,()=>Brush=Math.Min(5,Brush+1));
        AddButton("Undo",786,278,182,38,()=>{ if (Undo.Count>0) { Pixels=Undo.Pop();UploadDrawing(); } });
        AddButton(Repeated ? "Layout: repeated motif" : "Layout: whole sheet",438,332,530,38,()=>{ Repeated=!Repeated;Refresh(); });
        AddLabel("Draw directly on the sheet. Saving uses a new immutable revision. Old sheets and tabs keep the artwork they were printed with. Five custom revisions are available in this slice.",438,390,530,126);
        AddButton("Save new edition",438,548,260,40,()=>Send(new() { Action="draw",Name=NameInput.text,Pixels=Pixels,Repeated=Repeated }));
        AddButton("Discard drawing",708,548,260,40,()=>{ Editing=false;Refresh(); });
        TaskStatus.text="Brush: "+Brush+" · Hold left mouse to draw.\nThe grid is a cutting guide; it is not painted into your edition.";
    }
    private void UploadDrawing()
    { DrawingTexture.SetPixels32(new Il2CppStructArray<Color32>(Pixels.Select(p=>StudioAssets.Palette[p]).ToArray()));DrawingTexture.Apply(); }

    internal void Tick()
    {
        if (!Open) return;
        if (!Player.Local || Player.Local.IsUnconscious || !Player.Local.Health.IsAlive || (Equipment is not null && Equipment && Vector3.Distance(Player.Local.transform.position,Equipment.transform.position)>4))
        { Close();return; }
        if (Busy && Time.realtimeSinceStartup-SubmittedAt>8)
        { SubmittedAt=Time.realtimeSinceStartup;ShowMessage("Waiting for the host. Retrying this same action safely.");if (Pending is not null) WorkshopNetwork.Submit(Pending); }
        if (Editing) { DrawTick();return; }
        if (Task.Length>0) { TaskTick();return; }
        if (SeenRevision!=LSDGameplayMod.Current!.State.Revision) Refresh();
        var machine=LSDGameplayMod.Current.State.Machines.FirstOrDefault(m=>m.Id==MachineId);
        if (machine?.Stage==MachineStage.Drying) Status.text=$"Drying · {Math.Max(0,machine.DueMinute-LSDGameplayMod.Minute)} game minutes remaining. You can leave or sleep.";
    }
    private bool Pointer(out Vector2 normalized)
    {
        normalized=Vector2.zero;var mouse=Mouse.current;if (mouse is null) return false;
        var rect=Preview.GetComponent<RectTransform>();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,mouse.position.ReadValue(),null,out var point)) return false;
        normalized=new(point.x/rect.rect.width,1+point.y/rect.rect.height);
        return normalized.x is >=0 and <=1 && normalized.y is >=0 and <=1;
    }
    private void DrawTick()
    {
        var held=Mouse.current?.leftButton.isPressed==true;
        if (!held || !Pointer(out var point)) { Stroke=false;LastX=LastY=-1;return; }
        if (!Stroke) { if (Undo.Count<16) Undo.Push((byte[])Pixels.Clone());Stroke=true; }
        var x=Math.Clamp((int)(point.x*64),0,63);var y=Math.Clamp((int)(point.y*64),0,63);
        var steps=LastX<0 ? 1 : Math.Max(1,Math.Max(Math.Abs(x-LastX),Math.Abs(y-LastY)));
        var fromX=LastX<0 ? x : LastX;var fromY=LastY<0 ? y : LastY;
        for (var n=0;n<=steps;n++)
        {
            var cx=fromX+(x-fromX)*n/steps;var cy=fromY+(y-fromY)*n/steps;
            for (var dy=-Brush;dy<=Brush;dy++) for (var dx=-Brush;dx<=Brush;dx++)
                if (dx*dx+dy*dy<=Brush*Brush && cx+dx is >=0 and <64 && cy+dy is >=0 and <64)
                    Pixels[(cy+dy)*64+cx+dx]=(byte)Ink;
        }
        LastX=x;LastY=y;UploadDrawing();
    }
    private void TaskTick()
    {
        if (Busy || Time.timeScale<=0 || !Application.isFocused) return;
        var keyboard=Keyboard.current;
        var delta=Math.Min(Time.unscaledDeltaTime,0.1f);
        if (keyboard is not null)
        {
            ControlX=Math.Clamp(ControlX+((keyboard.rightArrowKey.isPressed ? 1:0)-(keyboard.leftArrowKey.isPressed ? 1:0))*delta*0.4f,0,1);
            ControlY=Math.Clamp(ControlY+((keyboard.upArrowKey.isPressed ? 1:0)-(keyboard.downArrowKey.isPressed ? 1:0))*delta*0.4f,0,1);
        }
        var pointer=Pointer(out var position);
        if (pointer && Mouse.current?.leftButton.isPressed==true) { ControlX=position.x;ControlY=position.y; }
        var working=Task=="prepare" || keyboard?.spaceKey.isPressed==true || (pointer && Mouse.current?.leftButton.isPressed==true);
        var targetX=Task=="prepare" ? 0.5f+0.14f*MathF.Sin(Elapsed*0.35f) : 0.5f;
        var targetY=Task=="prepare" ? 0.5f+0.14f*MathF.Cos(Elapsed*0.29f) : 0.5f;
        var accuracy=Math.Clamp(1-(Math.Abs(ControlX-targetX)+Math.Abs(ControlY-targetY))*1.5f,0,1);
        if (working) { Elapsed+=delta;Score+=accuracy*delta; }
        Status.text=Task switch { "prepare"=>"Stabilize the shifting pattern with the two controls.","print"=>"Align to the registration cross, then hold Space or mouse to roll.",_=>"Align the guide to the registration cross, then hold Space or mouse to cut." };
        TaskStatus.text=$"{Math.Min(100,Elapsed/12*100):0}% · alignment {accuracy*100:0}%\nArrow keys adjust both axes.\nTarget: {targetX:0.00}, {targetY:0.00}\nControls: {ControlX:0.00}, {ControlY:0.00}";
        TargetMarker.anchoredPosition=new(targetX*384-15,-targetY*320);
        ControlMarker.anchoredPosition=new(ControlX*384-13,-ControlY*320);
        ProgressBar.sizeDelta=new(Math.Clamp(Elapsed/12,0,1)*530,8);
        if (StageControls is not null) StageControls.text=$"X  {ControlX:0.00} / {targetX:0.00}\nY  {ControlY:0.00} / {targetY:0.00}\n\nHold Space / mouse to advance printing and cutting.";
        if (Task=="prepare") Preview.uvRect=new((ControlX-targetX)*0.35f,(ControlY-targetY)*0.35f,1,1);
        LSDGameplayMod.Current!.World?.Animate(Equipment,Task,Math.Clamp(Elapsed/12,0,1),ControlX,ControlY);
        if (Elapsed>=12)
        {
            var task=Task;Task="";Send(new() { Action=task,Accuracy=Math.Clamp(Score/Elapsed,0,1) });
        }
    }
    private void BuildTaskControls()
    {
        ClearControls();NameInput.gameObject.SetActive(false);Guides.SetActive(true);
        TargetMarker.gameObject.SetActive(true);ControlMarker.gameObject.SetActive(true);ProgressBar.gameObject.SetActive(true);
        AddLabel(Task=="prepare" ? "CALIBRATION" : Task=="print" ? "REGISTRATION + ROLLER" : "CUTTING GUIDE",438,178,530,44,28);
        AddLabel("Bring the red cross onto the yellow cross. Precision recovers a few extra tabs; ordinary mistakes leave useful output. Quality stays Standard.",438,240,530,112);
        StageControls=StudioWidgets.Label(Dialog,"",438,360,530,150,22);Controls.Add(StageControls.gameObject);
    }
    private static RectTransform Guide(Transform parent,float x,float y,float w,float h,Color color)
    {
        var rect=StudioWidgets.Rect(parent,"Guide",x,y,w,h);var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return rect;
    }
    private Button AddButton(string text,float x,float y,float w,float h,Action action)
    { var button=StudioWidgets.Button(Dialog,text,x,y,w,h,action);Controls.Add(button.gameObject);return button; }
    private void AddLabel(string text,float x,float y,float w,float h,float size=19)
    { var label=StudioWidgets.Label(Dialog,text,x,y,w,h,size);Controls.Add(label.gameObject); }
    private void ClearControls() { foreach (var control in Controls) if (control) Object.Destroy(control);Controls.Clear(); }
    public void Dispose()
    { Close();GameInput.DeregisterExitListener(ExitHandler);if (Root) Object.Destroy(Root);if (DrawingTexture) Object.Destroy(DrawingTexture); }
}
