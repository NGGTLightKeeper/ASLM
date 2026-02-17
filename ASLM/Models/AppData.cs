using System.Text.Json.Serialization;

namespace ASLM.Models
{
    /// <summary>
    /// Represents the persistent application data structure,
    /// deserialized from <c>Data/App/ASLM_Data.json</c>.
    /// </summary>
    public class AppData
    {
        /// <summary>
        /// Gets or sets a value indicating whether the first-run wizard has been completed.
        /// </summary>
        [JsonPropertyName("firstRunCompleted")]
        public bool FirstRunCompleted { get; set; }

        /// <summary>
        /// Gets or sets the user profile data.
        /// </summary>
        [JsonPropertyName("user")]
        public AppUserData User { get; set; } = new();

        /// <summary>
        /// Gets or sets the port allocation configuration.
        /// </summary>
        [JsonPropertyName("ports")]
        public AppPortConfig Ports { get; set; } = new();
    }

    /// <summary>
    /// Represents user profile information.
    /// </summary>
    public class AppUserData
    {
        /// <summary>
        /// Gets or sets the user's display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration for port allocation ranges used by modules.
    /// Official modules get a compact range; third-party modules get a larger one.
    /// </summary>
    public class AppPortConfig
    {
        /// <summary>
        /// Gets or sets the start port for the official module range.
        /// Default: 8000.
        /// </summary>
        [JsonPropertyName("officialStart")]
        public int OfficialStart { get; set; } = 8000;

        /// <summary>
        /// Gets or sets the number of ports reserved for official modules.
        /// Default: 100.
        /// </summary>
        [JsonPropertyName("officialCount")]
        public int OfficialCount { get; set; } = 100;

        /// <summary>
        /// Gets or sets the start port for the third-party module range.
        /// Default: 9000.
        /// </summary>
        [JsonPropertyName("thirdPartyStart")]
        public int ThirdPartyStart { get; set; } = 9000;

        /// <summary>
        /// Gets or sets the number of ports reserved for third-party modules.
        /// Default: 1000.
        /// </summary>
        [JsonPropertyName("thirdPartyCount")]
        public int ThirdPartyCount { get; set; } = 1000;
    }
}
