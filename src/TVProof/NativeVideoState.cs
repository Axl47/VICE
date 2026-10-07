using Il2CppInterop.Runtime;
using UnityEngine.Video;

namespace PersonalMods.TVProof;

// The installed generated VideoModule declares native bool-returning icalls as
// unannotated managed bool delegates (default 4-byte marshalling). Unity returns
// a one-byte bool; consume only that byte, including for asynchronous preparation.
// Scope this workaround to our player rather than altering the game's wrappers.
internal static class NativeVideoState
{
    private delegate byte ReadBool(IntPtr player);
    private static readonly ReadBool Prepared = IL2CPP.ResolveICall<ReadBool>("UnityEngine.Video.VideoPlayer::get_isPrepared");
    private static readonly ReadBool Playing = IL2CPP.ResolveICall<ReadBool>("UnityEngine.Video.VideoPlayer::get_isPlaying");
    private static readonly ReadBool Paused = IL2CPP.ResolveICall<ReadBool>("UnityEngine.Video.VideoPlayer::get_isPaused");
    private static readonly ReadBool Seekable = IL2CPP.ResolveICall<ReadBool>("UnityEngine.Video.VideoPlayer::get_canSetTime");

    internal static bool IsPrepared(VideoPlayer player) => Prepared(player.Pointer) != 0;
    internal static bool IsPlaying(VideoPlayer player) => Playing(player.Pointer) != 0;
    internal static bool IsPaused(VideoPlayer player) => Paused(player.Pointer) != 0;
    internal static bool CanSeek(VideoPlayer player) => Seekable(player.Pointer) != 0;
}
