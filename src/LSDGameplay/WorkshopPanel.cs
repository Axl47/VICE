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
    private readonly RectTransform Toolbar;
    private readonly TextMeshProUGUI Title,Status,Message;
    private readonly TMP_InputField NameInput;
    private readonly List<GameObject> Controls=new();
    private readonly Texture2D DrawingTexture;
    private readonly Stack<byte[]> Undo=new();
    private readonly GameInput.ExitDelegate ExitHandler;
    private BuildableItem? Equipment;
    private SheetView? Sheet;
    private CabinetView? Cabinet;
    private CabinetSequence? CabinetMotion;
    private string CabinetAction="",CabinetSheet="";
    private int CabinetArt;
    private bool CheckCabinetAdvance;
    private bool CheckExit;
    private float CabinetHeartbeat;
    private string MachineId="";
    private int SelectedArt,Brush=2,Ink=3,LastX=-1,LastY=-1,Cuts;
    private bool Editing,Repeated,Busy,PreviousLook,PreviousMove,PreviousHotbar,PreviousEquipping,PreviousTyping,Stroke,RollerStarted;
    private CutStroke? CuttingStroke;
    private byte[] Pixels=new byte[4096];
    private string Task="";
    private float Elapsed,Score,Stable,Progress,ControlX=0.28f,ControlY=0.68f,SubmittedAt,NextGuide,CutScore;
    private StudioCommand? Pending;
    private StudioCommand? PendingRelease;
    private string ReleaseAfterReply="";
    private float ReleaseSubmittedAt;
    private bool CameraOverrideInstalled;
    private int ClosedFrame=-1;
    private long SeenRevision=-1;
    internal bool IsOpen {get;private set;}
    private (Vector2 Point,bool Held,bool Space)? CheckInput;
    internal string CurrentTask=>Task;
    internal string CheckProgress=>$"task={Task}, cuts={Cuts}, progress={Progress}, elapsed={Elapsed}, busy={Busy}";
    internal void CheckDrawing(string name){StartDrawing();NameInput.text=name;}
    internal void CheckSaveDrawing()=>SaveDrawing();
    internal void CheckCommand(StudioCommand command)=>Send(command);
    internal void CheckPointer(Vector2 point,bool held,bool space)
    {if(Environment.GetCommandLineArgs().Contains("--lsd-slice-check"))CheckInput=(point,held,space);}
    internal void CheckProjection()=>Sheet!.VerifyProjection();
    internal SheetView CheckSheet=>Sheet!;
    internal CabinetSequence? CheckCabinetSequence=>CabinetMotion;
    internal CabinetView? CheckCabinetView=>Cabinet;
    internal void CheckCabinetStep(){if(Environment.GetCommandLineArgs().Contains("--lsd-slice-check"))CheckCabinetAdvance=true;}
    internal void CheckCabinetAction(string action)=>StartCabinet(action);
    internal void CheckEscape(){if(Environment.GetCommandLineArgs().Contains("--lsd-slice-check"))CheckExit=true;}
    internal string[] CheckButtons=>Controls.Select(c=>c.GetComponentInChildren<TextMeshProUGUI>().text).ToArray();
    internal int CheckArt=>SelectedArt;
    internal void CheckClick(string label)=>Controls.Single(c=>c.GetComponentInChildren<TextMeshProUGUI>().text==label).GetComponent<Button>().onClick.Invoke();
    internal bool HandleNativeExit()
    {
        if(IsOpen){Close();return false;}
        return ClosedFrame!=Time.frameCount;
    }

    internal WorkshopPanel()
    {
        Root=new GameObject("Off Register controls",new[]{Il2CppType.Of<RectTransform>()});
        var canvas=Root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;
        var scaler=Root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);
        Root.AddComponent<GraphicRaycaster>();Toolbar=StudioWidgets.Rect(Root.transform,"Bench controls",0,0,1180,182);
        Toolbar.anchorMin=Toolbar.anchorMax=Toolbar.pivot=new(0.5f,0);Toolbar.anchoredPosition=new(0,24);
        Toolbar.gameObject.AddComponent<Image>().color=new(0.035f,0.055f,0.07f,0.94f);
        Title=StudioWidgets.Label(Toolbar,"",18,8,940,32,23);Status=StudioWidgets.Label(Toolbar,"",18,42,1100,38,18);
        Message=StudioWidgets.Label(Toolbar,"",18,151,1140,26,16);
        StudioWidgets.Button(Toolbar,"Leave · Esc",1030,8,132,30,Close);
        NameInput=StudioWidgets.Input(Toolbar,"Edition name",548,86,210,34);NameInput.gameObject.SetActive(false);
        DrawingTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);DrawingTexture.filterMode=FilterMode.Point;
        ExitHandler=DelegateSupport.ConvertDelegate<GameInput.ExitDelegate>((Action<ExitAction>)(action=>{
            if(!IsOpen)return;action.used=true;Close();
        }))??throw new InvalidOperationException("Could not bind workshop exit.");
        GameInput.RegisterExitListener(ExitHandler,10);Root.SetActive(false);
    }
    internal void ShowIntroduction(){if(IsOpen || Busy || PendingRelease is not null)return;Equipment=null;MachineId="";Show();Refresh();}
    internal void OpenMachine(BuildableItem equipment)
    {
        if(IsOpen || Busy || PendingRelease is not null)return;Equipment=equipment;MachineId=equipment.GUID.ToString();
        var machine=WorkshopFeature.Current!.State.Machines.FirstOrDefault(m=>m.Id==MachineId);
        if(machine is not null && machine.Stage!=MachineStage.Empty)SelectedArt=machine.ArtSlot;
        Show();
        try {
            CameraOverrideInstalled=true;
            if(equipment.ItemInstance.ID==WorkshopRules.Bench)Sheet=new SheetView(equipment,SelectedArt);
            else {
                Cabinet=WorkshopFeature.Current!.World!.Cabinet(equipment);Cabinet.Frame();
            }
            Refresh();
        }catch{Close();throw;}
    }
    private void Show()
    {
        var camera=PlayerCamera.Instance;var inventory=PlayerInventory.Instance;
        PreviousLook=camera.CanLook;PreviousMove=PlayerMovement.Instance._canMove;
        PreviousHotbar=inventory.HotbarEnabled;PreviousEquipping=inventory.EquippingEnabled;PreviousTyping=GameInput.IsTyping;
        camera.AddActiveUIElement("OffRegister");camera.SetCanLook(false);camera.FreeMouse(true);
        PlayerMovement.Instance._canMove=false;inventory.SetInventoryEnabled(false);GameInput.IsTyping=true;
        IsOpen=true;Root.SetActive(true);Message.text="";
    }
    internal void Close()
    {
        if(!IsOpen)return;ClosedFrame=Time.frameCount;IsOpen=false;Task="";Editing=false;CheckInput=null;CheckExit=false;Root.SetActive(false);
        WorkshopFeature.Current?.World?.ResetAnimation(Equipment);Sheet?.Dispose();Sheet=null;
        Cabinet?.Reset();Cabinet=null;CabinetMotion=null;CabinetAction=CabinetSheet="";CheckCabinetAdvance=false;
        WorkshopFeature.Current?.World?.RefreshMachines(WorkshopFeature.Current.State);
        var camera=PlayerCamera.Instance;
        if(camera){
            camera.RemoveActiveUIElement("OffRegister");
            if(CameraOverrideInstalled)camera.StopTransformOverride(0.25f,PreviousLook,true);
            camera.SetCanLook(PreviousLook);if(camera.ActiveUIElementCount==0)camera.LockMouse(true);
        }
        if(PlayerMovement.Instance)PlayerMovement.Instance._canMove=PreviousMove;
        if(PlayerInventory.Instance){PlayerInventory.Instance.SetInventoryEnabled(PreviousHotbar);PlayerInventory.Instance.SetEquippingEnabled(PreviousEquipping);}
        CameraOverrideInstalled=false;GameInput.IsTyping=PreviousTyping;Equipment=null;
        if(MachineId.Length>0 && WorkshopFeature.Current?.Ready==true){
            if(Busy)ReleaseAfterReply=MachineId;else ReleaseMachine(MachineId);
        }
    }
    private void ReleaseMachine(string machine)
    {
        PendingRelease=new StudioCommand{Action="release",Machine=machine};ReleaseSubmittedAt=Time.realtimeSinceStartup;
        WorkshopNetwork.Submit(PendingRelease);
    }
    internal void ShowMessage(string message){if(Message)Message.text=message;CollectiveCatalogue.UpdateStatus();}
    private void Send(StudioCommand command)
    {
        if(Busy)return;command.Machine=MachineId;Pending=command;Busy=true;SubmittedAt=Time.realtimeSinceStartup;WorkshopNetwork.Submit(command);
    }
    private void StartCabinet(string action)
    {
        if(Busy || CabinetMotion is not null || Cabinet is null)return;
        CabinetSheet="";CabinetArt=SelectedArt;
        if(action=="dry"){
            var slots=InventoryTransaction.RegularSlots();var selected=PlayerInventory.Instance.EquippedSlotIndex;
            var items=slots.OrderByDescending(s=>selected>=0 && selected<slots.Length && s.Pointer==slots[selected].Pointer);
            var wet=items.FirstOrDefault(s=>!s.IsRemovalLocked && s.ItemInstance is {} item && WorkshopRules.TrySheet(item.ID,out var dry,out _,out _) && !dry)?.ItemInstance;
            if(wet is null){ShowMessage("Carry a wet printed sheet from the bench.");return;}
            CabinetSheet=wet.ID;WorkshopRules.TrySheet(wet.ID,out _,out CabinetArt,out _);
        }
        CabinetAction=action;
        Send(new(){Action="cabinetbegin",Name=CabinetSheet});
    }
    internal void OnReply(StudioReply reply)
    {
        if(PendingRelease is {} release && reply.Token==release.Token){PendingRelease=null;return;}
        var command=Pending;
        if(command is not null && reply.Token==command.Token){
            Busy=false;Pending=null;
            if(ReleaseAfterReply.Length>0){
                var machine=ReleaseAfterReply;ReleaseAfterReply="";ReleaseMachine(machine);
            }
            if(!IsOpen)return;
            if(command.Action=="cabinetbegin"){
                if(reply.Success && Cabinet is not null){CabinetMotion=new();CabinetHeartbeat=Time.realtimeSinceStartup;Cabinet.Begin(CabinetAction=="dry",CabinetArt);Cabinet.Animate(CabinetMotion);}
                else CabinetAction=CabinetSheet="";
            }
            if(CabinetMotion?.ReadyToCommit==true && command.Action==CabinetAction){
                CabinetMotion=null;CabinetAction=CabinetSheet="";Cabinet?.Reset();WorkshopFeature.Current!.World!.RefreshMachines(WorkshopFeature.Current.State);
            }
            if(reply.Success && command.Action=="introduce"){Close();CollectiveCatalogue.OpenCatalogue();return;}
            if(reply.Success && command.Action=="draw"){Editing=false;SelectedArt=WorkshopFeature.Current!.State.Artworks.Last().Slot;}
            if(reply.Success && command.Action is "start" or "prepare" or "cut" or "resume"){
                var machine=WorkshopFeature.Current!.State.Machines.FirstOrDefault(m=>m.Id==MachineId);
                SelectedArt=machine?.ArtSlot??SelectedArt;
                Task=machine?.Stage switch{MachineStage.Preparing=>"prepare",MachineStage.Printing=>"print",MachineStage.Cutting=>"finishcut",_=>""};
                Elapsed=Score=Stable=Progress=CutScore=0;Cuts=0;CuttingStroke=null;RollerStarted=false;ControlX=0.28f;ControlY=0.68f;
                if(Sheet is not null){Sheet.SetArtwork(SelectedArt);Sheet.Frame(Task=="finishcut");}
            }
            Refresh();
        }
        CollectiveCatalogue.UpdateStatus();
    }
    internal void Refresh()
    {
        if(!IsOpen)return;ClearControls();NameInput.gameObject.SetActive(Editing);
        var state=WorkshopFeature.Current!.State;SeenRevision=state.Revision;
        if(MachineId.Length==0){
            Title.text="OFF REGISTER";Status.text="Independent printers. Find your first edition in the Studio app.";
            Button(state.Introduced ? "Open Studio":"Meet the collective",18,86,250,36,()=>{
                if(state.Introduced){Close();CollectiveCatalogue.OpenCatalogue();}else Send(new(){Action="introduce"});
            });return;
        }
        var machine=state.Machines.FirstOrDefault(m=>m.Id==MachineId);var stage=machine?.Stage??MachineStage.Empty;
        if(machine is not null && stage!=MachineStage.Empty)SelectedArt=machine.ArtSlot;
        var art=state.Artworks.FirstOrDefault(a=>a.Slot==SelectedArt)??state.Artworks[0];SelectedArt=art.Slot;
        Title.text=Equipment?.ItemInstance.ID==WorkshopRules.Cabinet ? "DRYING CABINET":"BLOTTER BENCH · "+art.Name;
        if(CabinetMotion is not null){
            Status.text=Cabinet!.Instruction(CabinetMotion);Message.text="Space: next action · Esc: leave · The sheet transfers when the door closes.";return;
        }
        if(Task.Length>0){
            Status.text=Task=="prepare" ? "Drag the red registration mark onto the green cross and hold it steady. Arrow keys also adjust the sheet."
                :Task=="print" ? "Drag the roller from the lower edge to the upper edge. Space rolls it steadily as an alternative."
                :"Drag along each glowing cut line from its start to its end. Space is an alternative.";
            Message.text="Esc: leave and keep the batch";return;
        }
        if(Editing){BuildDrawing();return;}
        Sheet?.SetArtwork(SelectedArt);Sheet?.ClearGuides();
        Status.text=stage switch{
            MachineStage.Empty=>"Choose an edition, draw on the paper, or create your next batch.",
            MachineStage.Preparing=>"Sheet loaded. Register it on the lightbox.",MachineStage.Printing=>"Sheet registered. Roll the artwork onto the paper.",
            MachineStage.WetOutput=>$"Wet sheet ready · {machine!.Yield} tabs. Collect it and load the cabinet.",
            MachineStage.Drying=>$"Drying · {Math.Max(0,machine!.DueMinute-WorkshopFeature.Minute)} game minutes",
            MachineStage.DryOutput=>"Dry sheet ready. Collect it and return to the cutter.",MachineStage.Cutting=>"Dry sheet loaded. Follow the cut guides.",
            _=>$"{machine!.Yield} tabs ready. Collect and package them."};
        if(Equipment?.ItemInstance.ID==WorkshopRules.Cabinet){
            if(stage==MachineStage.Empty){Status.text="Empty tray. Load a wet sheet, then close the cabinet to start drying.";Button("Load wet sheet",18,86,250,36,()=>StartCabinet("dry"));}
            if(stage==MachineStage.DryOutput)Button("Collect dry sheet",18,86,250,36,()=>StartCabinet("collect"));return;
        }
        if(stage is MachineStage.Preparing or MachineStage.Printing or MachineStage.Cutting){Button("Continue at the sheet",18,86,250,36,()=>Send(new(){Action="resume"}));return;}
        if(stage is MachineStage.WetOutput or MachineStage.TabOutput){Button("Collect output",18,86,250,36,()=>Send(new(){Action="collect"}));return;}
        Button("Edition ▶",18,86,170,36,()=>Select(1));Button("Draw",198,86,140,36,StartDrawing);
        Button("Create batch",348,86,180,36,()=>Send(new(){Action="start",ArtSlot=SelectedArt}));Button("Load dry sheet",538,86,190,36,()=>Send(new(){Action="cut"}));
        Message.text="Package finished tabs at a packaging station: baggie · 1, jar · 5, edition sleeve · 20.";
    }
    private void Select(int direction)
    {var art=WorkshopFeature.Current!.State.Artworks;var index=art.FindIndex(a=>a.Slot==SelectedArt);SelectedArt=art[(index+direction+art.Count)%art.Count].Slot;Refresh();}
    private void StartDrawing()
    {
        var art=WorkshopFeature.Current!.State.Artworks.Single(a=>a.Slot==SelectedArt);Pixels=(byte[])art.Pixels.Clone();Repeated=art.Repeated;
        Editing=true;NameInput.text="";Undo.Clear();Sheet?.Frame(false);Upload();Refresh();
    }
    private void BuildDrawing()
    {
        Title.text="DRAW YOUR EDITION";Status.text="Paint directly on the physical paper. Each saved revision keeps printed stock unchanged.";
        for(var i=0;i<8;i++){var color=i;var b=Button(i==0 ? "Erase":"●",18+i*54,86,48,34,()=>Ink=color);b.targetGraphic.color=StudioAssets.Palette[i];}
        Button("−",460,86,34,34,()=>{Brush=Math.Max(1,Brush-1);Refresh();});Button("+",504,86,34,34,()=>{Brush=Math.Min(5,Brush+1);Refresh();});
        Button(Repeated ? "Repeated motif":"Full design",768,86,150,34,()=>{Repeated=!Repeated;Refresh();});
        Button("Undo",928,86,80,34,()=>{if(Undo.Count>0){Pixels=Undo.Pop();Upload();}});Button("Save edition",1018,86,144,34,SaveDrawing);
        Message.text=$"Brush {Brush} · use Save to keep the drawing · Esc discards unsaved strokes";
    }
    private void SaveDrawing()=>Send(new(){Action="draw",Name=string.IsNullOrWhiteSpace(NameInput.text) ? "Test edition":NameInput.text,Pixels=Pixels,Repeated=Repeated});
    private void Upload(){DrawingTexture.SetPixels32(new Il2CppStructArray<Color32>(Pixels.Select(p=>StudioAssets.Palette[p]).ToArray()));DrawingTexture.Apply();Sheet?.SetDrawing(DrawingTexture);}
    internal void Tick()
    {
        // Reliable requests still need retries after a panel is closed: the host can be saving or rate limiting.
        if(Busy && Time.realtimeSinceStartup-SubmittedAt>8){SubmittedAt=Time.realtimeSinceStartup;if(Pending is not null)WorkshopNetwork.Submit(Pending);}
        if(PendingRelease is {} release && Time.realtimeSinceStartup-ReleaseSubmittedAt>8){ReleaseSubmittedAt=Time.realtimeSinceStartup;WorkshopNetwork.Submit(release);}
        if(!IsOpen)return;
        // Read Esc directly: native exit dispatch can be suppressed while this panel owns typing input.
        if(CheckExit || Keyboard.current?.escapeKey.wasPressedThisFrame==true){Close();return;}
        if(!Player.Local || Player.Local.IsUnconscious || !Player.Local.Health.IsAlive || (MachineId.Length>0 && !Equipment) || (Equipment && Vector3.Distance(Player.Local.transform.position,Equipment!.transform.position)>4)){Close();return;}
        if(CabinetMotion is not null){CabinetTick();return;}
        if(Editing){DrawTick();return;}if(Task.Length>0){TaskTick();return;}
        if(SeenRevision!=WorkshopFeature.Current!.State.Revision)Refresh();
        var machine=WorkshopFeature.Current.State.Machines.FirstOrDefault(m=>m.Id==MachineId);
        if(machine?.Stage==MachineStage.Drying)Status.text=$"Drying · {Math.Max(0,machine.DueMinute-WorkshopFeature.Minute)} game minutes";
    }
    private void CabinetTick()
    {
        if(Busy || Cabinet is null || CabinetMotion is null || Time.timeScale<=0 || (!Application.isFocused && !Environment.GetCommandLineArgs().Contains("--lsd-slice-check")))return;
        var motion=CabinetMotion;var step=motion.Step;
        if(Cabinet.CameraReady && !motion.Moving && (CheckCabinetAdvance || Keyboard.current?.spaceKey.wasPressedThisFrame==true
            || (Mouse.current?.leftButton.wasPressedThisFrame==true && Cabinet.Hit(step,Mouse.current.position.ReadValue()))))motion.Advance();
        CheckCabinetAdvance=false;motion.Tick(Math.Min(Time.unscaledDeltaTime,0.1f));Cabinet.Animate(motion);
        if(step!=motion.Step){
            if(motion.Step==CabinetStep.TransferSheet || step==CabinetStep.TransferSheet)Cabinet.Frame(motion.Step==CabinetStep.TransferSheet);
            Refresh();
        }
        if(motion.ReadyToCommit)Send(new(){Action=CabinetAction,Name=CabinetSheet});
        else if(Time.realtimeSinceStartup-CabinetHeartbeat>20){CabinetHeartbeat=Time.realtimeSinceStartup;Send(new(){Action="cabinetkeepalive"});}
    }
    private void DrawTick()
    {
        if(Sheet is null)return;
        var inside=Sheet.Pointer(out var p);var held=Mouse.current?.leftButton.isPressed==true;
        if(CheckInput is {} input){p=input.Point;held=input.Held;inside=true;}
        if(!held || !inside){Stroke=false;LastX=LastY=-1;return;}
        if(!Stroke){if(Undo.Count<16)Undo.Push((byte[])Pixels.Clone());Stroke=true;}
        var x=Math.Clamp((int)(p.x*64),0,63);var y=Math.Clamp((int)(p.y*64),0,63);
        var steps=LastX<0 ? 1:Math.Max(1,Math.Max(Math.Abs(x-LastX),Math.Abs(y-LastY)));var ox=LastX<0 ? x:LastX;var oy=LastY<0 ? y:LastY;
        for(var n=0;n<=steps;n++){
            var cx=ox+(x-ox)*n/steps;var cy=oy+(y-oy)*n/steps;
            for(var dy=-Brush;dy<=Brush;dy++)for(var dx=-Brush;dx<=Brush;dx++)if(dx*dx+dy*dy<=Brush*Brush && cx+dx is >=0 and <64 && cy+dy is >=0 and <64)Pixels[(cy+dy)*64+cx+dx]=(byte)Ink;
        }
        LastX=x;LastY=y;Upload();
    }
    private void TaskTick()
    {
        if(Busy || Sheet is null || Time.timeScale<=0 || (!Application.isFocused && !Environment.GetCommandLineArgs().Contains("--lsd-slice-check")))return;
        var delta=Math.Min(Time.unscaledDeltaTime,0.1f);Elapsed+=delta;var keyboard=Keyboard.current;
        Sheet.UpdateControlDirections();
        var pointer=Sheet.Pointer(out var point,Task=="finishcut" ? float.PositiveInfinity:Task=="print" ? 0.12f:0);var held=Mouse.current?.leftButton.isPressed==true && pointer;
        var space=keyboard?.spaceKey.isPressed==true;
        if(CheckInput is { } input){point=input.Point;held=input.Held;space=input.Space;}
        if(Task=="prepare"){
            ControlX=Math.Clamp(ControlX+((keyboard?.rightArrowKey.isPressed==true ? 1:0)-(keyboard?.leftArrowKey.isPressed==true ? 1:0))*delta*0.45f*Sheet.ScreenXSign,0,1);
            ControlY=Math.Clamp(ControlY+((keyboard?.upArrowKey.isPressed==true ? 1:0)-(keyboard?.downArrowKey.isPressed==true ? 1:0))*delta*0.45f*Sheet.ScreenYSign,0,1);
            if(held){ControlX=point.x;ControlY=point.y;}
            var accuracy=Math.Clamp(1-(Math.Abs(ControlX-0.5f)+Math.Abs(ControlY-0.5f))*3,0,1);Stable=accuracy>0.85f ? Stable+delta:Math.Max(0,Stable-delta);Score=accuracy;
            Message.text=$"Registration {accuracy*100:0}% · hold steady {Math.Clamp(Stable/2.5f,0,1)*100:0}%";
            if(Time.realtimeSinceStartup>NextGuide){NextGuide=Time.realtimeSinceStartup+0.05f;Sheet.Registration(new(ControlX,ControlY));}
            if(Stable>=2.5f)Finish(Score);
        }else if(Task=="print"){
            if(Elapsed<0.3f)return; // Let the camera settle before choosing the screen's lower edge.
            Sheet.BeginPrinting();var coverage=Sheet.PrintProgress(point.y);
            if(held && coverage<0.18f)RollerStarted=true;if(held && RollerStarted)Progress=Math.Clamp(Math.Max(Progress,coverage),0,1);
            if(space){RollerStarted=true;Progress=Math.Min(1,Progress+delta*0.28f);}
            Message.text=$"Roller coverage {Progress*100:0}%";
            if(Time.realtimeSinceStartup>NextGuide){NextGuide=Time.realtimeSinceStartup+0.05f;Sheet.Roller(Progress);Sheet.Print(SelectedArt,Progress);}
            WorkshopFeature.Current!.World?.Animate(Equipment,Task,Progress,Sheet);if(Progress>=0.97f && Elapsed>=3)Finish(1);
        }else{
            var(a,b)=SheetView.CutLine(Cuts);CuttingStroke??=new(a.x,a.y,b.x,b.y);
            if(space)CuttingStroke.Advance(delta);else if(pointer || CheckInput is not null)CuttingStroke.Sample(point.x,point.y,held,delta);
            Progress=CuttingStroke.Progress;
            Message.text=$"Cut {Cuts+1}/11 · follow the pink line";
            if(Time.realtimeSinceStartup>NextGuide){NextGuide=Time.realtimeSinceStartup+0.05f;Sheet.CutGuide(Cuts,Progress);}
            WorkshopFeature.Current!.World?.Animate(Equipment,Task,Progress,Sheet,Cuts);
            if(CuttingStroke.Complete){CutScore+=CuttingStroke.Accuracy;Cuts++;CuttingStroke=null;Progress=0;if(Cuts>=11)Finish(CutScore/11);}
        }
    }
    private void Finish(float accuracy){var task=Task;Task="";Sheet?.ClearGuides();Send(new(){Action=task,Accuracy=Math.Clamp(accuracy,0,1)});}
    private Button Button(string text,float x,float y,float w,float h,Action action){var b=StudioWidgets.Button(Toolbar,text,x,y,w,h,action);Controls.Add(b.gameObject);return b;}
    private void ClearControls(){foreach(var control in Controls)if(control)Object.Destroy(control);Controls.Clear();}
    public void Dispose(){Close();GameInput.DeregisterExitListener(ExitHandler);Object.Destroy(Root);Object.Destroy(DrawingTexture);}
}
