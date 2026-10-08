using VICE.Syndicate.Compatibility;


using MelonLoader.Utils;

namespace VICE.Syndicate
{
    /// <summary>
    /// Consolidates all the paths that mod might use in different installations
    /// </summary>
    public static class ModDataPaths
    {

        public static string Root { get; } = VICE.Core.UserDataFolders.PrepareModule(
            MelonEnvironment.UserDataDirectory, "Syndicate", "VICE.Syndicate");

        // Then for each config file or persistent data file it depends on the subfolder level

        public static readonly string pathModConfig = "config.json"; // Mod Folder in UserData root level

        public static readonly string pathAmbushes = Path.Combine("Ambush", "ambush.json");
        public static readonly string pathDefAmbushes = Path.Combine("Ambush", "default.json");
        public static readonly string pathSettingsAmbushes = Path.Combine("Ambush", "settings.json");

        public static readonly string pathDriveBys = Path.Combine("DriveBy", "driveby.json");
        public static readonly string pathDealerConfig = Path.Combine("Dealers", "dealer.json");
        public static readonly string pathCartelStolen = "CartelItems"; // Directory, Filenames dynamic {saveslot num}_{organization}.json
        public static readonly string pathInfluenceConfig = Path.Combine("Influence", "influence.json");

        public static readonly string pathAlliedConfig = Path.Combine("Allied", "config.json");
        public static readonly string pathAlliedPersist = Path.Combine("Allied", "QuestData"); // Directory, Filename dynamic {saveslot num}_{organization}.json

        public static readonly string pathEventFrequency = Path.Combine("EventFrequency", "config.json");
        public static readonly string pathEventFrequencyPersist = Path.Combine("EventFrequency", "Cooldowns"); // Directory, Filename dynamic {saveslot num}_{organization}.json

        // Steal back customers persistence
        public static readonly string pathStolenCustomers = "StolenCustomers"; // Directory, Filenames dynamic {saveslot num}_{organization}.json

        public static string GetPathTo(string modDataDestination) => Path.Combine(Root, modDataDestination);
    }
}
