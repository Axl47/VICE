using Il2CppInterop.Runtime;
using Il2CppScheduleOne.UI.Compass;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace PersonalMods.LSDGameplay;

internal sealed class DeliveryGuide : IDisposable
{
    private GameObject? Root,Anchor;
    private CompassManager? Compass;
    private CompassManager.Element? Element;
    private S1API.Map.MapPOI? Map;
    internal void Tick(Transform? pallet)
    {
        if(!pallet){Dispose();return;}
        var compass=CompassManager.Instance;
        if(!compass || !compass.ElementUIContainer)return;
        if(Compass && Compass!.Pointer!=compass.Pointer)Dispose();
        if(Element is null){
            Compass=compass;Root=new GameObject("Studio delivery guide");Root.SetActive(false);
            Anchor=new GameObject("Studio pallet target");Anchor.transform.SetParent(Root.transform,false);
            var icon=new GameObject("Purple delivery star",new[]{Il2CppType.Of<RectTransform>()});icon.transform.SetParent(Root.transform,false);
            var rect=icon.GetComponent<RectTransform>();rect.sizeDelta=new(20,20);
            var image=icon.AddComponent<Image>();image.sprite=StudioAssets.DeliveryStar;image.raycastTarget=false;
            Anchor.transform.position=pallet!.position;Element=compass.AddElement(Anchor.transform,rect,true);
            Element.Group.blocksRaycasts=false;Element.Group.interactable=false;
            Map=new S1API.Map.MapPOIBuilder("personal.lsd:delivery").WithLabel("Off Register pickup").WithIcon(StudioAssets.DeliveryStar).WithPosition(pallet.position).Build();
        }
        Anchor!.transform.position=pallet!.position;compass.UpdateElement(Element);Map?.SetPosition(pallet.position);
    }
    public void Dispose()
    {
        Map?.Dispose();Map=null;
        if(Element is not null && Compass){Compass!.RemoveElement(Element,false);if(Element.Rect)Object.Destroy(Element.Rect.gameObject);}
        Element=null;Compass=null;if(Root)Object.Destroy(Root);Root=null;Anchor=null;
    }
}
