using System.Text.Json.Serialization;

namespace ASLM.Models
{
    /// <summary>
    /// Top-level configuration for an engine runtime, deserialized from <c>ASLM_Engine.json</c>.
    /// </summary>
    public class EngineConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("requirements")]
        public EngineRequirements? Requirements { get; set; }

        [JsonPropertyName("install")]
        public List<InstallStep> Install { get; set; } = [];

        [JsonPropertyName("status")]
        public EngineStatus Status { get; set; } = new();

        /// <summary>
        /// Absolute path to the JSON file this config was loaded from.
        /// Set at runtime after deserialization; not serialized to disk.
        /// </summary>
        [JsonIgnore]
        public string SourcePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// A single declarative installation step (download, extract, execute, etc.).
    /// Only the fields relevant to the <see cref="Action"/> type are populated;
    /// the rest remain <c>null</c> and are omitted during serialization.
    /// </summary>
    public class InstallStep
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        // --- download --------------------------------------------------------

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        // --- download, extract, move -----------------------------------------

        [JsonPropertyName("dest")]
        public string? Dest { get; set; }

        // --- extract, move ---------------------------------------------------

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        // --- modify_file -----------------------------------------------------

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("find")]
        public string? Find { get; set; }

        [JsonPropertyName("replace")]
        public string? Replace { get; set; }

        // --- execute ---------------------------------------------------------

        [JsonPropertyName("command")]
        public string? Command { get; set; }

        // --- cleanup ---------------------------------------------------------

        [JsonPropertyName("target")]
        public string? Target { get; set; }
    }

    /// <summary>
    /// System requirements needed to install this engine.
    /// </summary>
    public class EngineRequirements
    {
        [JsonPropertyName("os")]
        public string Os { get; set; } = string.Empty;

        [JsonPropertyName("arch")]
        public string Arch { get; set; } = string.Empty;

        [JsonPropertyName("diskSpaceMb")]
        public int DiskSpaceMb { get; set; }
    }

    /// <summary>
    /// Tracks whether the engine has been installed and when it was last checked.
    /// Persisted inside the <c>ASLM_Engine.json</c> file.
    /// </summary>
    public class EngineStatus
    {
        [JsonPropertyName("installed")]
        public bool Installed { get; set; }

        [JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }

        [JsonPropertyName("lastChecked")]
        public string? LastChecked { get; set; }
    }
}
