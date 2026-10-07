using Il2CppInterop.Runtime;
using Il2CppScheduleOne.UI.Shop;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PersonalMods.LSDGameplay;

internal static class StudioWidgets
{
    internal static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var root=new GameObject(name,new[] { Il2CppType.Of<RectTransform>() });
        root.layer=parent.gameObject.layer;
        var rect=root.GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new(0,1);rect.pivot=new(0,1);
        rect.anchoredPosition=new(x,-y);rect.sizeDelta=new(w,h);return rect;
    }
    internal static TextMeshProUGUI Label(Transform parent,string text,float x,float y,float w,float h,float size=19)
    {
        var rect=Rect(parent,"Label",x,y,w,h);
        var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        var source=ShopInterface.AllShops.ToArray().FirstOrDefault(s=>s && s.StoreNameLabel)?.StoreNameLabel;
        if (source) { label.font=source!.font;label.fontSharedMaterial=source.fontSharedMaterial; }
        label.text=text;label.fontSize=size;label.color=new(0.92f,0.92f,0.84f);label.richText=false;
        label.enableWordWrapping=true;label.raycastTarget=false;label.alignment=TextAlignmentOptions.TopLeft;
        return label;
    }
    internal static Button Button(Transform parent,string text,float x,float y,float w,float h,Action action)
    {
        var rect=Rect(parent,text,x,y,w,h);var image=rect.gameObject.AddComponent<Image>();
        image.color=new(0.12f,0.35f,0.32f);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new(1.3f,1.3f,1.2f);
        colors.pressedColor=new(0.7f,0.8f,0.8f);colors.disabledColor=new(0.4f,0.4f,0.4f);button.colors=colors;
        var label=Label(rect,text,8,3,w-16,h-6,17);label.alignment=TextAlignmentOptions.Midline;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>{
            try { action(); } catch (Exception error) { LSDGameplayMod.Current?.ShowMessage(error.Message); }
        })) ?? throw new InvalidOperationException("Could not bind studio control."));
        return button;
    }
    internal static TMP_InputField Input(Transform parent,string placeholder,float x,float y,float w,float h)
    {
        var rect=Rect(parent,"Edition name",x,y,w,h);var image=rect.gameObject.AddComponent<Image>();image.color=new(0.14f,0.17f,0.19f);
        var text=Label(rect,"",8,2,w-16,h-4,18);text.alignment=TextAlignmentOptions.MidlineLeft;text.enableWordWrapping=false;
        var hint=Label(rect,placeholder,8,2,w-16,h-4,18);hint.color=new(0.55f,0.61f,0.62f);hint.alignment=TextAlignmentOptions.MidlineLeft;
        var input=rect.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=image;input.textComponent=text;
        input.textViewport=rect;input.placeholder=hint;input.fontAsset=text.font;input.characterLimit=32;input.richText=false;
        input.contentType=TMP_InputField.ContentType.Standard;input.lineType=TMP_InputField.LineType.SingleLine;return input;
    }
}
