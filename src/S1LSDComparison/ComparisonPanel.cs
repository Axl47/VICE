using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PersonalMods.S1LSDComparison;

// Native uGUI controls use the game's EventSystem/InputSystem, rather than
// IMGUI's stripped GUIContent.Temp(string[]) toolbar helper.
internal sealed class ComparisonPanel : IDisposable
{
    private GameObject? Root;
    private readonly TextMeshProUGUI Status;
    private readonly Image Classic, Subtle, Peak, Timeline;
    private string LastStatus = "";
    private static readonly Color Selected = new(0.12f, 0.46f, 0.42f, 1);
    private static readonly Color Unselected = new(0.12f, 0.18f, 0.24f, 1);

    internal ComparisonPanel(Action<bool> style, Action<bool> timing, Action<int> dose, Action stop, Action close)
    {
        try {
            Root = new GameObject("S1LSD comparison controls", new[] { Il2CppType.Of<RectTransform>() });
            Root.SetActive(false);
            var canvas = Root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            var scaler = Root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            Root.AddComponent<GraphicRaycaster>();
            var backdrop = Rect(Root.transform, "Backdrop", 0, 0, 1920, 1080);
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            backdrop.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var panel = Rect(Root.transform, "Panel", 0, 0, 760, 540);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.08f, 0.99f);
            Label(panel, "S1LSD · VISUAL COMPARISON", 28, 24, 704, 40, 27);
            Label(panel, "Choose a style and timing, then click a dose to start.", 28, 70, 704, 30, 19);
            Classic = Button(panel, "CLASSIC · C", 28, 116, 340, 48, () => style(true));
            Subtle = Button(panel, "SUBTLE · S", 392, 116, 340, 48, () => style(false));
            Peak = Button(panel, "HOLD PEAK · P", 28, 180, 340, 48, () => timing(true));
            Timeline = Button(panel, "FULL TIMELINE · T", 392, 180, 340, 48, () => timing(false));
            var doses = new[] { 70, 140, 280, 360 };
            for (var i = 0; i < doses.Length; i++) {
                var value = doses[i];
                Button(panel, $"{value} µg · {i + 1}", 28 + i * 180, 250, 164, 58, () => dose(value));
            }
            Label(panel, "Full timeline: 3.5 / 5.5 / 8.5 / 11 minutes. Visuals only; audio stays dry.", 28, 324, 704, 44, 17);
            Status = Label(panel, "", 28, 379, 704, 62, 18);
            Button(panel, "STOP COMPARISON · F10", 28, 454, 340, 48, stop);
            Button(panel, "CLOSE PANEL · F6 / Esc", 392, 454, 340, 48, close);
        } catch { Dispose(); throw; }
    }

    internal void Show(bool classic, bool peak, string status)
    {
        Refresh(classic, peak, status);
        Root!.SetActive(true);
    }

    internal void Refresh(bool classic, bool peak, string status)
    {
        Classic.color = classic ? Selected : Unselected; Subtle.color = classic ? Unselected : Selected;
        Peak.color = peak ? Selected : Unselected; Timeline.color = peak ? Unselected : Selected;
        if (status != LastStatus) { LastStatus = status; Status.text = status; }
    }

    internal void Hide() { if (Root) Root!.SetActive(false); }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var root = new GameObject(name, new[] { Il2CppType.Of<RectTransform>() });
        var rect = root.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static TextMeshProUGUI Label(Transform parent, string text, float x, float y, float width, float height, float size)
    {
        var rect = Rect(parent, "Label", x, y, width, height);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text; label.fontSize = size; label.richText = false;
        label.color = new Color(0.92f, 0.96f, 0.98f); label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.TopLeft; label.enableWordWrapping = true;
        return label;
    }

    private static Image Button(Transform parent, string text, float x, float y, float width, float height, Action action)
    {
        var rect = Rect(parent, text, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>(); image.color = Unselected;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f); colors.pressedColor = new Color(0.7f, 0.8f, 0.8f);
        button.colors = colors;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        var label = Label(rect, text, 8, 4, width - 16, height - 8, 19); label.alignment = TextAlignmentOptions.Midline;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(action)
            ?? throw new InvalidOperationException("Could not bind a comparison button."));
        return image;
    }

    public void Dispose()
    {
        if (Root) { Root!.SetActive(false); Object.Destroy(Root); }
        Root = null;
    }
}
