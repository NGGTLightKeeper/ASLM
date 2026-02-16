using System.Text.Json.Serialization;

namespace ASLM.Models
{
    /// <summary>
    /// Persistent application data, deserialized from <c>Data/App/ASLM_Data.json</c>.
    /// </summary>
    public class AppData
    {
        [JsonPropertyName("firstRunCompleted")]
        public bool FirstRunCompleted { get; set; }

        [JsonPropertyName("user")]
        public AppUserData User { get; set; } = new();

        [JsonPropertyName("ports")]
        public AppPortConfig Ports { get; set; } = new();
    }

    /// <summary>
    /// User profile data.
    /// </summary>
    public class AppUserData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Port allocation ranges for modules.
    /// Official modules get a compact range; third-party modules get a larger one.
    /// </summary>
    public class AppPortConfig
    {
        /// <summary>Start of the official module port range.</summary>
        [JsonPropertyName("officialStart")]
        public int OfficialStart { get; set; } = 8000;

        /// <summary>Number of ports reserved for official modules.</summary>
        [JsonPropertyName("officialCount")]
        public int OfficialCount { get; set; } = 100;

        /// <summary>Start of the third-party module port range.</summary>
        [JsonPropertyName("thirdPartyStart")]
        public int ThirdPartyStart { get; set; } = 9000;

        /// <summary>Number of ports reserved for third-party modules.</summary>
        [JsonPropertyName("thirdPartyCount")]
        public int ThirdPartyCount { get; set; } = 1000;
    }
}
