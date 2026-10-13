using Il2CppScheduleOne;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Amenities;

// One locally owned camera. Native transform, FOV, pipeline asset and global AA are untouched.
internal sealed class AmenityCameraRig : IDisposable
{
    private sealed record Snapshot(PlayerCamera Controller,PlayerMovement Movement,PlayerInventory Inventory,Camera Main,Camera? Overlay,bool MainEnabled,bool OverlayEnabled,
        bool Look,bool Move,bool Hotbar,bool Equipping,bool Typing,bool CursorVisible,CursorLockMode CursorLock,HUD? Hud,bool CrosshairActive,bool CrosshairEnabled);
    private readonly PresentationLease<Snapshot> Lease;
    private GameObject? Root;
    private Camera? NativeMain;
    private PlayerCamera? NativeController;
    internal Camera View { get; private set; } = null!;
    internal bool Active=>Lease.Active&&View;
    internal bool Overhead;
    internal float Fov;
    private const string UIKey="VICEHomeGames";
    private static readonly List<DeferredPresentationRestore<Snapshot>> Pending=new();
    private static bool Overridden(PlayerCamera c)=>c.transformOverriden||c.fovOverriden||c.FreeCamEnabled||c.ViewingAvatar;
    internal static bool Available
    {
        get
        {
            PumpRestoration();
            var c=PlayerCamera.Instance;
            return c&&c.Camera&&c.ActiveUIElementCount==0&&!Overridden(c)
                &&PlayerMovement.Instance&&PlayerInventory.Instance;
        }
    }
    internal AmenityCameraRig(string kind)
    {
        Fov=kind=="darts"?22:64;
        Lease=new(Capture,Acquire,Restore);
    }
    private Snapshot Capture()
    {
        if(!Available)throw new InvalidOperationException("Close other menus or camera views before playing.");
        var c=PlayerCamera.Instance;var main=c.Camera;var overlay=c.OverlayCamera;var inv=PlayerInventory.Instance;
        NativeMain=main;NativeController=c;
        var hud=HUD.Instance;var crosshair=hud?hud.crosshair:null;
        return new(c,PlayerMovement.Instance,inv,main,overlay,main.enabled,overlay&&overlay.enabled,c.CanLook,PlayerMovement.Instance._canMove,inv.HotbarEnabled,inv.EquippingEnabled,
            GameInput.IsTyping,Cursor.visible,Cursor.lockState,hud,crosshair&&crosshair!.gameObject.activeSelf,crosshair&&crosshair!.enabled);
    }
    private void Acquire()
    {
        var c=PlayerCamera.Instance;
        Root=new("VICE home-games world camera");Root.tag="MainCamera";View=Root.AddComponent<Camera>();View.enabled=false;View.CopyFrom(c.Camera);
        View.targetTexture=null;View.fieldOfView=Fov;View.nearClipPlane=.025f;
        View.rect=new Rect(0,.18f,1,.82f);View.ResetAspect(); // Keep the near pockets above the score/control strip.
        View.ResetWorldToCameraMatrix();View.ResetProjectionMatrix();
        var source=c.Camera.GetComponent<UniversalAdditionalCameraData>();var data=Root.AddComponent<UniversalAdditionalCameraData>();
        data.renderType=CameraRenderType.Base;
        if(source)
        {
            data.SetRenderer(source.m_RendererIndex);data.antialiasing=source.antialiasing;data.antialiasingQuality=source.antialiasingQuality;
            data.renderPostProcessing=source.renderPostProcessing;data.renderShadows=source.renderShadows;data.volumeLayerMask=source.volumeLayerMask;
            data.volumeTrigger=source.volumeTrigger;data.dithering=source.dithering;data.stopNaN=source.stopNaN;
            data.requiresDepthOption=source.requiresDepthOption;data.requiresColorOption=source.requiresColorOption;
            data.m_TaaSettings=source.m_TaaSettings;
        }
        c.AddActiveUIElement(UIKey);c.SetCanLook(false);c.FreeMouse(true);PlayerMovement.Instance._canMove=false;
        if(HUD.Instance&&HUD.Instance.crosshair)HUD.Instance.crosshair.enabled=false;
        PlayerInventory.Instance.SetInventoryEnabled(false);PlayerInventory.Instance.SetEquippingEnabled(false);GameInput.IsTyping=true;
        c.Camera.enabled=false;if(c.OverlayCamera)c.OverlayCamera.enabled=false;View.enabled=true;
    }
    internal bool Valid=>Active&&NativeMain&&!NativeMain!.enabled&&NativeController&&PlayerCamera.Instance&&PlayerCamera.Instance.Pointer==NativeController!.Pointer
        &&PlayerCamera.Instance.Camera&&PlayerCamera.Instance.Camera.Pointer==NativeMain.Pointer&&!Overridden(PlayerCamera.Instance)&&PlayerCamera.Instance.ActiveUIElements.Contains(UIKey)
        &&PlayerCamera.Instance.ActiveUIElementCount==1;
    internal void Pose(Transform visual,string kind)
    {
        if(!Active||!visual)return;
        var pos=kind=="darts"?new Vector3(0,0,2.37f):Overhead?new Vector3(0,2.45f,0):new Vector3(0,2.45f,-2.15f);
        var focus=kind=="darts"?new Vector3(0,0,.055f):new Vector3(0,.78f,.05f);
        View.transform.position=visual.TransformPoint(pos);
        View.transform.rotation=Quaternion.LookRotation(visual.TransformPoint(focus)-View.transform.position,Overhead&&kind=="pool"?visual.forward:visual.up);
        View.fieldOfView=Fov;
        View.orthographic=Overhead&&kind=="pool";if(View.orthographic)View.orthographicSize=(float)WorldPlayGeometry.OverheadSize(Fov);
    }
    private void Restore(Snapshot s)
    {
        // Always release the local rendering objects even if a native owner was destroyed at a scene boundary.
        try
        {
            if(View)View.enabled=false;
            if(!PlayerCamera.Instance||PlayerCamera.Instance.Camera&&PlayerCamera.Instance.Camera.Pointer==s.Main.Pointer)
            {if(s.Main)s.Main.enabled=s.MainEnabled;if(s.Overlay)s.Overlay!.enabled=s.OverlayEnabled;}
        }
        finally
        {
            try
            {
                try{if(s.Controller)s.Controller.RemoveActiveUIElement(UIKey);}
                catch(Exception error){ModSupport.WarnOnce("amenities:ui-release",error.Message);}
                var deferred=new DeferredPresentationRestore<Snapshot>(s,()=>SameOwner(s),()=>CanRestore(s),RestoreControls);
                deferred.Pump();if(deferred.Pending)Pending.Add(deferred);
            }
            finally{if(Root)Object.Destroy(Root);Root=null;}
        }
    }
    private static bool SameOwner(Snapshot s)=>s.Controller&&PlayerCamera.Instance&&PlayerCamera.Instance.Pointer==s.Controller.Pointer;
    private static bool CanRestore(Snapshot s)=>SameOwner(s)&&s.Controller.ActiveUIElementCount==0&&!Overridden(s.Controller)
        &&s.Controller.Camera&&s.Controller.Camera.Pointer==s.Main.Pointer
        &&!(PauseMenu.Instance&&PauseMenu.Instance.IsPaused)&&Player.Local&&Player.Local.Health&&Player.Local.Health.IsAlive&&!Player.Local.IsSleeping;
    private static void RestoreControls(Snapshot s)
    {
        // A stale/destroyed native owner must not prevent the remaining owned flags from releasing.
        foreach(var release in new Action[]{
            ()=>{if(s.Main)s.Main.enabled=s.MainEnabled;if(s.Overlay)s.Overlay!.enabled=s.OverlayEnabled;},
            ()=>{if(s.Controller)s.Controller.SetCanLook(s.Look);},
            ()=>{if(s.Movement)s.Movement._canMove=s.Move;},
            ()=>{if(s.Inventory)s.Inventory.SetInventoryEnabled(s.Hotbar);},
            ()=>{if(s.Inventory)s.Inventory.SetEquippingEnabled(s.Equipping);},
            ()=>GameInput.IsTyping=s.Typing,
            ()=>{if(s.Controller&&s.CursorLock==CursorLockMode.Locked)s.Controller.LockMouse(true);},
            ()=>{Cursor.visible=s.CursorVisible;Cursor.lockState=s.CursorLock;},
            ()=>{if(s.Hud&&s.Hud!.crosshair){s.Hud.crosshair.gameObject.SetActive(s.CrosshairActive);s.Hud.crosshair.enabled=s.CrosshairEnabled;}}})
            try{release();}catch(Exception error){ModSupport.WarnOnce("amenities:control-release:"+error.GetType().Name,error.Message);}
    }
    internal static void PumpRestoration(){foreach(var pending in Pending.ToArray())pending.Pump();Pending.RemoveAll(p=>!p.Pending);}
    internal static void ClearPending()=>Pending.Clear();
    public void Dispose()=>Lease.Dispose();
}
