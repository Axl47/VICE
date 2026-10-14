using Il2CppInterop.Runtime;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using LegacyText = UnityEngine.UI.Text;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Own font-material copies and legacy glyph shadows. Never rewrite label text,
// move its layout, or change native colors, input handlers or hit targets.
internal sealed class HudTextBlur : IDisposable
{
    private sealed class Label
    {
        internal TextMeshProUGUI Text = null!;
        internal Material Original = null!, Owned = null!;
        internal float Softness, Width, Weight, UnderlayX, UnderlayY, UnderlaySoftness;
        internal Color UnderlayColor;
        internal bool Phone, Underlay, InnerUnderlay;
        internal int Seed;
    }
    private readonly Dictionary<IntPtr, Label> Labels = new();
    private sealed class LegacyLabel
    {
        internal LegacyText Text = null!;
        internal Shadow Fringe = null!;
        internal bool Phone;
        internal int Seed;
    }
    private readonly Dictionary<IntPtr, LegacyLabel> LegacyLabels = new();
    private static readonly string[] AnimatedFloats = ["_OutlineSoftness", "_OutlineWidth", "_FaceDilate",
        "_UnderlayOffsetX", "_UnderlayOffsetY", "_UnderlaySoftness"];
    private float NextRefresh;
    private bool PhoneOpen, DialogueOpen;
    private IntPtr AppPointer;
    private bool Disposed;
    private bool LoggedText;
    private float NextAnimation;

    internal void Tick(float elapsed, float amount, float focusAmount)
    {
        if (Disposed) return;
        if (amount <= 0 && focusAmount <= 0) { Restore(); return; }
        var phone = Phone.Instance;
        var phoneOpen = PreviewFeature.PhoneEffects.Value && phone && phone.IsOpen;
        var app = phoneOpen && Phone.ActiveApp ? Phone.ActiveApp.Pointer : IntPtr.Zero;
        var dialogue = DialogueCanvas.Instance;
        var dialogueOpen = dialogue && dialogue.IsOpen;
        // Discover an opened screen immediately; restore closed-screen fonts
        // immediately, so effects cannot leak into an inactive phone or menu.
        if (phoneOpen != PhoneOpen || dialogueOpen != DialogueOpen || app != AppPointer)
        {
            Restore();
            PhoneOpen = phoneOpen;
            DialogueOpen = dialogueOpen;
            AppPointer = app;
        }
        if (elapsed >= NextRefresh)
        {
            NextRefresh = elapsed + 1;
            var hud = HUD.Instance;
            if (hud && hud.canvas) { Discover(hud.canvas, false); DiscoverLegacy(hud.canvas, false); }
            if (dialogueOpen && dialogue.canvas) { Discover(dialogue.canvas, false); DiscoverLegacy(dialogue.canvas, false); }
            if (phoneOpen)
            {
                Discover(PhoneColorBreathing.ScreenRoot(phone), true);
                DiscoverLegacy(PhoneColorBreathing.ScreenRoot(phone), true);
                if (Phone.ActiveApp) { Discover(Phone.ActiveApp.transform, true); DiscoverLegacy(Phone.ActiveApp.transform, true); }
            }
        }
        // Slow typography needs no per-frame mesh rebuild. Bound animation
        // writes to 20Hz and the same combined 192-label ownership limit.
        if (elapsed < NextAnimation) return;
        NextAnimation = elapsed + 0.05f;
        var blur = amount * (0.7f + 0.3f * Mathf.Sin(elapsed * 0.4f));
        foreach (var pair in Labels.ToArray())
        {
            var label = pair.Value;
            if (!label.Text || !label.Owned || !label.Text.fontSharedMaterial
                || label.Text.fontSharedMaterial.Pointer != label.Owned.Pointer)
            {
                if (label.Owned) Object.Destroy(label.Owned);
                Labels.Remove(pair.Key);
                continue;
            }
            var focus = TextFocus.Sample(elapsed, label.Seed, focusAmount);
            SetIfPresent(label.Owned, "_OutlineSoftness", Mathf.Clamp01(label.Softness + blur * 0.8f + focus.Softness));
            SetIfPresent(label.Owned, "_OutlineWidth", Mathf.Clamp01(label.Width + blur * 0.12f));
            SetIfPresent(label.Owned, "_FaceDilate", Mathf.Clamp(label.Weight + focus.Weight, -1, 1));
            if (label.Owned.HasProperty("_UnderlayColor"))
            {
                var phoneFringe = label.Phone && amount > 0;
                var color = phoneFringe
                    ? Color.Lerp(new Color(0.25f, 0.9f, 1, blur * 0.5f), new Color(0.9f, 0.3f, 1, blur * 0.5f),
                        0.5f + 0.5f * Mathf.Sin(elapsed * 0.21f))
                    : label.UnderlayColor;
                SetKeyword(label.Owned, "UNDERLAY_ON", phoneFringe || focus.Opacity > 0 || label.Underlay);
                SetKeyword(label.Owned, "UNDERLAY_INNER", !phoneFringe && focus.Opacity <= 0 && label.InnerUnderlay);
                label.Owned.SetColor("_UnderlayColor", AddFocus(color, focus));
                SetIfPresent(label.Owned, "_UnderlayOffsetX", (phoneFringe ? 0.18f * Mathf.Sin(elapsed * 0.31f) : label.UnderlayX) + focus.OffsetX);
                SetIfPresent(label.Owned, "_UnderlayOffsetY", (phoneFringe ? 0.12f * Mathf.Cos(elapsed * 0.27f) : label.UnderlayY) + focus.OffsetY);
                SetIfPresent(label.Owned, "_UnderlaySoftness", Mathf.Clamp01((phoneFringe ? 0.2f + blur * 0.5f : label.UnderlaySoftness) + focus.Softness));
            }
            // Masked phone/dialogue labels render a cached stencil copy of
            // this label's unique font material. Update only effect properties
            // on that derived copy too; preserve its mask/stencil settings.
            var rendered = label.Text.materialForRendering;
            if (rendered && rendered.Pointer != label.Owned.Pointer)
            {
                foreach (var property in AnimatedFloats)
                    if (label.Owned.HasProperty(property)) SetIfPresent(rendered, property, label.Owned.GetFloat(property));
                if (rendered.HasProperty("_UnderlayColor") && label.Owned.HasProperty("_UnderlayColor"))
                    rendered.SetColor("_UnderlayColor", label.Owned.GetColor("_UnderlayColor"));
                SetKeyword(rendered, "UNDERLAY_ON", label.Owned.IsKeywordEnabled("UNDERLAY_ON"));
                SetKeyword(rendered, "UNDERLAY_INNER", label.Owned.IsKeywordEnabled("UNDERLAY_INNER"));
            }
        }
        foreach (var pair in LegacyLabels.ToArray())
        {
            var label = pair.Value;
            if (!label.Text || !label.Fringe)
            {
                if (label.Fringe) { label.Fringe.enabled = false; Object.Destroy(label.Fringe); }
                LegacyLabels.Remove(pair.Key);
                continue;
            }
            var focus = TextFocus.Sample(elapsed, label.Seed, focusAmount);
            var color = label.Phone ? Color.Lerp(new Color(0.2f, 0.9f, 1, blur * 1.1f),
                new Color(0.95f, 0.25f, 1, blur * 1.1f), 0.5f + 0.5f * Mathf.Sin(elapsed * 0.21f)) : Color.clear;
            label.Fringe.effectColor = AddFocus(color, focus);
            label.Fringe.effectDistance = new Vector2((label.Phone ? 0.8f + blur * 5 * Mathf.Sin(elapsed * 0.31f) : 0) + focus.OffsetX * 6,
                (label.Phone ? 0.6f + blur * 3 * Mathf.Cos(elapsed * 0.27f) : 0) + focus.OffsetY * 6);
        }
        if (!LoggedText && Labels.Count + LegacyLabels.Count > 0 && focusAmount > 0)
        {
            LoggedText = true;
            MelonLoader.MelonLogger.Msg($"LSD reading focus bound {Labels.Count} SDF and {LegacyLabels.Count} legacy labels: independently timed glyph shadows/stroke softness; original words and layout retained.");
        }
    }

    private void DiscoverLegacy(Component? root, bool phone)
    {
        if (root is null || !root) return;
        foreach (var component in root.GetComponentsInChildren(Il2CppType.Of<LegacyText>(), false))
        {
            if (LegacyLabels.Count + Labels.Count >= 192) break;
            var text = component?.TryCast<LegacyText>();
            if (text is null || !text) continue;
            if (LegacyLabels.TryGetValue(text.Pointer, out var existing)) { existing.Phone |= phone; continue; }
            var fringe = text.gameObject.AddComponent(Il2CppType.Of<Shadow>()).TryCast<Shadow>();
            if (fringe is null || !fringe) throw new InvalidOperationException("The owned text fringe component is unavailable.");
            fringe.effectColor = Color.clear;
            fringe.useGraphicAlpha = true;
            LegacyLabels[text.Pointer] = new LegacyLabel { Text = text, Fringe = fringe, Phone = phone, Seed = text.GetInstanceID() };
        }
    }

    private void Discover(Component? root, bool phone)
    {
        if (root is null || !root) return;
        foreach (var component in root.GetComponentsInChildren(Il2CppType.Of<TextMeshProUGUI>(), false))
        {
            if (Labels.Count + LegacyLabels.Count >= 192) break;
            var text = component?.TryCast<TextMeshProUGUI>();
            if (text is null || !text) continue;
            if (Labels.TryGetValue(text.Pointer, out var existing))
            {
                existing.Phone |= phone;
                continue;
            }
            var original = text.fontSharedMaterial;
            if (!original || !original.HasProperty("_OutlineSoftness")) continue;
            var owned = new Material(original) { name = "Personal LSD reading focus", hideFlags = HideFlags.DontSave };
            var underlay = original.IsKeywordEnabled("UNDERLAY_ON");
            var inner = original.IsKeywordEnabled("UNDERLAY_INNER");
            var label = new Label { Text = text, Original = original, Owned = owned, Phone = phone,
                Softness = original.GetFloat("_OutlineSoftness"), Width = GetIfPresent(original, "_OutlineWidth"),
                Weight = GetIfPresent(original, "_FaceDilate"), Underlay = underlay, InnerUnderlay = inner,
                UnderlayColor = (underlay || inner) && original.HasProperty("_UnderlayColor") ? original.GetColor("_UnderlayColor") : Color.clear,
                UnderlayX = underlay || inner ? GetIfPresent(original, "_UnderlayOffsetX") : 0,
                UnderlayY = underlay || inner ? GetIfPresent(original, "_UnderlayOffsetY") : 0,
                UnderlaySoftness = underlay || inner ? GetIfPresent(original, "_UnderlaySoftness") : 0, Seed = text.GetInstanceID() };
            // Reserve sufficient glyph padding once. Only shader parameters
            // animate afterwards, so drifting strokes neither clip nor move layout.
            if (owned.HasProperty("_UnderlayColor"))
            {
                owned.DisableKeyword("UNDERLAY_INNER"); owned.EnableKeyword("UNDERLAY_ON");
                SetIfPresent(owned, "_UnderlayOffsetX", Math.Abs(label.UnderlayX) + 1.1f);
                SetIfPresent(owned, "_UnderlayOffsetY", Math.Abs(label.UnderlayY) + 0.7f);
                SetIfPresent(owned, "_UnderlaySoftness", 1);
            }
            Labels[text.Pointer] = label;
            text.fontSharedMaterial = owned;
            text.UpdateMeshPadding();
        }
    }

    private static void SetIfPresent(Material material, string property, float value)
    { if (material.HasProperty(property)) material.SetFloat(property, value); }

    private static float GetIfPresent(Material material, string property)
        => material.HasProperty(property) ? material.GetFloat(property) : 0;

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (material.IsKeywordEnabled(keyword) == enabled) return;
        if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
    }

    private static Color AddFocus(Color original, ReadingFocus focus)
    {
        if (focus.Opacity <= 0) return original;
        var tint = Color.Lerp(new Color(0.7f, 0.95f, 1), new Color(1, 0.8f, 0.95f), focus.ColorMix);
        var color = Color.Lerp(original, tint, focus.Opacity / Math.Max(0.001f, original.a + focus.Opacity));
        color.a = Mathf.Clamp01(original.a + focus.Opacity);
        return color;
    }

    private void Restore()
    {
        foreach (var label in Labels.Values)
        {
            if (label.Text && label.Owned && label.Text.fontSharedMaterial
                && label.Text.fontSharedMaterial.Pointer == label.Owned.Pointer && label.Original)
            { label.Text.fontSharedMaterial = label.Original; label.Text.UpdateMeshPadding(); }
            if (label.Owned) Object.Destroy(label.Owned);
        }
        Labels.Clear();
        foreach (var label in LegacyLabels.Values)
        {
            if (!label.Fringe) continue;
            label.Fringe.enabled = false;
            Object.Destroy(label.Fringe);
        }
        LegacyLabels.Clear();
        NextAnimation = 0;
        NextRefresh = 0;
        PhoneOpen = DialogueOpen = false;
        AppPointer = IntPtr.Zero;
    }

    public void Dispose() { if (Disposed) return; Disposed = true; Restore(); }
}
