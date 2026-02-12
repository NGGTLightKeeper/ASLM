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

        /// <summary>
        /// Relative path to the main executable (e.g. "runtime/python.exe").
        /// </summary>
        [JsonPropertyName("executablePath")]
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Describes how to install libraries for this engine.
        /// E.g. Python uses <c>"-m pip install"</c>, Node.js uses <c>"install"</c> with <c>npm.cmd</c>.
        /// </summary>
        [JsonPropertyName("packageManager")]
        public EnginePackageManager? PackageManager { get; set; }

        [JsonPropertyName("requirements")]
        public EngineRequirements? Requirements { get; set; }

        [JsonPropertyName("install")]
        public List<InstallStep> Install { get; set; } = [];

        /// <summary>
        /// Steps executed after all <see cref="Install"/> steps complete successfully.
        /// Used for engine-specific fixes (e.g. renaming restrictive config files).
        /// Supports the same actions as <see cref="Install"/>.
        /// </summary>
        [JsonPropertyName("postInstall")]
        public List<InstallStep> PostInstall { get; set; } = [];

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

        /// <summary>Human-readable name for logging.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

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
    /// Describes how to install third-party libraries for this engine.
    /// </summary>
    public class EnginePackageManager
    {
        /// <summary>
        /// Arguments prepended before library names.
        /// E.g. <c>"-m pip install"</c> for Python.
        /// </summary>
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Relative path to the package manager executable.
        /// If null, the engine's own executable (<see cref="EngineConfig.ExecutablePath"/>) is used.
        /// E.g. <c>"runtime/npm.cmd"</c> for Node.js.
        /// </summary>
        [JsonPropertyName("executable")]
        public string? Executable { get; set; }
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
