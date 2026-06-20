// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text.Json.Serialization;

namespace ASLM.Models
{
    // Engine manifest

    /// <summary>
    /// Describes an engine package loaded from <c>ASLM_Engine.json</c>.
    /// </summary>
    public class EngineConfig
    {
        // Version of the manifest schema used for compatibility checks.
        [JsonPropertyName("fileVersion")]
        public int FileVersion { get; set; } = 1;

        // Stable engine identifier used throughout the application.
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        // Human-readable engine name shown in the UI.
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        // Short engine description displayed in setup and management screens.
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        // Version string of the packaged engine runtime.
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        // Resource type stored in the manifest, for example "runtime".
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        // Relative path to the primary executable inside the engine folder.
        [JsonPropertyName("executablePath")]
        public string ExecutablePath { get; set; } = string.Empty;

        // Package manager configuration used to install engine-specific libraries.
        [JsonPropertyName("packageManager")]
        public EnginePackageManager? PackageManager { get; set; }

        // Optional per-module environment configuration used to isolate dependencies.
        [JsonPropertyName("moduleEnvironment")]
        public EngineModuleEnvironment? ModuleEnvironment { get; set; }

        // Informational system requirements declared by the engine package.
        [JsonPropertyName("requirements")]
        public EngineRequirements? Requirements { get; set; }

        // Installation steps executed in the declared order.
        [JsonPropertyName("install")]
        public List<InstallStep> Install { get; set; } = [];

        // Additional steps executed after the main installation pipeline completes.
        [JsonPropertyName("postInstall")]
        public List<InstallStep> PostInstall { get; set; } = [];

        // Remote update configuration used to check GitHub releases.
        [JsonPropertyName("update")]
        public EngineUpdateConfig? Update { get; set; }

        // Persisted installation state of the engine.
        [JsonPropertyName("status")]
        public EngineStatus Status { get; set; } = new();

        // Absolute path to the source manifest file resolved at runtime.
        [JsonIgnore]
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Restores nested objects, collections, and strings after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            // Keep scalar string fields safe for downstream code and UI bindings.
            Id ??= string.Empty;
            Name ??= string.Empty;
            Description ??= string.Empty;
            Version ??= string.Empty;
            Type ??= string.Empty;
            ExecutablePath ??= string.Empty;
            SourcePath ??= string.Empty;

            // Normalize optional nested configuration blocks when they are present.
            PackageManager?.Normalize();
            ModuleEnvironment?.Normalize();
            Requirements?.Normalize();

            // Recreate and normalize the main installation pipeline.
            Install ??= [];
            foreach (var step in Install)
            {
                step?.Normalize();
            }

            // Recreate and normalize the post-install pipeline.
            PostInstall ??= [];
            foreach (var step in PostInstall)
            {
                step?.Normalize();
            }

            // Ensure persisted runtime status is always available.
            Status ??= new();
            Status.Normalize();

            Update?.Normalize();
        }
    }


    // Installation steps

    /// <summary>
    /// Describes one declarative installation action in the engine manifest.
    /// </summary>
    public class InstallStep
    {
        // Action name that determines which fields are meaningful for this step.
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        // Optional human-readable label used in logs and progress UI.
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // Download source URL.
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        // Expected SHA-256 checksum for downloaded content.
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        // Destination path used by download, extract, or move actions.
        [JsonPropertyName("dest")]
        public string? Dest { get; set; }

        // Source path used by extract or move actions.
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        // File path targeted by modify-file actions.
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        // Text fragment to locate inside a file modification action.
        [JsonPropertyName("find")]
        public string? Find { get; set; }

        // Replacement text for a file modification action.
        [JsonPropertyName("replace")]
        public string? Replace { get; set; }

        // Command line used by execute actions.
        [JsonPropertyName("command")]
        public string? Command { get; set; }

        // File or directory removed by cleanup actions.
        [JsonPropertyName("target")]
        public string? Target { get; set; }

        /// <summary>
        /// Restores the required action name after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            // Keep the action field non-null because step dispatch depends on it.
            Action ??= string.Empty;
        }
    }


    // Package management

    /// <summary>
    /// Describes how the engine installs third-party libraries.
    /// </summary>
    public class EnginePackageManager
    {
        // Arguments passed before package names, for example "-m pip install".
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        // Optional executable path used instead of the engine's main executable.
        [JsonPropertyName("executable")]
        public string? Executable { get; set; }

        /// <summary>
        /// Restores required string values after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            // Keep package installation commands safe for command construction.
            Command ??= string.Empty;
        }
    }


    // Module environments

    /// <summary>
    /// Describes how one engine creates and uses isolated environments per module.
    /// </summary>
    public class EngineModuleEnvironment
    {
        // Allows an engine manifest to opt out while keeping the block for documentation.
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        // Environment folder prefix inside the engine directory.
        [JsonPropertyName("directoryPrefix")]
        public string DirectoryPrefix { get; set; } = "venv-";

        // Optional descriptive kind, for example "python-venv" or "node-prefix".
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        // Optional command run with the engine executable to create the environment.
        [JsonPropertyName("createCommand")]
        public string CreateCommand { get; set; } = string.Empty;

        // Executable used for module commands after environment creation.
        [JsonPropertyName("executablePath")]
        public string ExecutablePath { get; set; } = string.Empty;

        // Optional package-manager executable override for environment-scoped installs.
        [JsonPropertyName("packageManagerExecutable")]
        public string PackageManagerExecutable { get; set; } = string.Empty;

        // Optional package-manager command override for environment-scoped installs.
        [JsonPropertyName("packageManagerCommand")]
        public string PackageManagerCommand { get; set; } = string.Empty;

        // Environment variables injected when commands run through this module environment.
        [JsonPropertyName("environment")]
        public Dictionary<string, string> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Restores string values and dictionaries after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            DirectoryPrefix = string.IsNullOrWhiteSpace(DirectoryPrefix) ? "venv-" : DirectoryPrefix;
            Kind ??= string.Empty;
            CreateCommand ??= string.Empty;
            ExecutablePath ??= string.Empty;
            PackageManagerExecutable ??= string.Empty;
            PackageManagerCommand ??= string.Empty;

            Environment ??= new(StringComparer.OrdinalIgnoreCase);
            Environment = Environment
                .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    static pair => pair.Key.Trim(),
                    static pair => pair.Value ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
        }
    }


    // Requirements

    /// <summary>
    /// Stores the system requirements declared by an engine package.
    /// </summary>
    public class EngineRequirements
    {
        // Required operating system identifier.
        [JsonPropertyName("os")]
        public string Os { get; set; } = string.Empty;

        // Required processor architecture identifier.
        [JsonPropertyName("arch")]
        public string Arch { get; set; } = string.Empty;

        // Required free disk space in megabytes.
        [JsonPropertyName("diskSpaceMb")]
        public int DiskSpaceMb { get; set; }

        /// <summary>
        /// Restores required string values after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            // Keep requirement values non-null for validation and display logic.
            Os ??= string.Empty;
            Arch ??= string.Empty;
        }
    }


    // Update configuration

    /// <summary>
    /// Describes how one engine checks and downloads updates from GitHub releases.
    /// </summary>
    public class EngineUpdateConfig
    {
        // GitHub repository in owner/name form.
        [JsonPropertyName("repo")]
        public string Repo { get; set; } = string.Empty;

        // Platform-specific release asset names, for example windows-x64.
        [JsonPropertyName("assetName")]
        public Dictionary<string, string> AssetName { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Restores required string values after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            Repo = string.IsNullOrWhiteSpace(Repo) ? string.Empty : Repo.Trim();

            AssetName ??= new(StringComparer.OrdinalIgnoreCase);
            AssetName = AssetName
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }


    // Status tracking

    /// <summary>
    /// Tracks the persisted installation state of an engine package.
    /// </summary>
    public class EngineStatus
    {
        // Indicates whether the engine is currently installed.
        [JsonPropertyName("installed")]
        public bool Installed { get; set; }

        // Version string of the installed engine, if known.
        [JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }

        // GitHub release tag of the installed engine, if known.
        [JsonPropertyName("installedReleaseTag")]
        public string? InstalledReleaseTag { get; set; }

        // Timestamp of the latest installation check or update.
        [JsonPropertyName("lastChecked")]
        public string? LastChecked { get; set; }

        /// <summary>
        /// Normalizes optional persisted values after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            // Empty text values are treated as missing persisted state.
            InstalledVersion = string.IsNullOrWhiteSpace(InstalledVersion) ? null : InstalledVersion;
            InstalledReleaseTag = string.IsNullOrWhiteSpace(InstalledReleaseTag) ? null : InstalledReleaseTag;
            LastChecked = string.IsNullOrWhiteSpace(LastChecked) ? null : LastChecked;
        }
    }
}
