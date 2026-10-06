using Il2CppScheduleOne.UI;
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
    }
    private readonly Dictionary<IntPtr, Label> Labels = new();
    private float NextRefresh;
    private bool Disposed;

    internal void Tick(float elapsed, float amount)
    {
        if (Disposed) return;
        if (amount <= 0) { Restore(); return; }
        if (elapsed >= NextRefresh)
        {
            NextRefresh = elapsed + 1;
            var hud = HUD.Instance;
            if (hud && hud.canvas)
            foreach (var text in hud.canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (!text || Labels.ContainsKey(text.Pointer)) continue;
                var original = text.fontSharedMaterial;
                if (!original || !original.HasProperty("_OutlineSoftness")) continue;
                var owned = new Material(original) { name = "Personal LSD soft HUD text", hideFlags = HideFlags.DontSave };
                var label = new Label { Text = text, Original = original, Owned = owned,
                    Softness = original.GetFloat("_OutlineSoftness"), Width = original.HasProperty("_OutlineWidth") ? original.GetFloat("_OutlineWidth") : 0 };
                Labels[text.Pointer] = label;
                text.fontSharedMaterial = owned;
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
        }
    }

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
    }

    public void Dispose() { if (Disposed) return; Disposed = true; Restore(); }
}
