using MelonLoader;
using VICE.Lifestyle.Television;

[assembly: MelonInfo(typeof(VICE.YouTube.YouTubeMod), "VICE YouTube (Development Add-on)", "0.1.0", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: HarmonyDontPatchAll]

namespace VICE.YouTube;

public sealed class YouTubeMod : MelonMod
{
    private YouTubeProvider? Provider;
    private IDisposable? Registration;
    public override void OnInitializeMelon()
    {
        Provider = new();
        Registration = TVYouTube.Register(Provider);
        MelonLogger.Msg(Provider.IsAvailable ? "Optional TV YouTube playback enabled." : Provider.UnavailableReason);
    }
    public override void OnDeinitializeMelon()
    {
        Registration?.Dispose();Registration=null;
        Provider?.Dispose();Provider=null;
    }
}
