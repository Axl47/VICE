using Il2CppInterop.Runtime;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using Il2CppTMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Copy font materials only. Keep native label text, layout and input untouched.
internal sealed class HudTextBlur : IDisposable
{
    private sealed class Label
    {
        internal TextMeshProUGUI Text = null!;
        internal Material Original = null!, Owned = null!;
        internal float Softness, Width;
        internal bool Phone;
    }
    private readonly Dictionary<IntPtr, Label> Labels = new();
    private float NextRefresh;
    private bool PhoneOpen, DialogueOpen;
    private IntPtr AppPointer;
    private bool Disposed;

    internal void Tick(float elapsed, float amount)
    {
        if (Disposed) return;
        if (amount <= 0) { Restore(); return; }
        var phone = Phone.Instance;
        var phoneOpen = LSDPreviewMod.PhoneEffects.Value && phone && phone.IsOpen;
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
            if (hud && hud.canvas) Discover(hud.canvas, false);
            if (dialogueOpen && dialogue.canvas) Discover(dialogue.canvas, false);
            if (phoneOpen)
            {
                Discover(PhoneColorBreathing.ScreenRoot(phone), true);
                if (Phone.ActiveApp) Discover(Phone.ActiveApp.transform, true);
            }
        }
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
            label.Owned.SetFloat("_OutlineSoftness", Mathf.Clamp01(label.Softness + blur * 0.8f));
            if (label.Owned.HasProperty("_OutlineWidth")) label.Owned.SetFloat("_OutlineWidth", Mathf.Clamp01(label.Width + blur * 0.12f));
            if (label.Phone && label.Owned.HasProperty("_UnderlayColor"))
            {
                label.Owned.SetColor("_UnderlayColor", Color.Lerp(new Color(0.25f, 0.9f, 1, blur * 0.5f),
                    new Color(0.9f, 0.3f, 1, blur * 0.5f), 0.5f + 0.5f * Mathf.Sin(elapsed * 0.21f)));
                SetIfPresent(label.Owned, "_UnderlayOffsetX", 0.18f * Mathf.Sin(elapsed * 0.31f));
                SetIfPresent(label.Owned, "_UnderlayOffsetY", 0.12f * Mathf.Cos(elapsed * 0.27f));
                SetIfPresent(label.Owned, "_UnderlaySoftness", 0.2f + blur * 0.5f);
            }
        }
    }

    private void Discover(Component? root, bool phone)
    {
        if (root is null || !root) return;
        foreach (var component in root.GetComponentsInChildren(Il2CppType.Of<TextMeshProUGUI>(), false))
        {
            if (Labels.Count >= 192) break;
            var text = component?.TryCast<TextMeshProUGUI>();
            if (text is null || !text) continue;
            if (Labels.TryGetValue(text.Pointer, out var existing))
            {
                existing.Phone |= phone;
                if (phone) existing.Owned.EnableKeyword("UNDERLAY_ON");
                continue;
            }
            var original = text.fontSharedMaterial;
            if (!original || !original.HasProperty("_OutlineSoftness")) continue;
            var owned = new Material(original) { name = "Personal LSD soft text", hideFlags = HideFlags.DontSave };
            if (phone) owned.EnableKeyword("UNDERLAY_ON");
            var label = new Label { Text = text, Original = original, Owned = owned, Phone = phone,
                Softness = original.GetFloat("_OutlineSoftness"), Width = original.HasProperty("_OutlineWidth") ? original.GetFloat("_OutlineWidth") : 0 };
            Labels[text.Pointer] = label;
            text.fontSharedMaterial = owned;
        }
    }

    private static void SetIfPresent(Material material, string property, float value)
    { if (material.HasProperty(property)) material.SetFloat(property, value); }

    private void Restore()
    {
        foreach (var label in Labels.Values)
        {
            if (label.Text && label.Owned && label.Text.fontSharedMaterial
                && label.Text.fontSharedMaterial.Pointer == label.Owned.Pointer && label.Original)
                label.Text.fontSharedMaterial = label.Original;
            if (label.Owned) Object.Destroy(label.Owned);
        }
        Labels.Clear();
        NextRefresh = 0;
        PhoneOpen = DialogueOpen = false;
        AppPointer = IntPtr.Zero;
    }

    public void Dispose() { if (Disposed) return; Disposed = true; Restore(); }
}
