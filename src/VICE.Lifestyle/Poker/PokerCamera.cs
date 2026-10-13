using Il2CppScheduleOne;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Poker;

// One locally owned camera. Native transform, FOV, pipeline asset and global AA are untouched.
internal sealed class PokerCamera : IDisposable
{
    private sealed record Snapshot(PlayerCamera Controller,PlayerMovement Movement,PlayerInventory Inventory,Camera Main,Camera? Overlay,bool MainEnabled,bool OverlayEnabled,
        bool Look,bool Move,bool Hotbar,bool Equipping,bool Typing,bool CursorVisible,CursorLockMode CursorLock,HUD? Hud,bool CrosshairActive,bool CrosshairEnabled);
    private readonly PresentationLease<Snapshot> Lease;
    private GameObject? Root;
    internal Camera View { get; private set; } = null!;
    internal bool Active=>Lease.Active&&View;
    internal float Fov;
    private const string UIKey="VICEPoker";
    internal static bool Available
    {
        get
        {
            var c=PlayerCamera.Instance;
            return c&&c.Camera&&c.ActiveUIElementCount==0&&!c.transformOverriden&&!c.fovOverriden&&!c.FreeCamEnabled&&!c.ViewingAvatar
                &&PlayerMovement.Instance&&PlayerInventory.Instance;
        }
    }
    internal PokerCamera()
    {
        Fov=70;
        Lease=new(Capture,Acquire,Restore);
    }
    private Snapshot Capture()
    {
        if(!Available)throw new InvalidOperationException("Close other menus or camera views before playing.");
        var c=PlayerCamera.Instance;var main=c.Camera;var overlay=c.OverlayCamera;var inv=PlayerInventory.Instance;
        var hud=HUD.Instance;var crosshair=hud?hud.crosshair:null;
        return new(c,PlayerMovement.Instance,inv,main,overlay,main.enabled,overlay&&overlay.enabled,c.CanLook,PlayerMovement.Instance._canMove,inv.HotbarEnabled,inv.EquippingEnabled,
            GameInput.IsTyping,Cursor.visible,Cursor.lockState,hud,crosshair&&crosshair!.gameObject.activeSelf,crosshair&&crosshair!.enabled);
    }
    private void Acquire()
    {
        var c=PlayerCamera.Instance;
        Root=new("VICE poker seat camera");Root.tag="MainCamera";View=Root.AddComponent<Camera>();View.enabled=false;View.CopyFrom(c.Camera);
        View.targetTexture=null;View.fieldOfView=Fov;View.nearClipPlane=.025f;
        View.rect=new Rect(0,0,1,1);View.ResetAspect();
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
        PlayerInventory.Instance.SetInventoryEnabled(false);PlayerInventory.Instance.SetEquippingEnabled(false);GameInput.IsTyping=true;
        if(HUD.Instance&&HUD.Instance.crosshair)HUD.Instance.crosshair.enabled=false;
        c.Camera.enabled=false;if(c.OverlayCamera)c.OverlayCamera.enabled=false;View.enabled=true;
    }
    internal bool Valid=>Active&&PlayerCamera.Instance&&PlayerCamera.Instance.ActiveUIElements.Contains(UIKey)
        &&PlayerCamera.Instance.ActiveUIElementCount==(PokerWallet.Locked?2:1);
    internal void Pose(Transform visual, int seat)
    {
        if(!Active || !visual)return;
        var position=PokerGeometry.Rotate(new Vector3(0,1.50f,-1.40f),seat);
        View.transform.position=visual.TransformPoint(position);
        View.transform.rotation=Quaternion.LookRotation(visual.TransformPoint(new Vector3(0,.77f,0))-View.transform.position,visual.up);
        View.fieldOfView=Fov;
    }
    private void Restore(Snapshot s)
    {
        // Always release the local rendering objects even if a native owner was destroyed at a scene boundary.
        try
        {
            if(View)View.enabled=false;
            if(s.Main)s.Main.enabled=s.MainEnabled;if(s.Overlay)s.Overlay!.enabled=s.OverlayEnabled;
            if(s.Controller)
            {
                s.Controller.RemoveActiveUIElement(UIKey);
                if(s.Controller.ActiveUIElementCount==0)s.Controller.SetCanLook(s.Look);
            }
        }
        finally
        {
            var anotherUI=s.Controller&&s.Controller.ActiveUIElementCount>0;
            var sameOwner=s.Controller&&PlayerCamera.Instance&&PlayerCamera.Instance.Pointer==s.Controller.Pointer;
            if(!anotherUI&&sameOwner)
            {
                if(s.Movement)s.Movement._canMove=s.Move;
                if(s.Inventory){s.Inventory.SetInventoryEnabled(s.Hotbar);s.Inventory.SetEquippingEnabled(s.Equipping);}
                GameInput.IsTyping=s.Typing;Cursor.visible=s.CursorVisible;Cursor.lockState=s.CursorLock;
                if(s.Controller&&s.CursorLock==CursorLockMode.Locked)s.Controller.LockMouse(true);
                Cursor.visible=s.CursorVisible;Cursor.lockState=s.CursorLock;
                if(s.Hud&&s.Hud!.crosshair){s.Hud.crosshair.gameObject.SetActive(s.CrosshairActive);s.Hud.crosshair.enabled=s.CrosshairEnabled;}
            }
            if(Root)Object.Destroy(Root);Root=null;
        }
    }
    public void Dispose()=>Lease.Dispose();
}

