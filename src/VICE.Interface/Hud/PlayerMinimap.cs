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

namespace VICE.Interface.Hud;

// Only UI primitives are copied. Cloning MapApp/POI behaviours would register
// duplicate map items, listeners and navigation state on the native phone map.
internal sealed class PlayerMinimap : IDisposable
{
    private readonly HUD Hud;
    private readonly MapApp Map;
    private readonly RectTransform Frame, Viewport, Content, Artwork, PlayerArrow;
    private readonly Image MapImage, ArrowImage, ViewportFill;
    private readonly RectMask2D ViewportMask;
    private readonly Dictionary<IntPtr, MinimapPin> Pins = new();
    private KeyValuePair<IntPtr, MinimapPin>[] PinSnapshot = Array.Empty<KeyValuePair<IntPtr, MinimapPin>>();
    private readonly RefreshBudget ArtworkRefresh = new(0.2f);
    private Vector2 CachedAnchor;
    private MinimapPin? LocalPlayerPin;
    private Texture2D? BorderTexture;
    private Sprite? BorderSprite;
    private bool BorderIsTutorial;
    private float NextRefresh;
    private bool Disposed, WasVisible;

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
            ViewportFill = Image(Viewport, new Color32(50, 66, 76, 255));
            // A textured fill follows the artwork's sRGB sampling path. A solid
            // UI vertex color can otherwise differ in a linear-color renderer.
            var nativeTexture = map.BackgroundImage.sprite ? map.BackgroundImage.sprite.texture : null;
            BorderTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, nativeTexture is not null && nativeTexture && !nativeTexture.isDataSRGB);
            BorderTexture.SetPixel(0, 0, new Color32(50, 66, 76, 255));
            BorderTexture.Apply(false, false);
            BorderSprite = Sprite.Create(BorderTexture, new UnityEngine.Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            ViewportFill.sprite = BorderSprite;
            ViewportFill.color = Color.white;
            ViewportMask = Viewport.gameObject.AddComponent<RectMask2D>();
            Content = Rect(Viewport, "Map projection", Vector2.zero, Vector2.zero);
            Artwork = Rect(Content, "Native map artwork", Vector2.zero, Vector2.one);
            MapImage = Image(Artwork, Color.white);
            MapImage.type = UnityEngine.UI.Image.Type.Simple;
            PlayerArrow = Rect(Viewport, "Local player", Vector2.zero, new(14, 14));
            ArrowImage = Image(PlayerArrow, new(0.35f, 0.85f, 1, 1));
            // The full native player glyph replaces this fallback after its UI
            // initializes. The first Image alone can be its black backing disc.
            PlayerArrow.localRotation = Quaternion.Euler(0, 0, 45);
        }
        catch { DestroyBorder(); Object.Destroy(Frame.gameObject); throw; }
    }

    internal bool BelongsTo(HUD hud, MapApp map) => !Disposed && Frame && Hud && Map
        && Hud.Pointer == hud.Pointer && Map.Pointer == map.Pointer;

    internal static bool Ready => HudFeature.Enabled.Value && HudFeature.MinimapEnabled.Value
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
            && !(Phone.InstanceExists && Phone.Instance.IsOpen);
        Frame.gameObject.SetActive(visible);
        if (!visible) { WasVisible = false; return; }
        var resumed = !WasVisible;
        WasVisible = true;
        var projection = MapPositionUtility.Instance;
        var center = projection.GetMapPosition(player.transform.position);
        var factor = Math.Abs(projection.conversionFactor);
        if (!Finite(center) || !float.IsFinite(factor) || factor < 0.0001f)
        { Frame.gameObject.SetActive(false); WasVisible = false; return; }
        var size = Safe(HudFeature.MinimapSize.Value, 240, 160, 480);
        var range = Safe(HudFeature.MinimapRange.Value, 160, 60, 500);
        Frame.sizeDelta = new(size + 6, size + 6);
        Viewport.sizeDelta = new(size, size);
        var scale = size / (range * factor);
        Content.localScale = new(scale, scale, 1);

        if (resumed) { NextRefresh = 0; ArtworkRefresh.Reset(); }
        if (Time.realtimeSinceStartup >= NextRefresh) RefreshPins(player);
        // Native POIs use GetMapPosition as their anchoredPosition. Account for
        // their prefab anchors as well as the map image's own pivot/hierarchy.
        if (ArtworkRefresh.Take(Time.realtimeSinceStartup))
        {
            CachedAnchor = MapAnchor();
            UpdateArtwork();
        }
        var anchor = CachedAnchor;
        Content.anchoredPosition = -(center + anchor) * scale;
        foreach (var pair in PinSnapshot)
        {
            try
            {
                var pin = pair.Value;
                if (!pin.IsAlive) { Remove(pair.Key); continue; }
                var position = projection.GetMapPosition(pin.Poi.transform.position);
                if (!Finite(position)) { pin.Root.gameObject.SetActive(false); continue; }
                var projected = position + pin.AnchorOffset;
                var offset = projected - (center + anchor);
                var halfWidth = range * factor * 0.5f + pin.Radius;
                var onMap = Math.Abs(offset.x) <= halfWidth && Math.Abs(offset.y) <= halfWidth;
                var active = onMap && pin.Poi.isActiveAndEnabled && pin.Poi.UI.gameObject.activeSelf;
                pin.Root.gameObject.SetActive(active);
                if (!active) continue; // Hidden icons need no visual/geometry copies.
                pin.Root.anchoredPosition = projected;
                pin.Sync(IconScale);
            }
            catch (Exception error)
            {
                Remove(pair.Key);
                ModSupport.WarnOnce("ui:minimap-pin", $"A minimap icon was unavailable: {error.Message}");
            }
        }
        UpdatePlayerArrow(player, scale);
        if (resumed)
        {
            // Re-enabling the phone can leave clipped renderers culled. Refresh
            // our own square's mask after its restored layout, once per return.
            ViewportMask.enabled = false;
            ViewportMask.enabled = true;
            Canvas.ForceUpdateCanvases();
            ViewportMask.PerformClipping();
        }
    }

    private void UpdateArtwork()
    {
        var source = Map.BackgroundImage;
        MapImage.sprite = source.overrideSprite ? source.overrideSprite : source.sprite;
        MapImage.color = source.color;
        MapImage.preserveAspect = source.preserveAspect;
        // Verified border pixels of the installed native Map_Full/DesertMap
        // sprites. Use their tint so moving beyond the artwork has no blue seam.
        var tutorial = Map.TutorialMapSprite && MapImage.sprite == Map.TutorialMapSprite;
        if (BorderTexture is not null && BorderTexture && tutorial != BorderIsTutorial)
        {
            BorderIsTutorial = tutorial;
            BorderTexture.SetPixel(0, 0, tutorial ? new Color32(84, 77, 68, 255) : new Color32(50, 66, 76, 255));
            BorderTexture.Apply(false, false);
        }
        var tint = source.color; tint.a = 1;
        ViewportFill.color = tint;
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
        var playerPois = new HashSet<IntPtr>();
        if (Player.PlayerList is not null)
            foreach (var peer in Player.PlayerList)
                if (peer && peer.PoI) playerPois.Add(peer.PoI.Pointer);
        foreach (var poi in MapPinRegistry.Current)
        {
            if (!poi || !poi.isActiveAndEnabled || !poi.UI || !poi.IconContainer || !poi.UI.IsChildOf(Map.PoIContainer)
                || (player.PoI && poi.Pointer == player.PoI.Pointer) || !seen.Add(poi.Pointer)) continue;
            if (Pins.TryGetValue(poi.Pointer, out var pin) && !pin.IsAlive) Remove(poi.Pointer);
            if (Pins.ContainsKey(poi.Pointer)) continue;
            try { Pins.Add(poi.Pointer, MinimapPin.Create(poi, Content, playerPois.Contains(poi.Pointer))); }
            catch (Exception error) { ModSupport.WarnOnce("ui:minimap-create", $"A minimap icon could not be created: {error.Message}"); }
        }
        foreach (var key in Pins.Keys.Where(key => !seen.Contains(key)).ToArray()) Remove(key);
        PinSnapshot = Pins.ToArray();
    }

    private void UpdatePlayerArrow(Player player, float mapScale)
    {
        var poi = player.PoI;
        if (LocalPlayerPin is not null && (!LocalPlayerPin.IsAlive || !poi || LocalPlayerPin.Poi.Pointer != poi.Pointer))
        { LocalPlayerPin.Dispose(); LocalPlayerPin = null; }
        if (LocalPlayerPin is null && poi && poi.UI && poi.IconContainer)
            LocalPlayerPin = MinimapPin.Create(poi, Viewport, true);
        if (LocalPlayerPin is not null)
        {
            var camera = PlayerCamera.InstanceExists ? PlayerCamera.Instance.Camera : null;
            var facing = camera is not null && camera ? camera.transform.forward : player.transform.forward;
            LocalPlayerPin.Sync(IconScale, facing);
            LocalPlayerPin.Root.anchoredPosition = Vector2.zero;
            LocalPlayerPin.Root.localScale *= mapScale;
            LocalPlayerPin.Root.gameObject.SetActive(true);
            PlayerArrow.gameObject.SetActive(!LocalPlayerPin.HasVisibleArtwork);
        }
        else
        {
            PlayerArrow.gameObject.SetActive(true);
            ArrowImage.sprite = null;
            PlayerArrow.sizeDelta = new(10 * IconScale, 10 * IconScale);
            PlayerArrow.localRotation = Quaternion.Euler(0, 0, 45);
        }
    }

    private static float IconScale => Safe(HudFeature.MinimapIconScale.Value, 1.55f, 0.5f, 3);

    internal void MapVisibilityChanged()
    {
        NextRefresh = 0;
        WasVisible = false;
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

    // Compose only transforms below the common ancestor. A hidden phone may
    // scale that ancestor to zero; world-space inversion would collapse the map
    // until it was reopened. Common phone pan/zoom/fades are excluded entirely.
    private static void MatchRect(RectTransform target, RectTransform source, RectTransform coordinates)
    {
        Transform? common = source;
        for (var depth = 0; common && !coordinates.IsChildOf(common) && depth < 64; depth++) common = common.parent;
        if (!common || !coordinates.IsChildOf(common)) return;
        var coordinateMatrix = ToAncestor(coordinates, common!);
        if (Math.Abs(coordinateMatrix.determinant) < 0.000001f) return;
        var relative = coordinateMatrix.inverse * ToAncestor(source, common!);
        var bounds = source.rect;
        var bottomLeft = relative.MultiplyPoint3x4(new(bounds.xMin, bounds.yMin, 0));
        var topLeft = relative.MultiplyPoint3x4(new(bounds.xMin, bounds.yMax, 0));
        var bottomRight = relative.MultiplyPoint3x4(new(bounds.xMax, bounds.yMin, 0));
        var horizontal = bottomRight - bottomLeft;
        var vertical = topLeft - bottomLeft;
        var position = new Vector2(bottomLeft.x + (horizontal.x + vertical.x) * 0.5f, bottomLeft.y + (horizontal.y + vertical.y) * 0.5f);
        var size = new Vector2(horizontal.magnitude, vertical.magnitude);
        if (!Finite(position) || !Finite(size) || size.x < 0.0001f || size.y < 0.0001f) return;
        target.anchoredPosition = position;
        target.sizeDelta = size;
        target.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(horizontal.y, horizontal.x) * Mathf.Rad2Deg);
    }

    private static Matrix4x4 ToAncestor(Transform source, Transform ancestor)
    {
        var result = Matrix4x4.identity;
        var node = source;
        for (var depth = 0; node && node.Pointer != ancestor.Pointer && depth < 64; depth++, node = node.parent)
            result = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale) * result;
        return result;
    }

    private static bool Finite(Vector2 point) => float.IsFinite(point.x) && float.IsFinite(point.y);
    private static float Safe(float value, float fallback, float min, float max) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private void DestroyBorder()
    {
        if (BorderSprite) Object.Destroy(BorderSprite);
        if (BorderTexture) Object.Destroy(BorderTexture);
        BorderSprite = null; BorderTexture = null;
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        foreach (var pin in Pins.Values) pin.Dispose();
        Pins.Clear();
        PinSnapshot = Array.Empty<KeyValuePair<IntPtr, MinimapPin>>();
        LocalPlayerPin?.Dispose(); LocalPlayerPin = null;
        if (Frame) { Frame.gameObject.SetActive(false); Object.Destroy(Frame.gameObject); }
        DestroyBorder();
    }

    private sealed class MinimapPin : IDisposable
    {
        internal readonly POI Poi;
        internal readonly RectTransform Root;
        internal float Radius { get; private set; }
        internal Vector2 AnchorOffset { get; private set; }
        private readonly List<(Graphic Source, Graphic Copy, Transform[] Nodes, CanvasGroup[] Groups)> Graphics = new();
        private readonly RefreshBudget VisualRefresh = new(0.2f);
        private float DisplayScale = 1;
        private readonly Dictionary<IntPtr, (RectTransform Source, RectTransform Copy)> PlayerRects = new();
        private RectTransform? FacingRoot;
        private readonly bool IsPlayer;
        private readonly IntPtr SourceUi, SourceIcon;
        private float Heading;
        internal bool HasVisibleArtwork { get; private set; }
        internal bool IsAlive => Poi && Poi.UI && Poi.IconContainer && Root
            && Poi.UI.Pointer == SourceUi && Poi.IconContainer.Pointer == SourceIcon;
        private MinimapPin(POI poi, RectTransform root, bool isPlayer)
        { Poi = poi; Root = root; IsPlayer = isPlayer; SourceUi = poi.UI.Pointer; SourceIcon = poi.IconContainer.Pointer; }

        internal static MinimapPin Create(POI poi, RectTransform parent, bool isPlayer)
        {
            var root = Rect(parent, "Minimap point of interest", Vector2.zero, Vector2.zero);
            var pin = new MinimapPin(poi, root, isPlayer);
            try
            {
                var sourceRoot = isPlayer ? poi.UI : poi.IconContainer;
                if (isPlayer)
                {
                    // Retain native pivots, anchors, scales and nesting. Flattening
                    // a composite into independently sized/rotated rectangles can
                    // change sliced shadows and the alignment of its circular layers.
                    foreach (var sourceRect in poi.UI.GetComponentsInChildren<RectTransform>(true))
                    {
                        var parentRect = sourceRect.Pointer == poi.UI.Pointer ? root
                            : pin.PlayerRects[sourceRect.parent.Pointer].Copy;
                        // Centered circular layers and the sliced shade have no
                        // direction. Keep their sampling stable while the offset
                        // arrow turns in an additional owned child container.
                        if (sourceRect.Pointer != poi.UI.Pointer && sourceRect.parent.Pointer == pin.SourceIcon
                            && sourceRect.name is not ("Shade" or "Outline" or "Fill"))
                        {
                            pin.FacingRoot ??= Rect(parentRect, "Player facing", Vector2.zero, Vector2.zero);
                            parentRect = pin.FacingRoot;
                        }
                        var copyRect = Rect(parentRect, "Player icon layer", Vector2.zero, Vector2.zero);
                        pin.PlayerRects.Add(sourceRect.Pointer, (sourceRect, copyRect));
                    }
                }
                // Preserve native image/raw-image draw order and every player
                // foreground layer, including siblings outside IconContainer.
                foreach (var graphic in sourceRoot.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic.TryCast<Image>() is { } source)
                    {
                        var rect = isPlayer ? pin.PlayerRects[source.rectTransform.Pointer].Copy
                            : Rect(root, "Map icon", Vector2.zero, Vector2.zero);
                        var copy = Image(rect, source.color);
                        copy.type = source.type; copy.preserveAspect = source.preserveAspect;
                        copy.fillMethod = source.fillMethod; copy.fillOrigin = source.fillOrigin;
                        copy.fillClockwise = source.fillClockwise;
                        pin.AddGraphic(source, copy);
                    }
                    else if (graphic.TryCast<RawImage>() is { } raw)
                    {
                        var rect = isPlayer ? pin.PlayerRects[raw.rectTransform.Pointer].Copy
                            : Rect(root, "Map portrait", Vector2.zero, Vector2.zero);
                        var copy = rect.gameObject.AddComponent<RawImage>();
                        copy.raycastTarget = false;
                        pin.AddGraphic(raw, copy);
                    }
                }
                pin.Sync(IconScale);
                return pin;
            }
            catch { pin.Dispose(); throw; }
        }

        internal void Sync(float iconScale, Vector3? viewDirection = null)
        {
            if (IsPlayer)
            {
                var facing = viewDirection ?? Poi.transform.forward;
                // Native map X/Y corresponds to world X/Z: east is clockwise.
                // Ignore pitch and keep the last heading when looking vertically.
                if (float.IsFinite(facing.x) && float.IsFinite(facing.z)
                    && facing.x * facing.x + facing.z * facing.z > 0.000001f)
                    Heading = Mathf.Atan2(-facing.x, facing.z) * Mathf.Rad2Deg;
                if (FacingRoot) FacingRoot!.localRotation = Quaternion.Euler(0, 0, Heading);
            }
            // Root scale is restored each frame because the local glyph's caller
            // applies the current map zoom after Sync. Heavy style/layout work is
            // independent of smooth facing and position updates.
            Root.localScale = new(DisplayScale, DisplayScale, 1);
            if (!VisualRefresh.Take(Time.realtimeSinceStartup)) return;
            AnchorOffset = AnchorReference(Poi.UI);
            Radius = 0;
            HasVisibleArtwork = false;
            var area = false;
            if (IsPlayer) SyncPlayerLayout();
            foreach (var (source, copy, nodes, groups) in Graphics)
            {
                if (!source || !copy) continue;
                var visible = source.enabled;
                var opacity = 1f;
                // activeInHierarchy includes the closed phone panel; only local
                // icon/UI visibility should affect an independent HUD copy.
                foreach (var node in nodes) if (!node || !node.gameObject.activeSelf) visible = false;
                foreach (var group in groups) if (group) opacity *= group.alpha;
                // A disabled parent Graphic must not hide its child graphics:
                // player copies now share the native transform hierarchy.
                if (IsPlayer) copy.enabled = visible;
                else copy.gameObject.SetActive(visible);
                if (!visible) continue;
                var color = source.color; color.a *= opacity;
                copy.color = color;
                copy.material = source.material;
                if (!IsPlayer) MatchRect(copy.rectTransform, source.rectTransform, Poi.UI);
                var dimensions = copy.rectTransform.sizeDelta;
                area |= Math.Max(dimensions.x, dimensions.y) > 100;
                // Large potential-customer search zones may intersect the square
                // even when their center lies outside it; retain their radius.
                Radius = Math.Max(Radius, IsPlayer ? PlayerLayerRadius(copy.rectTransform)
                    : copy.rectTransform.anchoredPosition.magnitude + copy.rectTransform.sizeDelta.magnitude * 0.5f);
                if (source.TryCast<Image>() is { } image && copy.TryCast<Image>() is { } imageCopy)
                {
                    imageCopy.sprite = image.overrideSprite ? image.overrideSprite : image.sprite;
                    imageCopy.type = image.type; imageCopy.preserveAspect = image.preserveAspect;
                    imageCopy.fillCenter = image.fillCenter; imageCopy.useSpriteMesh = image.useSpriteMesh;
                    imageCopy.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
                    imageCopy.fillMethod = image.fillMethod; imageCopy.fillOrigin = image.fillOrigin;
                    imageCopy.fillClockwise = image.fillClockwise;
                    imageCopy.fillAmount = image.fillAmount;
                    HasVisibleArtwork |= imageCopy.sprite && color.a > 0.01f;
                }
                else if (source.TryCast<RawImage>() is { } raw && copy.TryCast<RawImage>() is { } rawCopy)
                {
                    rawCopy.texture = raw.texture; rawCopy.uvRect = raw.uvRect;
                    HasVisibleArtwork |= rawCopy.texture && color.a > 0.01f;
                }
            }
            // Enlarge symbols, not geographical search-zone footprints.
            var scale = area && !IsPlayer ? 1 : iconScale;
            DisplayScale = scale;
            Root.localScale = new(scale, scale, 1);
            Radius *= scale;
        }

        private void AddGraphic(Graphic source, Graphic copy)
        {
            var nodes = new List<Transform>();
            var groups = new List<CanvasGroup>();
            var boundary = Poi.UI.parent;
            for (var node = source.transform; node && node.Pointer != boundary.Pointer; node = node.parent)
            {
                nodes.Add(node);
                var group = node.GetComponent<CanvasGroup>();
                if (group) groups.Add(group);
            }
            Graphics.Add((source, copy, nodes.ToArray(), groups.ToArray()));
        }

        private void SyncPlayerLayout()
        {
            foreach (var (source, copy) in PlayerRects.Values)
            {
                if (!source || !copy) continue;
                copy.pivot = source.pivot;
                if (source.Pointer == SourceUi)
                {
                    // Phone position, pan/zoom and fades belong to the source UI.
                    // Only the inner glyph layout is shared with our centered pin.
                    copy.anchorMin = copy.anchorMax = new(0.5f, 0.5f);
                    copy.sizeDelta = source.rect.size;
                    copy.anchoredPosition3D = Vector3.zero;
                    copy.localScale = Vector3.one;
                    copy.localRotation = Quaternion.identity;
                }
                else
                {
                    copy.anchorMin = source.anchorMin; copy.anchorMax = source.anchorMax;
                    copy.sizeDelta = source.sizeDelta;
                    copy.anchoredPosition3D = source.anchoredPosition3D;
                    copy.localScale = source.localScale;
                    // Freeze the circular backing; the facing subtree alone turns.
                    copy.localRotation = source.Pointer == SourceIcon
                        ? Quaternion.identity : source.localRotation;
                }
            }
            if (FacingRoot is not null && FacingRoot)
            {
                FacingRoot.anchorMin = FacingRoot.anchorMax = FacingRoot.pivot = Poi.IconContainer.pivot;
                FacingRoot.sizeDelta = Poi.IconContainer.rect.size;
                FacingRoot.anchoredPosition3D = Vector3.zero;
                FacingRoot.localScale = Vector3.one;
                FacingRoot.localRotation = Quaternion.Euler(0, 0, Heading);
            }
        }

        private float PlayerLayerRadius(RectTransform layer)
        {
            var relative = ToAncestor(layer, Root);
            var bounds = layer.rect;
            var radius = Math.Max(relative.MultiplyPoint3x4(new(bounds.xMin, bounds.yMin, 0)).magnitude,
                relative.MultiplyPoint3x4(new(bounds.xMin, bounds.yMax, 0)).magnitude);
            radius = Math.Max(radius, relative.MultiplyPoint3x4(new(bounds.xMax, bounds.yMin, 0)).magnitude);
            radius = Math.Max(radius, relative.MultiplyPoint3x4(new(bounds.xMax, bounds.yMax, 0)).magnitude);
            return float.IsFinite(radius) ? radius : 0;
        }

        public void Dispose() { if (Root) Object.Destroy(Root.gameObject); }
    }
}
