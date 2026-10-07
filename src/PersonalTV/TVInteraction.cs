using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Input;
using UnityEngine;

namespace PersonalMods.TV;

internal static class TVInteraction
{
    private static readonly Dictionary<IntPtr,(CanvasGroup Group,float Alpha)> Adjusted=new();
    private static string HoveredScreen="";
    private static double HoverStarted,LastHovered;
    internal static void Install(HarmonyLib.Harmony harmony)
    {
        ModSupport.Patch(harmony,typeof(InteractionManager),"LateUpdate",typeof(TVInteraction),postfix:nameof(CenterPrompt));
    }
    // Hide only the visual prompt. The native hover target and E binding stay available.
    private static void CenterPrompt()
    {
        RestoreOpacity();
        var now=PlaybackClock.LocalNow;
        var manager=InteractionManager.Instance;
        var interaction=manager?manager.HoveredValidInteractableObject:null;
        var item=interaction?interaction!.GetComponentInParent<BuildableItem>():null;
        if(!item||item!.ItemInstance?.ID!=TVContent.ItemId)
        {
            // Brief raycast gaps while moving across the same screen should not restart its hint.
            if(now-LastHovered>.35)HoveredScreen="";
            return;
        }
        var id=item.GUID.ToString();
        if(HoveredScreen!=id){HoveredScreen=id;HoverStarted=now;}
        LastHovered=now;
        var opacity=(float)Math.Clamp((1-(now-HoverStarted))/.2,0,1);
        foreach(var rect in PromptRects())
        {
            var position=rect.position;
            position.x+=Screen.width*.5f-rect.TransformPoint(rect.rect.center).x;
            rect.position=position;
            var group=rect.GetComponent<CanvasGroup>()??rect.gameObject.AddComponent<CanvasGroup>();
            if(Adjusted.ContainsKey(group.Pointer))continue;
            Adjusted[group.Pointer]=(group,group.alpha);
            group.alpha*=opacity;
        }
    }
    private static void RestoreOpacity()
    {
        foreach(var entry in Adjusted.Values)if(entry.Group)entry.Group.alpha=entry.Alpha;
        Adjusted.Clear();
    }
    internal static void Reset(){RestoreOpacity();HoveredScreen="";HoverStarted=0;LastHovered=0;}
    internal static IEnumerable<RectTransform> PromptRects()
    {
        var manager=InputPromptsManager.Instance;
        var ui=manager?manager._inputPromptsUI:null;
        if(ui&&ui!._activePrompts is not null)
            foreach(var panel in ui._activePrompts.Values)
                if(panel&&panel.gameObject.activeInHierarchy&&panel._activePrompts.ToArray().Any(p=>p&&p._promptLabel&&p._promptLabel.text=="Use Wall Screen"))
                    yield return panel.transform.Cast<RectTransform>();
        var legacy=InteractionCanvas.Instance;
        if(legacy&&legacy.Container&&legacy.Container.gameObject.activeInHierarchy&&legacy.MessageText&&legacy.MessageText.text=="Use Wall Screen")
            yield return legacy.Container;
    }
}
