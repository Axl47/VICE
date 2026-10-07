using MelonLoader;

namespace PersonalMods.TV;

internal static class TVConfig
{
    internal static MelonPreferences_Entry<string> RelayOrigin = null!, RelayToken = null!;
    internal static MelonPreferences_Entry<float> Distance = null!, PersonalVolume = null!;
    private static MelonPreferences_Category Category = null!;
    internal static string Root = "", Images = "", Videos = "";
    internal static void Initialize()
    {
        Root = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "PersonalTV");
        Images = Path.Combine(Root, "Images"); Videos = Path.Combine(Root, "Videos");
        Directory.CreateDirectory(Images); Directory.CreateDirectory(Videos);
        Category = MelonPreferences.CreateCategory("PersonalTV");
        RelayOrigin = Category.CreateEntry("RelayApiOrigin", "", description: "Relay API origin. HTTPS, or loopback HTTP for development.");
        RelayToken = Category.CreateEntry("RelayGameToken", "", description: "Scoped game integration token, stored only on this computer; never sent to game peers.");
        Distance = Category.CreateEntry("PlaybackDistance", 35f, description: "Decode nearby TVs within this distance; distant TVs keep shared timing without decoding.");
        PersonalVolume = Category.CreateEntry("PersonalVolume", 1f, description: "Local listening gain from 0 to 1, in addition to TV and game volume.");
    }
    internal static void SaveRelay(string origin, string token)
    { using var check = new RelayClient(origin, token); RelayOrigin.Value = origin.TrimEnd('/'); RelayToken.Value = token.Trim(); Category.SaveToFile(false); }
    internal static RelayClient Relay() => new(RelayOrigin.Value, RelayToken.Value);
    internal static Func<RelayClient> RelayFactory()
    {var origin=RelayOrigin.Value;var token=RelayToken.Value;return()=>new RelayClient(origin,token);}
}
