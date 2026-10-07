using Il2CppInterop.Runtime;
using Il2CppScheduleOne.UI.Shop;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VICE.Lifestyle.Television;

internal static class TVWidgets
{
    internal static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var root=new GameObject(name,new[]{Il2CppType.Of<RectTransform>()});root.layer=parent.gameObject.layer;
        var rect=root.GetComponent<RectTransform>();rect.SetParent(parent,false);Place(rect,x,y,w,h);return rect;
    }
    internal static void Place(RectTransform rect,float x,float y,float w,float h)
    {rect.anchorMin=rect.anchorMax=new(0,1);rect.pivot=new(0,1);rect.anchoredPosition=new(x,-y);rect.sizeDelta=new(w,h);}
    internal static TextMeshProUGUI Label(Transform parent,string text,float x,float y,float w,float h,float size=20)
    {
        var rect=Rect(parent,"Text",x,y,w,h);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        var source=ShopInterface.AllShops.ToArray().FirstOrDefault(s=>s&&s.StoreNameLabel)?.StoreNameLabel;
        if(source){label.font=source!.font;label.fontSharedMaterial=source.fontSharedMaterial;}
        label.text=text;label.fontSize=size;label.color=new(.9f,.93f,.96f);label.richText=false;label.raycastTarget=false;
        label.enableWordWrapping=true;label.alignment=TextAlignmentOptions.MidlineLeft;return label;
    }
    internal static Button Button(Transform parent,string text,float x,float y,float w,float h,Action action)
    {
        var rect=Rect(parent,text,x,y,w,h);var image=rect.gameObject.AddComponent<Image>();image.color=new(.12f,.27f,.38f);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var label=Label(rect,text,8,2,w-16,h-4,18);label.alignment=TextAlignmentOptions.Midline;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>{
            try{action();}catch(Exception error){TelevisionFeature.Current?.Notice(TVNetwork.SafeError(error));}
        }))!);return button;
    }
    internal static TMP_InputField Input(Transform parent,string placeholder,float x,float y,float w,float h,int limit=256,bool password=false)
    {
        var rect=Rect(parent,"Input",x,y,w,h);var image=rect.gameObject.AddComponent<Image>();image.color=new(.12f,.16f,.20f);
        var text=Label(rect,"",8,2,w-16,h-4,18);text.enableWordWrapping=false;
        var hint=Label(rect,placeholder,8,2,w-16,h-4,18);hint.color=new(.5f,.6f,.7f);
        var input=rect.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=image;input.textComponent=text;
        input.textViewport=rect;input.placeholder=hint;input.fontAsset=text.font;input.characterLimit=limit;input.richText=false;
        input.contentType=password?TMP_InputField.ContentType.Password:TMP_InputField.ContentType.Standard;
        input.lineType=TMP_InputField.LineType.SingleLine;return input;
    }
    internal static Slider Slider(Transform parent,float x,float y,float w,Action<float> changed)
    {
        var rect=Rect(parent,"Slider",x,y,w,24);var background=rect.gameObject.AddComponent<Image>();background.color=new(.11f,.16f,.22f);
        var fill=Rect(rect,"Fill",0,0,w,24);fill.gameObject.AddComponent<Image>().color=new(.19f,.52f,.68f);
        fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.pivot=new(.5f,.5f);fill.sizeDelta=Vector2.zero;fill.anchoredPosition=Vector2.zero;
        var area=Rect(rect,"Handle area",0,0,w,24);area.anchorMin=Vector2.zero;area.anchorMax=Vector2.one;area.pivot=new(.5f,.5f);area.sizeDelta=new(-14,0);area.anchoredPosition=Vector2.zero;
        var handle=Rect(area,"Handle",0,0,14,24);handle.anchorMin=new(0,0);handle.anchorMax=new(0,1);handle.pivot=new(.5f,.5f);handle.sizeDelta=new(14,0);handle.anchoredPosition=Vector2.zero;
        var graphic=handle.gameObject.AddComponent<Image>();graphic.color=new(.80f,.91f,.97f);
        var slider=rect.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=graphic;
        slider.minValue=0;slider.maxValue=1;slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<float>>(changed)!);return slider;
    }
}
