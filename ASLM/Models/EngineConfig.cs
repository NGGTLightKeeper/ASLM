using System.Text.Json.Serialization;

namespace ASLM.Models
{
    /// <summary>
    /// Top-level configuration for an engine runtime, deserialized from <c>ASLM_Engine.json</c>.
    /// </summary>
    public class EngineConfig
    {
        /// <summary>
        /// Unique identifier for the engine (e.g., "python-runtime").
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name of the engine.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the engine's purpose.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Version string of the engine (e.g., "3.12.8").
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Type of the engine (e.g., "runtime").
        /// </summary>
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

        /// <summary>
        /// System requirements needed to install this engine.
        /// </summary>
        [JsonPropertyName("requirements")]
        public EngineRequirements? Requirements { get; set; }

        /// <summary>
        /// List of steps to install the engine.
        /// </summary>
        [JsonPropertyName("install")]
        public List<InstallStep> Install { get; set; } = [];

        /// <summary>
        /// Steps executed after all <see cref="Install"/> steps complete successfully.
        /// Used for engine-specific fixes (e.g. renaming restrictive config files).
        /// Supports the same actions as <see cref="Install"/>.
        /// </summary>
        [JsonPropertyName("postInstall")]
        public List<InstallStep> PostInstall { get; set; } = [];

        /// <summary>
        /// Current installation status of the engine.
        /// </summary>
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
        /// <summary>
        /// The action to perform (e.g., "download", "extract", "execute").
        /// </summary>
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name for logging purposes.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // --- download --------------------------------------------------------

        /// <summary>
        /// URL for download actions.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Expected SHA-256 checksum for verification.
        /// </summary>
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        // --- download, extract, move -----------------------------------------

        /// <summary>
        /// Destination path for the action.
        /// </summary>
        [JsonPropertyName("dest")]
        public string? Dest { get; set; }

        // --- extract, move ---------------------------------------------------

        /// <summary>
        /// Source path for extraction or move actions.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        // --- modify_file -----------------------------------------------------

        /// <summary>
        /// Path to the file to modify.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        /// <summary>
        /// String pattern to find in the file.
        /// </summary>
        [JsonPropertyName("find")]
        public string? Find { get; set; }

        /// <summary>
        /// String to replace the found pattern with.
        /// </summary>
        [JsonPropertyName("replace")]
        public string? Replace { get; set; }

        // --- execute ---------------------------------------------------------

        /// <summary>
        /// Command line string to execute.
        /// </summary>
        [JsonPropertyName("command")]
        public string? Command { get; set; }

        // --- cleanup ---------------------------------------------------------

        /// <summary>
        /// Target directory or file to clean up (delete).
        /// </summary>
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
        /// <summary>
        /// Required Operating System (e.g., "windows").
        /// </summary>
        [JsonPropertyName("os")]
        public string Os { get; set; } = string.Empty;

        /// <summary>
        /// Required Architecture (e.g., "x64").
        /// </summary>
        [JsonPropertyName("arch")]
        public string Arch { get; set; } = string.Empty;

        /// <summary>
        /// Required disk space in Megabytes.
        /// </summary>
        [JsonPropertyName("diskSpaceMb")]
        public int DiskSpaceMb { get; set; }
    }

    /// <summary>
    /// Tracks whether the engine has been installed and when it was last checked.
    /// Persisted inside the <c>ASLM_Engine.json</c> file.
    /// </summary>
    public class EngineStatus
    {
        /// <summary>
        /// Gets or sets whether the engine is currently installed.
        /// </summary>
        [JsonPropertyName("installed")]
        public bool Installed { get; set; }

        /// <summary>
        /// Gets or sets the version currently installed.
        /// </summary>
        [JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }

        /// <summary>
        /// Timestamp of the last check/update.
        /// </summary>
        [JsonPropertyName("lastChecked")]
        public string? LastChecked { get; set; }
    }
}
