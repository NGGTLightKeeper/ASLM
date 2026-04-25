// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text.Json.Serialization;

namespace ASLM.Models
{
    // Application data

    /// <summary>
    /// Stores the persisted application data loaded from <c>Data/App/ASLM_Data.json</c>.
    /// </summary>
    public class AppData
    {
        // Tracks whether the initial setup flow has already finished.
        [JsonPropertyName("firstRunCompleted")]
        public bool FirstRunCompleted { get; set; }

        // Stores user-facing profile information.
        [JsonPropertyName("user")]
        public AppUserData User { get; set; } = new();

        // Stores the reserved application port ranges.
        [JsonPropertyName("ports")]
        public AppPortConfig Ports { get; set; } = new();

        // Stores ASLM and module update preferences.
        [JsonPropertyName("updates")]
        public AppUpdateSettings Updates { get; set; } = new();

        /// <summary>
        /// Restores nested objects after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            // Recreate the user section when it is missing from the persisted file.
            User ??= new();
            User.Normalize();

            // Recreate the ports section when it is missing from the persisted file.
            Ports ??= new();

            // Recreate and normalize update preferences when the section is absent.
            Updates ??= new();
            Updates.Normalize();
        }
    }


    // User data

    /// <summary>
    /// Stores the user profile values saved by the application.
    /// </summary>
    public class AppUserData
    {
        // Stores the display name shown in the UI.
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Restores string fields after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            // Keep string properties safe for UI binding and serialization.
            Name ??= string.Empty;
        }
    }


    // Port allocation

    /// <summary>
    /// Defines the port ranges reserved for official and third-party modules.
    /// </summary>
    public class AppPortConfig
    {
        // First port in the range reserved for official modules.
        [JsonPropertyName("officialStart")]
        public int OfficialStart { get; set; } = 8000;

        // Number of ports reserved for official modules.
        [JsonPropertyName("officialCount")]
        public int OfficialCount { get; set; } = 100;

        // First port in the range reserved for third-party modules.
        [JsonPropertyName("thirdPartyStart")]
        public int ThirdPartyStart { get; set; } = 9000;

        // Number of ports reserved for third-party modules.
        [JsonPropertyName("thirdPartyCount")]
        public int ThirdPartyCount { get; set; } = 1000;
    }


    // Update settings

    /// <summary>
    /// Stores user preferences for update checks and automatic installation.
    /// </summary>
    public class AppUpdateSettings
    {
        [JsonPropertyName("checkEnabled")]
        public bool CheckEnabled { get; set; } = true;

        [JsonPropertyName("autoUpdateEnabled")]
        public bool AutoUpdateEnabled { get; set; }

        [JsonPropertyName("autoCheckPeriodHours")]
        public int AutoCheckPeriodHours { get; set; } = 24;

        [JsonPropertyName("lastAutoCheckUtc")]
        public string? LastAutoCheckUtc { get; set; }

        [JsonPropertyName("appChannel")]
        public string AppChannel { get; set; } = "release";

        [JsonPropertyName("installedReleaseTag")]
        public string? InstalledReleaseTag { get; set; }

        [JsonPropertyName("moduleDefaultMode")]
        public string ModuleDefaultMode { get; set; } = "release";

        [JsonPropertyName("moduleDefaultChannel")]
        public string ModuleDefaultChannel { get; set; } = "release";

        /// <summary>
        /// Restores safe defaults and clamps numeric values.
        /// </summary>
        public void Normalize()
        {
            AutoCheckPeriodHours = Math.Clamp(AutoCheckPeriodHours <= 0 ? 24 : AutoCheckPeriodHours, 1, 720);
            LastAutoCheckUtc = string.IsNullOrWhiteSpace(LastAutoCheckUtc) ? null : LastAutoCheckUtc;
            AppChannel = NormalizeChannel(AppChannel);
            InstalledReleaseTag = string.IsNullOrWhiteSpace(InstalledReleaseTag) ? null : InstalledReleaseTag.Trim();
            ModuleDefaultMode = NormalizeMode(ModuleDefaultMode);
            ModuleDefaultChannel = NormalizeChannel(ModuleDefaultChannel);
        }

        private static string NormalizeChannel(string? value) =>
            string.Equals(value, "pre-release", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "prerelease", StringComparison.OrdinalIgnoreCase)
                ? "pre-release"
                : "release";

        private static string NormalizeMode(string? value) =>
            string.Equals(value, "branch", StringComparison.OrdinalIgnoreCase)
                ? "branch"
                : "release";
    }
}
