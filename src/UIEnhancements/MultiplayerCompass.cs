using Il2CppInterop.Runtime;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI.Compass;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PersonalMods.UIEnhancements;

internal sealed class MultiplayerCompass : IDisposable
{
    private const string ContentName = "Personal player compass content";
    private const string NameLabelName = "Player name";
    private readonly CompassManager Compass;
    private readonly Dictionary<IntPtr, PlayerMarker> Markers = new();
    private GameObject? Root;
    private RectTransform? Template;
    private float NextRefresh;
    private bool Disposed;

    internal MultiplayerCompass(CompassManager compass) => Compass = compass;
    internal bool BelongsTo(CompassManager compass) => !Disposed && Compass && Compass.Pointer == compass.Pointer;

    internal void Tick()
    {
        if (Disposed) return;
        var local = Player.Local;
        if (!UIEnhancementsMod.Enabled.Value || !UIEnhancementsMod.PlayerCompassMarkers.Value || !Compass || !local)
        { Clear(); return; }
        if (!local.PlayerLoaded || !Compass.ElementUIContainer || !Compass.ElementPrefab || !PlayerCamera.InstanceExists || !Compass.cam) return;
        if (Time.realtimeSinceStartup >= NextRefresh) Refresh(local);
        foreach (var pair in Markers.ToArray())
        {
            try
            {
                if (!Eligible(pair.Value.Player, local) || !pair.Value.IsAlive) { Remove(pair.Key); continue; }
                var position = pair.Value.Player.transform.position;
                if (!Finite(position)) { pair.Value.Element.Group.alpha = 0; continue; }
                // A mod-owned target survives a peer despawn until we remove its
                // element. Native UpdateElements never receives a destroyed peer.
                pair.Value.Anchor.transform.position = position;
                Compass.UpdateElement(pair.Value.Element);
                Compass.GetCompassData(position, out _, out var alpha);
                pair.Value.Element.Group.alpha = float.IsFinite(alpha) ? Mathf.Clamp01(alpha) : 0;
            }
            catch (Exception error)
            {
                Remove(pair.Key);
                ModSupport.WarnOnce("ui:compass-player", $"A player compass marker could not be updated: {error.Message}");
            }
        }
    }

    private void Refresh(Player local)
    {
        NextRefresh = Time.realtimeSinceStartup + 0.5f;
        var seen = new HashSet<IntPtr>();
        if (Player.PlayerList is null) { Clear(); return; }
        foreach (var player in Player.PlayerList)
        {
            try
            {
                if (!Eligible(player, local) || !seen.Add(player.Pointer)) continue;
                if (Markers.TryGetValue(player.Pointer, out var existing) && !existing.IsAlive) Remove(player.Pointer);
                if (!Markers.TryGetValue(player.Pointer, out var marker))
                {
                    var position = player.transform.position;
                    if (!Finite(position)) continue;
                    EnsureTemplate();
                    var anchor = new GameObject("Personal compass player target");
                    anchor.transform.SetParent(Root!.transform, false);
                    anchor.transform.position = position;
                    CompassManager.Element? element = null;
                    try
                    {
                        element = Compass.AddElement(anchor.transform, Template!, true);
                        var content = element.Rect.Find(ContentName + "(Clone)");
                        var label = content ? content.Find(NameLabelName)?.GetComponent<TextMeshProUGUI>() : null;
                        if (label is null || !label || !element.Group || !element.DistanceLabel)
                            throw new InvalidOperationException("The native player compass element is incomplete.");
                        element.Group.blocksRaycasts = false;
                        element.Group.interactable = false;
                        element.DistanceLabel.raycastTarget = false;
                        marker = new(player, anchor, element, label);
                        Markers.Add(player.Pointer, marker);
                    }
                    catch
                    {
                        // AddElement registers before its first native refresh.
                        // Recover that registration if refresh throws before return.
                        if (element is null && Compass.elements is not null)
                            foreach (var candidate in Compass.elements)
                                if (candidate.TargetTransform && candidate.TargetTransform.Pointer == anchor.transform.Pointer)
                                { element = candidate; break; }
                        if (element is not null) RemoveElement(element);
                        Object.Destroy(anchor);
                        throw;
                    }
                }
                marker.NameLabel.text = string.IsNullOrWhiteSpace(player.PlayerName) ? "Player" : player.PlayerName;
            }
            catch (Exception error) { ModSupport.WarnOnce("ui:compass-create", $"A player compass marker could not be created: {error.Message}"); }
        }
        foreach (var id in Markers.Keys.Where(id => !seen.Contains(id)).ToArray()) Remove(id);
    }

    private void EnsureTemplate()
    {
        if (Template) return;
        var fontSource = Compass.ElementPrefab.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!fontSource || !fontSource.font) throw new InvalidOperationException("The native compass font is missing.");
        if (!Root) { Root = new GameObject("Personal multiplayer compass"); Root.SetActive(false); }
        var template = Rectangle(Root!.transform, ContentName, Vector2.zero, Compass.ElementContentSize);
        try
        {
            var diamond = Rectangle(template, "Player marker", Vector2.zero, new(10, 10));
            diamond.localRotation = Quaternion.Euler(0, 0, 45);
            var image = diamond.gameObject.AddComponent<Image>();
            image.color = new(0.35f, 0.8f, 1, 1);
            image.raycastTarget = false;
            var labelRect = Rectangle(template, NameLabelName, new(0, 20), new(110, 18));
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = fontSource.font;
            label.fontSharedMaterial = fontSource.fontSharedMaterial;
            label.fontSize = 13;
            label.fontStyle = FontStyles.Normal;
            label.color = new(0.6f, 0.88f, 1, 1);
            label.richText = false;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            Template = template;
        }
        catch { Object.Destroy(template.gameObject); throw; }
        // The template is activeSelf=true under an inactive holder. Its clone
        // becomes visible under the native compass without displaying the template.
    }

    private static RectTransform Rectangle(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, new[] { Il2CppType.Of<RectTransform>() });
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static bool Eligible(Player? player, Player local) => player is not null && player && player.Pointer != local.Pointer
        && !player.IsLocalPlayer && player.gameObject.activeInHierarchy && (player.IsClientInitialized || player.IsServerInitialized);
    private static bool Finite(Vector3 position) => float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z);

    private void Remove(IntPtr id)
    {
        if (!Markers.Remove(id, out var marker)) return;
        RemoveElement(marker.Element);
        if (marker.Anchor) Object.Destroy(marker.Anchor);
    }

    private void RemoveElement(CompassManager.Element element)
    {
        try
        {
            if (Compass && Compass.elements is not null) Compass.RemoveElement(element, false);
        }
        catch (Exception error) { ModSupport.WarnOnce("ui:compass-remove", $"Player compass deregistration failed: {error.Message}"); }
        // Deregister the exact owned element, then destroy its UI. Removing by
        // player transform could remove another feature's compass marker.
        if (element.Rect) Object.Destroy(element.Rect.gameObject);
    }

    private void Clear()
    {
        foreach (var id in Markers.Keys.ToArray()) Remove(id);
        if (Root) Object.Destroy(Root);
        Root = null; Template = null; NextRefresh = 0;
    }

    public void Dispose() { if (Disposed) return; Clear(); Disposed = true; }
    private sealed record PlayerMarker(Player Player, GameObject Anchor, CompassManager.Element Element, TextMeshProUGUI NameLabel)
    {
        internal bool IsAlive => Player && Anchor && Element.Rect && Element.Group && NameLabel;
    }
}
