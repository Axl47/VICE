using Il2CppInterop.Runtime;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Map;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PersonalMods.UIEnhancements;

// Only UI primitives are copied. Cloning MapApp/POI behaviours would register
// duplicate map items, listeners and navigation state on the native phone map.
internal sealed class PlayerMinimap : IDisposable
{
    private readonly HUD Hud;
    private readonly MapApp Map;
    private readonly RectTransform Frame, Viewport, Content, Artwork, PlayerArrow;
    private readonly Image MapImage, ArrowImage;
    private readonly Dictionary<IntPtr, MinimapPin> Pins = new();
    private float NextRefresh;
    private bool Disposed;

    internal PlayerMinimap(HUD hud, MapApp map)
    {
        Hud = hud; Map = map;
        Frame = Rect(hud.canvasRect, "Personal square minimap", Vector2.zero, Vector2.one);
        Frame.gameObject.SetActive(false);
        try
        {
            Frame.anchorMin = Frame.anchorMax = Frame.pivot = Vector2.one;
            Frame.anchoredPosition = new(-24, -24);
            Image(Frame, new(0.06f, 0.08f, 0.10f, 0.96f));
            var group = Frame.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            Viewport = Rect(Frame, "Map viewport", Vector2.zero, Vector2.one);
            Image(Viewport, new(0.12f, 0.16f, 0.19f, 1));
            Viewport.gameObject.AddComponent<RectMask2D>();
            Content = Rect(Viewport, "Map projection", Vector2.zero, Vector2.zero);
            Artwork = Rect(Content, "Native map artwork", Vector2.zero, Vector2.one);
            MapImage = Image(Artwork, Color.white);
            MapImage.type = UnityEngine.UI.Image.Type.Simple;
            PlayerArrow = Rect(Viewport, "Local player", Vector2.zero, new(14, 14));
            ArrowImage = Image(PlayerArrow, new(0.35f, 0.85f, 1, 1));
            // A native player sprite is used when available; the fallback diamond
            // still keeps the exact center visible before its POI initializes.
            PlayerArrow.localRotation = Quaternion.Euler(0, 0, 45);
        }
        catch { Object.Destroy(Frame.gameObject); throw; }
    }

    internal bool BelongsTo(HUD hud, MapApp map) => !Disposed && Frame && Hud && Map
        && Hud.Pointer == hud.Pointer && Map.Pointer == map.Pointer;

    internal static bool Ready => UIEnhancementsMod.Enabled.Value && UIEnhancementsMod.MinimapEnabled.Value
        && LoadManager.InstanceExists && LoadManager.Instance.IsGameLoaded && !LoadManager.Instance.IsLoading
        && Player.Local && Player.Local.PlayerLoaded && HUD.InstanceExists && HUD.Instance.canvasRect
        && MapApp.InstanceExists && MapApp.Instance.BackgroundImage && MapApp.Instance.PoIContainer
        && MapPositionUtility.InstanceExists && MapPositionUtility.Instance.OriginPoint;

    internal void Tick()
    {
        if (Disposed || !Frame) return;
        var player = Player.Local;
        if (!Ready || !Hud || !Map || !player) { Frame.gameObject.SetActive(false); return; }
        var visible = Hud.canvas && Hud.canvas.enabled && Hud.canvas.gameObject.activeInHierarchy
            && !(PauseMenu.InstanceExists && PauseMenu.Instance.IsPaused)
            && !(Phone.InstanceExists && Phone.Instance.IsOpen) && !Map.isOpen;
        Frame.gameObject.SetActive(visible);
        if (!visible) return;
        var projection = MapPositionUtility.Instance;
        var center = projection.GetMapPosition(player.transform.position);
        var factor = Math.Abs(projection.conversionFactor);
        if (!Finite(center) || !float.IsFinite(factor) || factor < 0.0001f) { Frame.gameObject.SetActive(false); return; }
        var size = Safe(UIEnhancementsMod.MinimapSize.Value, 240, 160, 480);
        var range = Safe(UIEnhancementsMod.MinimapRange.Value, 160, 60, 500);
        Frame.sizeDelta = new(size + 6, size + 6);
        Viewport.sizeDelta = new(size, size);
        var scale = size / (range * factor);
        Content.localScale = new(scale, scale, 1);

        if (Time.realtimeSinceStartup >= NextRefresh) RefreshPins(player);
        // Native POIs use GetMapPosition as their anchoredPosition. Account for
        // their prefab anchors as well as the map image's own pivot/hierarchy.
        var anchor = MapAnchor();
        Content.anchoredPosition = -(center + anchor) * scale;
        UpdateArtwork();
        foreach (var pair in Pins.ToArray())
        {
            try
            {
                var pin = pair.Value;
                if (!pin.IsAlive) { Remove(pair.Key); continue; }
                var position = projection.GetMapPosition(pin.Poi.transform.position);
                if (!Finite(position)) { pin.Root.gameObject.SetActive(false); continue; }
                var projected = position + AnchorReference(pin.Poi.UI);
                var offset = projected - (center + anchor);
                var halfWidth = range * factor * 0.5f + pin.Radius;
                var onMap = Math.Abs(offset.x) <= halfWidth && Math.Abs(offset.y) <= halfWidth;
                pin.Root.gameObject.SetActive(onMap && pin.Poi.isActiveAndEnabled && pin.Poi.UI.gameObject.activeSelf);
                if (!onMap) continue; // Faraway icons need no per-frame visual/geometry copies.
                pin.Root.anchoredPosition = projected;
                pin.Sync();
            }
            catch (Exception error)
            {
                Remove(pair.Key);
                ModSupport.WarnOnce("ui:minimap-pin", $"A minimap icon was unavailable: {error.Message}");
            }
        }
        UpdatePlayerArrow(player);
    }

    private void UpdateArtwork()
    {
        var source = Map.BackgroundImage;
        MapImage.sprite = source.overrideSprite ? source.overrideSprite : source.sprite;
        MapImage.color = source.color;
        MapImage.preserveAspect = source.preserveAspect;
        MatchRect(Artwork, source.rectTransform, Map.PoIContainer);
    }

    private Vector2 MapAnchor()
    {
        var localPoi = Player.Local.PoI;
        if (localPoi && localPoi.UI) return AnchorReference(localPoi.UI);
        foreach (var pin in Pins.Values)
            if (pin.IsAlive) return AnchorReference(pin.Poi.UI);
        return Vector2.zero;
    }

    private void RefreshPins(Player player)
    {
        NextRefresh = Time.realtimeSinceStartup + 1;
        var seen = new HashSet<IntPtr>();
        foreach (var poi in Object.FindObjectsOfType<POI>())
        {
            if (!poi || !poi.isActiveAndEnabled || !poi.UI || !poi.IconContainer || !poi.UI.IsChildOf(Map.PoIContainer)
                || (player.PoI && poi.Pointer == player.PoI.Pointer) || !seen.Add(poi.Pointer)) continue;
            if (Pins.TryGetValue(poi.Pointer, out var pin) && !pin.IsAlive) Remove(poi.Pointer);
            if (Pins.ContainsKey(poi.Pointer)) continue;
            try { Pins.Add(poi.Pointer, MinimapPin.Create(poi, Content)); }
            catch (Exception error) { ModSupport.WarnOnce("ui:minimap-create", $"A minimap icon could not be created: {error.Message}"); }
        }
        foreach (var key in Pins.Keys.Where(key => !seen.Contains(key)).ToArray()) Remove(key);
    }

    private void UpdatePlayerArrow(Player player)
    {
        var poi = player.PoI;
        var sprite = poi && poi.IconContainer ? poi.IconContainer.GetComponentInChildren<Image>(true)?.sprite : null;
        if (sprite)
        {
            ArrowImage.sprite = sprite;
            ArrowImage.preserveAspect = true;
            PlayerArrow.sizeDelta = new(18, 18);
            var forward = player.transform.forward;
            PlayerArrow.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
        }
        else
        {
            ArrowImage.sprite = null;
            PlayerArrow.sizeDelta = new(10, 10);
            PlayerArrow.localRotation = Quaternion.Euler(0, 0, 45);
        }
    }

    private void Remove(IntPtr key)
    {
        if (Pins.Remove(key, out var pin)) pin.Dispose();
    }

    internal static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, new[] { Il2CppType.Of<RectTransform>() });
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private static Image Image(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return image;
    }

    private static Vector2 AnchorReference(RectTransform source)
    {
        var parent = source.parent.TryCast<RectTransform>();
        if (parent is null || !parent) return Vector2.zero;
        var bounds = parent.rect;
        var anchor = source.anchorMin + Vector2.Scale(source.anchorMax - source.anchorMin, source.pivot);
        return new(bounds.xMin + bounds.width * anchor.x, bounds.yMin + bounds.height * anchor.y);
    }

    // Transform corners into the unzoomed POI coordinate system. Phone-map
    // panning/zooming cancels out, so the minimap never moves the native content.
    private static void MatchRect(RectTransform target, RectTransform source, RectTransform coordinates)
    {
        var bounds = source.rect;
        var bottomLeft = coordinates.InverseTransformPoint(source.TransformPoint(new(bounds.xMin, bounds.yMin, 0)));
        var topLeft = coordinates.InverseTransformPoint(source.TransformPoint(new(bounds.xMin, bounds.yMax, 0)));
        var bottomRight = coordinates.InverseTransformPoint(source.TransformPoint(new(bounds.xMax, bounds.yMin, 0)));
        var horizontal = bottomRight - bottomLeft;
        var vertical = topLeft - bottomLeft;
        target.anchoredPosition = new(bottomLeft.x + (horizontal.x + vertical.x) * 0.5f, bottomLeft.y + (horizontal.y + vertical.y) * 0.5f);
        target.sizeDelta = new(horizontal.magnitude, vertical.magnitude);
        target.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(horizontal.y, horizontal.x) * Mathf.Rad2Deg);
    }

    private static bool Finite(Vector2 point) => float.IsFinite(point.x) && float.IsFinite(point.y);
    private static float Safe(float value, float fallback, float min, float max) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        foreach (var pin in Pins.Values) pin.Dispose();
        Pins.Clear();
        if (Frame) { Frame.gameObject.SetActive(false); Object.Destroy(Frame.gameObject); }
    }

    private sealed class MinimapPin : IDisposable
    {
        internal readonly POI Poi;
        internal readonly RectTransform Root;
        internal float Radius { get; private set; }
        private readonly List<(Graphic Source, Graphic Copy)> Graphics = new();
        internal bool IsAlive => Poi && Poi.UI && Poi.IconContainer && Root;
        private MinimapPin(POI poi, RectTransform root) { Poi = poi; Root = root; }

        internal static MinimapPin Create(POI poi, RectTransform parent)
        {
            var root = Rect(parent, "Minimap point of interest", Vector2.zero, Vector2.zero);
            var pin = new MinimapPin(poi, root);
            try
            {
                foreach (var source in poi.IconContainer.GetComponentsInChildren<Image>(true))
                {
                    var rect = Rect(root, "Map icon", Vector2.zero, Vector2.zero);
                    var copy = Image(rect, source.color);
                    copy.type = source.type; copy.preserveAspect = source.preserveAspect;
                    copy.fillMethod = source.fillMethod; copy.fillOrigin = source.fillOrigin;
                    copy.fillClockwise = source.fillClockwise;
                    pin.Graphics.Add((source, copy));
                }
                foreach (var source in poi.IconContainer.GetComponentsInChildren<RawImage>(true))
                {
                    var rect = Rect(root, "Map portrait", Vector2.zero, Vector2.zero);
                    var copy = rect.gameObject.AddComponent<RawImage>();
                    copy.raycastTarget = false;
                    pin.Graphics.Add((source, copy));
                }
                pin.Sync();
                return pin;
            }
            catch { pin.Dispose(); throw; }
        }

        internal void Sync()
        {
            Radius = 0;
            foreach (var (source, copy) in Graphics)
            {
                if (!source || !copy) continue;
                var visible = source.enabled;
                var opacity = 1f;
                // activeInHierarchy includes the closed phone panel; only local
                // icon/UI visibility should affect an independent HUD copy.
                for (var node = source.transform; node && node.Pointer != Poi.UI.parent.Pointer; node = node.parent)
                {
                    if (!node.gameObject.activeSelf) visible = false;
                    var group = node.GetComponent<CanvasGroup>();
                    if (group) opacity *= group.alpha;
                }
                copy.gameObject.SetActive(visible);
                if (!visible) continue;
                var color = source.color; color.a *= opacity;
                copy.color = color;
                MatchRect(copy.rectTransform, source.rectTransform, Poi.UI);
                // Large potential-customer search zones may intersect the square
                // even when their center lies outside it; retain their radius.
                Radius = Math.Max(Radius, copy.rectTransform.anchoredPosition.magnitude + copy.rectTransform.sizeDelta.magnitude * 0.5f);
                if (source.TryCast<Image>() is { } image && copy.TryCast<Image>() is { } imageCopy)
                {
                    imageCopy.sprite = image.overrideSprite ? image.overrideSprite : image.sprite;
                    imageCopy.fillAmount = image.fillAmount;
                }
                else if (source.TryCast<RawImage>() is { } raw && copy.TryCast<RawImage>() is { } rawCopy)
                { rawCopy.texture = raw.texture; rawCopy.uvRect = raw.uvRect; }
            }
        }

        public void Dispose() { if (Root) Object.Destroy(Root.gameObject); }
    }
}
