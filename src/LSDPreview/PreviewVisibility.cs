namespace PersonalMods.LSDPreview;

internal static class PreviewVisibility
{
    // Input restrictions do not end perception: phone, dialogue, voice input,
    // transport and focus loss keep the trip running while gameplay continues.
    // Retain these inputs to test that none silently becomes a pause again.
    internal static bool Suspend(bool paused, bool focused, bool typing, bool menuOpen,
        bool riding, bool canLook, bool hasCameraUi)
        => paused;
}
