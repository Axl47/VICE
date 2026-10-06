using Il2CppInterop.Runtime;
using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalMods.LSDPreview;

// Overlay UI does not pass through world post-processing. Animate its native
// graphic colors separately, retaining alpha, text, layout and hit targets.
internal sealed class PhoneColorBreathing : IDisposable
{
    private sealed class Entry
    {
        internal Graphic Graphic = null!;
        internal TintLease Lease = null!;
    }
    private const int Limit = 160;
    private readonly Dictionary<IntPtr, Entry> Graphics = new();
    private float NextRefresh, NextAnimation;
    private IntPtr PhonePointer, AppPointer;
    private bool Disposed;

    internal static Component? ScreenRoot(Phone phone) => phone.raycaster ? phone.raycaster : phone;

    internal void Tick(float elapsed, float amount)
    {
        if (Disposed) return;
        var phone = Phone.Instance;
        if (amount <= 0 || !phone || !phone.IsOpen) { Restore(); return; }
        var app = Phone.ActiveApp ? Phone.ActiveApp.Pointer : IntPtr.Zero;
        if (PhonePointer != phone.Pointer || AppPointer != app)
        {
            Restore();
            PhonePointer = phone.Pointer;
            AppPointer = app;
        }
        if (elapsed >= NextRefresh)
        {
            NextRefresh = elapsed + 0.75f;
            Discover(ScreenRoot(phone));
            if (Phone.ActiveApp) Discover(Phone.ActiveApp.transform);
        }
        if (elapsed < NextAnimation) return;
        NextAnimation = elapsed + 0.05f;
        foreach (var pair in Graphics.ToArray())
        {
            var entry = pair.Value;
            if (!entry.Graphic) { Graphics.Remove(pair.Key); continue; }
            var current = Tint(entry.Graphic.color);
            entry.Lease.Observe(current);
            var tint = ColorBreathing.Sample(entry.Lease.Baseline, 7, elapsed, amount);
            entry.Graphic.color = new Color(tint.Red, tint.Green, tint.Blue, tint.Alpha);
            entry.Lease.Written(tint);
        }
    }

    private void Discover(Component? root)
    {
        if (root is null || !root) return;
        foreach (var component in root.GetComponentsInChildren(Il2CppType.Of<Graphic>(), false))
        {
            if (Graphics.Count >= Limit) break;
            var graphic = component?.TryCast<Graphic>();
            if (graphic is null || !graphic || Graphics.ContainsKey(graphic.Pointer)) continue;
            Graphics[graphic.Pointer] = new Entry { Graphic = graphic, Lease = new TintLease(Tint(graphic.color)) };
        }
    }

    private static SurfaceTint Tint(Color color) => new(color.r, color.g, color.b, color.a);

    private void Restore()
    {
        foreach (var entry in Graphics.Values)
        {
            if (!entry.Graphic || !entry.Lease.CanRestore(Tint(entry.Graphic.color))) continue;
            var tint = entry.Lease.Baseline;
            entry.Graphic.color = new Color(tint.Red, tint.Green, tint.Blue, tint.Alpha);
        }
        Graphics.Clear();
        PhonePointer = AppPointer = IntPtr.Zero;
        NextRefresh = NextAnimation = 0;
    }

    public void Dispose() { if (Disposed) return; Disposed = true; Restore(); }
}
