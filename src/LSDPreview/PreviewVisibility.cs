namespace PersonalMods.LSDPreview;

internal static class PreviewVisibility
{
    // Riding changes camera input state without opening a menu. Never infer
    // a pause solely from CanLook while on a skateboard or in a vehicle.
    internal static bool Suspend(bool paused, bool focused, bool typing, bool menuOpen,
        bool riding, bool canLook, bool hasCameraUi)
        => paused || !focused || typing || menuOpen || (!riding && (!canLook || hasCameraUi));
}
