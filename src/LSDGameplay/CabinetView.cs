using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;
using Object=UnityEngine.Object;

namespace PersonalMods.LSDGameplay;

// Each placed cabinet gets its own pivots/materials; the authored model is retained.
internal sealed class CabinetView : IDisposable
{
    private readonly BuildableItem Item;
    private readonly Transform Door,Tray,Sheet;
    private readonly Renderer Handle,Lip,Artwork;
    private readonly Vector3 DoorClosed,TrayClosed,SheetRest,FaceCenter;
    private readonly Quaternion DoorRotation;
    private readonly Material Paper;
    private readonly float Swing;
    private const float TrayTravel=0.52f;
    private Vector3 CameraPosition;
    private Quaternion CameraRotation;
    private float CameraSettlesAt;
    internal bool CameraReady=>Time.realtimeSinceStartup>=CameraSettlesAt;
    internal readonly Vector3 Front;
    internal bool InUse {get;private set;}
    internal Color CheckColor=>Paper.color;
    internal void CheckGeometry(CabinetSequence sequence)
    {
        var camera=PlayerCamera.Instance.Camera;
        if(CameraReady && (Vector3.Distance(camera.transform.position,CameraPosition)>0.002f
            || Quaternion.Angle(camera.transform.rotation,CameraRotation)>0.2f))
            throw new InvalidOperationException("Cabinet camera did not reach its front or tray close-up view.");
        if(Math.Abs(Quaternion.Angle(Door.rotation,Item.transform.rotation*DoorRotation)-Math.Abs(Swing*sequence.Door))>0.2f)
            throw new InvalidOperationException("Cabinet door does not rotate around its hinge.");
        if(Vector3.Distance(Tray.position,Item.transform.TransformPoint(TrayClosed)+Front*(sequence.Tray*TrayTravel))>0.001f)
            throw new InvalidOperationException("Cabinet tray does not follow its rail.");
        if(!CameraReady)return;
        var target=sequence.Step is CabinetStep.OpenDoor or CabinetStep.CloseDoor ? Handle : sequence.Step is CabinetStep.PullTray or CabinetStep.PushTray ? Lip : Artwork;
        var screen=PlayerCamera.Instance.Camera.WorldToScreenPoint(target.bounds.center);
        if(!Hit(sequence.Step,new(screen.x,screen.y)))throw new InvalidOperationException("Cabinet part cannot be clicked in the task camera.");
        if(screen.x<0 || screen.x>Screen.width || screen.y<220f*Screen.height/1080 || screen.y>Screen.height)
            throw new InvalidOperationException($"Cabinet action {sequence.Step} is outside the task camera or behind its toolbar: {screen}.");
    }
    private bool Loading;
    internal CabinetView(BuildableItem item)
    {
        Item=item;var renderers=item.GetComponentsInChildren<Renderer>(true).ToArray();
        Handle=renderers.First(r=>r.name=="CabinetHandle");
        Artwork=renderers.First(r=>r.name.StartsWith("ArtworkSurface",StringComparison.Ordinal));
        var panel=renderers.First(r=>r.name=="CabinetDoor");
        var filter=panel.GetComponent<MeshFilter>();var size=filter.sharedMesh.bounds.size;
        var normal=filter.transform.TransformDirection(size.x<size.y && size.x<size.z ? Vector3.right : size.y<size.z ? Vector3.up : Vector3.forward);
        if(Vector3.Dot(normal,Handle.bounds.center-panel.bounds.center)<0)normal=-normal;
        Front=Vector3.ProjectOnPlane(normal,Vector3.up).normalized;
        var center=renderers.First(r=>r.name=="StaticDetails").bounds.center;
        center+=Front*Vector3.Dot(panel.bounds.center-center,Front);center.y=panel.bounds.center.y;
        FaceCenter=item.transform.InverseTransformPoint(center);
        var hinge=renderers.First(r=>r.name=="CabinetHinges");
        Door=Pivot("Studio cabinet door",hinge.bounds.center);
        foreach(var part in new[]{panel,Handle})part.transform.SetParent(Door,true);
        DoorClosed=Door.localPosition;DoorRotation=Door.localRotation;
        Swing=MathF.Sign(Vector3.Dot(Vector3.Cross(Vector3.up,Handle.bounds.center-hinge.bounds.center),Front))*105;
        var paper=renderers.First(r=>r.name=="PrintedSheet");
        var tray=renderers.First(r=>r.name=="ActiveDryingTray");
        Lip=renderers.First(r=>r.name=="ActiveTrayLip");
        Tray=Pivot("Studio cabinet sliding tray",tray.bounds.center);
        tray.transform.SetParent(Tray,true);Lip.transform.SetParent(Tray,true);TrayClosed=Tray.localPosition;
        Sheet=Pivot("Studio cabinet sheet",paper.bounds.center);
        paper.transform.SetParent(Sheet,true);Artwork.transform.SetParent(Sheet,true);
        Sheet.SetParent(Tray,true);SheetRest=Sheet.localPosition;
        Paper=StudioAssets.SurfaceMaterial(Color.white);Artwork.sharedMaterial=Paper;
        Reset();
    }
    private Transform Pivot(string name,Vector3 position)
    {
        var pivot=new GameObject(name).transform;pivot.SetParent(Item.transform,false);pivot.position=position;return pivot;
    }
    internal void Refresh(MachineState? machine)
    {
        if(InUse)return;
        Sheet.gameObject.SetActive(machine?.Stage is MachineStage.Drying or MachineStage.DryOutput);
        if(machine is null)return;
        SetArtwork(machine.ArtSlot);
        var remaining=machine.DueMinute-LSDGameplayMod.Minute;
        var dry=machine.Stage==MachineStage.DryOutput ? 1 : 1-Math.Clamp(remaining/(float)WorkshopRules.DryMinutes,0,1);
        Paper.color=Color.Lerp(new(0.60f,0.78f,1),new(1,0.94f,0.80f),dry);
    }
    private void SetArtwork(int slot)=>Paper.mainTexture=StudioAssets.SheetTexture(slot);
    internal void Begin(bool loading,int slot)
    {
        Reset();InUse=true;Loading=loading;SetArtwork(slot);
        Paper.color=loading ? new(0.60f,0.78f,1) : new(1,0.94f,0.80f);
        Sheet.gameObject.SetActive(!loading);
        Frame();
    }
    internal void Frame(bool closeup=false)
    {
        var target=Item.transform.TransformPoint(FaceCenter)+Vector3.up*0.16f;
        CameraPosition=target+Front*1.05f;
        CameraRotation=Quaternion.LookRotation(-Front,Vector3.up);
        if(closeup){
            var seated=Tray.TransformPoint(SheetRest);
            target=seated+Front*0.16f+Vector3.up*0.09f;
            CameraPosition=target+Vector3.up*0.62f+Front*0.12f;
            CameraRotation=Quaternion.LookRotation(target-CameraPosition,-Front);
        }
        CameraSettlesAt=Time.realtimeSinceStartup+0.4f;
        PlayerCamera.Instance.OverrideTransform(CameraPosition,CameraRotation,0.35f,false);
    }
    internal void Animate(CabinetSequence sequence)
    {
        Door.localPosition=DoorClosed;Door.rotation=Quaternion.AngleAxis(Swing*sequence.Door,Vector3.up)*Item.transform.rotation*DoorRotation;
        Tray.position=Item.transform.TransformPoint(TrayClosed)+Front*(sequence.Tray*TrayTravel);
        var transfer=sequence.Transfer;
        Sheet.localPosition=SheetRest;
        var seated=Sheet.position;var carried=seated+Front*0.38f+Vector3.up*0.23f;
        Sheet.position=Vector3.Lerp(Loading ? carried : seated,Loading ? seated : carried,transfer);
        Sheet.gameObject.SetActive(Loading ? sequence.Step>=CabinetStep.TransferSheet : transfer<1);
    }
    internal string Instruction(CabinetSequence sequence)=>sequence.Step switch {
        CabinetStep.OpenDoor=>"Click the door handle to open the cabinet.",
        CabinetStep.PullTray=>"Click the tray lip to slide the tray out.",
        CabinetStep.TransferSheet=>Loading ? "Click the wet sheet to place it on the tray." : "Click the dry sheet to lift it off the tray.",
        CabinetStep.PushTray=>"Click the tray lip to slide the tray back in.",
        CabinetStep.CloseDoor=>"Click the door handle to close the cabinet.",
        _=>Loading ? "Starting the drying cycle…" : "Collecting the dry sheet…" };
    internal bool Hit(CabinetStep step,Vector2 pointer)
    {
        var renderer=step is CabinetStep.OpenDoor or CabinetStep.CloseDoor ? Handle : step is CabinetStep.PullTray or CabinetStep.PushTray ? Lip : Artwork;
        var bounds=renderer.bounds;var camera=PlayerCamera.Instance.Camera;
        var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
        for(var i=0;i<8;i++){
            var p=camera.WorldToScreenPoint(bounds.center+Vector3.Scale(bounds.extents,new(i%2==0 ? -1:1,(i&2)==0 ? -1:1,(i&4)==0 ? -1:1)));
            if(p.z<=0)return false;
            min=Vector2.Min(min,new(p.x,p.y));max=Vector2.Max(max,new(p.x,p.y));
        }
        var padding=12f*Screen.height/1080f;
        return pointer.x>=min.x-padding && pointer.x<=max.x+padding && pointer.y>=min.y-padding && pointer.y<=max.y+padding;
    }
    internal void Reset()
    {
        InUse=false;Door.localPosition=DoorClosed;Door.localRotation=DoorRotation;Tray.localPosition=TrayClosed;Sheet.localPosition=SheetRest;
    }
    public void Dispose()=>Object.Destroy(Paper);
}
