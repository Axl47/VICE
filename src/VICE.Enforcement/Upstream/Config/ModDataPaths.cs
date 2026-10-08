using MelonLoader.Utils;

namespace NACops
{
    /// <summary>
    /// Consolidates all the paths that mod might use in different installations
    /// </summary>
    public static class ModDataPaths
    {

        public static string Root { get; } = VICE.Core.UserDataFolders.PrepareModule(
            MelonEnvironment.UserDataDirectory, "Enforcement", "VICE.Enforcement");

        // Then for each config file or persistent data file it depends on the subfolder level
        public static readonly string pathModConfig = "config.json";
        public static readonly string pathOfficerConfig = "officer.json";
        public static readonly string pathRaidConfig = "raid.json";
        public static readonly string pathEventFrequencyConfig = "progression.json";
        public static readonly string pathSurveillanceConfig = "surveillance.json";

        public static readonly string pathPatrolsConfig = Path.Combine("Spawn", "patrols.json");
        public static readonly string pathVehiclePatrolsConfig = Path.Combine("Spawn", "vehiclepatrols.json");
        public static readonly string pathSentrysConfig = Path.Combine("Spawn", "sentrys.json");
        
        public static readonly string pathPropertyHeatConfig = "HeatData"; // Directory, Filename dynamic {saveslot num}_{organization}.json

        public static string GetPathTo(string modDataDestination) => Path.Combine(Root, modDataDestination);
    }
}
