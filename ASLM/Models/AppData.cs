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
}
