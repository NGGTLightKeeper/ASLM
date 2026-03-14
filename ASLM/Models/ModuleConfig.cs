using System.Text.Json;
using System.Text.Json.Serialization;

namespace ASLM.Models
{
    /// <summary>
    /// Configuration for an installable module, deserialized from <c>ASLM_Module.json</c>.
    /// </summary>
    public class ModuleConfig
    {
        /// <summary>
        /// Schema version of the JSON file. Current version: 1.
        /// Used to maintain backward compatibility when the file structure changes.
        /// </summary>
        [JsonPropertyName("fileVersion")]
        public int FileVersion { get; set; } = 1;

        /// <summary>
        /// Unique identifier for the module.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name of the module.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the module.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Module version string.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Author of the module.
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Type of module (e.g., "service", "ui").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        // --- Source ----------------------------------------------------------

        /// <summary>
        /// Information about the module's source code repository.
        /// </summary>
        [JsonPropertyName("source")]
        public ModuleSource Source { get; set; } = new();

        // --- Dependencies ----------------------------------------------------

        /// <summary>
        /// Dependencies required by the module (engines, models).
        /// </summary>
        [JsonPropertyName("dependencies")]
        public ModuleDependencies Dependencies { get; set; } = new();

        // --- Commands --------------------------------------------------------

        /// <summary>
        /// Commands for setup and execution.
        /// </summary>
        [JsonPropertyName("commands")]
        public ModuleCommands Commands { get; set; } = new();

        // --- UI --------------------------------------------------------------

        /// <summary>
        /// Whether this module provides a web page accessible from the main panel.
        /// </summary>
        [JsonPropertyName("hasPage")]
        public bool HasPage { get; set; }

        /// <summary>
        /// Relative path to the module icon (e.g. "icon.png"). Optional.
        /// </summary>
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        /// <summary>
        /// Absolute path to the icon file. Resolved at runtime from <see cref="SourcePath"/>
        /// only when the target stays inside the module directory.
        /// </summary>
        [JsonIgnore]
        public string? IconFullPath =>
            ResolveModuleAssetPath(Icon);

        /// <summary>
        /// Relative path to the sidebar icon (e.g. "sidebar_icon.svg"). Optional.
        /// </summary>
        [JsonPropertyName("sidebarIcon")]
        public string? SidebarIcon { get; set; }

        /// <summary>
        /// Absolute path to the sidebar icon file. Resolved at runtime from <see cref="SourcePath"/>
        /// only when the target stays inside the module directory.
        /// </summary>
        [JsonIgnore]
        public string? SidebarIconFullPath =>
            ResolveModuleAssetPath(SidebarIcon);

        // --- Settings --------------------------------------------------------

        /// <summary>
        /// List of user-configurable settings exposed by the module.
        /// </summary>
        [JsonPropertyName("settings")]
        public List<ModuleSetting> Settings { get; set; } = [];

        // --- Status ----------------------------------------------------------

        /// <summary>
        /// Current installation and runtime status of the module.
        /// </summary>
        [JsonPropertyName("status")]
        public ModuleStatus Status { get; set; } = new();

        /// <summary>
        /// Absolute path to the JSON file this config was loaded from.
        /// Set at runtime; not serialized to disk.
        /// </summary>
        [JsonIgnore]
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Restores non-null nested objects, collections, and strings after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            Id ??= string.Empty;
            Name ??= string.Empty;
            Description ??= string.Empty;
            Version ??= string.Empty;
            Author ??= string.Empty;
            Type ??= string.Empty;
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon;
            SidebarIcon = string.IsNullOrWhiteSpace(SidebarIcon) ? null : SidebarIcon;
            SourcePath ??= string.Empty;

            Source ??= new();
            Source.Normalize();

            Dependencies ??= new();
            Dependencies.Normalize();

            Commands ??= new();
            Commands.Normalize();

            Settings ??= [];
            foreach (var setting in Settings)
            {
                setting?.Normalize();
            }

            Status ??= new();
            Status.Normalize();
        }

        private string? ResolveModuleAssetPath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(SourcePath))
                return null;

            var moduleDir = Path.GetDirectoryName(SourcePath);
            if (string.IsNullOrWhiteSpace(moduleDir))
                return null;

            var fullModuleDir = Path.GetFullPath(moduleDir);
            var fullPath = Path.GetFullPath(Path.Combine(fullModuleDir, relativePath));
            var moduleDirPrefix = fullModuleDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(moduleDirPrefix, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }
    }

    // --- Source ---------------------------------------------------------------

    /// <summary>
    /// Defines where the module is hosted (e.g. GitHub).
    /// </summary>
    public class ModuleSource
    {
        /// <summary>
        /// Type of source control (default "github").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "github";

        /// <summary>
        /// Repository path (e.g. "User/Repo").
        /// </summary>
        [JsonPropertyName("repo")]
        public string Repo { get; set; } = string.Empty;

        /// <summary>
        /// Restores required non-null string values after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            Type = string.IsNullOrWhiteSpace(Type) ? "github" : Type;
            Repo ??= string.Empty;
        }
    }

    // --- Dependencies --------------------------------------------------------

    /// <summary>
    /// Declares what this module needs to run.
    /// </summary>
    public class ModuleDependencies
    {
        /// <summary>
        /// List of required engine runtimes (e.g. Python, Node).
        /// </summary>
        [JsonPropertyName("engines")]
        public List<ModuleEngineDependency> Engines { get; set; } = [];

        /// <summary>
        /// Required model categories (e.g. "ASR", "LLM").
        /// </summary>
        [JsonPropertyName("models")]
        public List<string> Models { get; set; } = [];

        /// <summary>
        /// Restores non-null collections after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            Engines ??= [];
            foreach (var engine in Engines)
            {
                engine?.Normalize();
            }

            Models ??= [];
            Models = Models
                .Where(static model => !string.IsNullOrWhiteSpace(model))
                .ToList();
        }
    }

    /// <summary>
    /// Describes a dependency on a specific engine runtime.
    /// </summary>
    public class ModuleEngineDependency
    {
        /// <summary>
        /// The engine ID (e.g. "python-runtime").
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// List of libraries/packages to install for this engine.
        /// </summary>
        [JsonPropertyName("libraries")]
        public List<string> Libraries { get; set; } = [];

        /// <summary>
        /// Restores required non-null strings and collections after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            Id ??= string.Empty;

            Libraries ??= [];
            Libraries = Libraries
                .Where(static library => !string.IsNullOrWhiteSpace(library))
                .ToList();
        }
    }

    // --- Commands ------------------------------------------------------------

    /// <summary>
    /// Groups commands by their purpose (setup vs run).
    /// </summary>
    public class ModuleCommands
    {
        /// <summary>
        /// Commands executed once on first setup (e.g. migrations, pip install).
        /// </summary>
        [JsonPropertyName("firstRun")]
        public List<ModuleCommand> FirstRun { get; set; } = [];

        /// <summary>
        /// Commands executed on normal launch.
        /// </summary>
        [JsonPropertyName("run")]
        public List<ModuleCommand> Run { get; set; } = [];

        /// <summary>
        /// Restores non-null command collections after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            FirstRun ??= [];
            foreach (var command in FirstRun)
            {
                command?.Normalize();
            }

            Run ??= [];
            foreach (var command in Run)
            {
                command?.Normalize();
            }
        }
    }

    /// <summary>
    /// A single executable command.
    /// <c>exec</c> format: <c>"file.py subcommand --arg1 val1 --arg2 val2"</c>.
    /// The engine is specified separately via <see cref="Engine"/>.
    /// </summary>
    public class ModuleCommand
    {
        /// <summary>
        /// Display name of the command.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the command does.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Engine ID to use for execution (e.g. "python-runtime").
        /// If empty, the file is executed directly.
        /// </summary>
        [JsonPropertyName("engine")]
        public string Engine { get; set; } = string.Empty;

        /// <summary>
        /// Execution string relative to the module directory.
        /// Examples: <c>"main.py"</c>, <c>"manage.py runserver --port 8000"</c>.
        /// </summary>
        [JsonPropertyName("exec")]
        public string Exec { get; set; } = string.Empty;

        /// <summary>
        /// Restores required non-null string values after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            Name ??= string.Empty;
            Description ??= string.Empty;
            Engine ??= string.Empty;
            Exec ??= string.Empty;
        }
    }

    // --- Settings ------------------------------------------------------------

    /// <summary>
    /// A configurable setting exposed by the module.
    /// Type determines how the UI renders and validates its value.
    /// </summary>
    public class ModuleSetting
    {
        /// <summary>
        /// Unique key for the setting.
        /// </summary>
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the setting.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Help text describing the setting.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Data type for the setting value.
        /// Current runtime recognizes <c>select</c>, <c>string</c>, <c>int</c>,
        /// <c>bool</c>, <c>port</c>, <c>engine</c>, <c>path</c>, <c>data</c>,
        /// and <c>models</c>. The <c>port</c> type is managed by the application itself.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        /// <summary>
        /// Default value for the setting. Can be string, boolean, numeric, or null.
        /// </summary>
        [JsonPropertyName("default")]
        public object? Default { get; set; }

        /// <summary>
        /// Current value, updated at runtime. If null, <see cref="Default"/> is used.
        /// Can be string, boolean, numeric, or null.
        /// </summary>
        [JsonPropertyName("value")]
        public object? Value { get; set; }

        /// <summary>
        /// Allowed options for <c>select</c> settings.
        /// The current settings UI still renders these values as plain text input.
        /// </summary>
        [JsonPropertyName("allowedValues")]
        public List<string>? AllowedValues { get; set; }

        // --- Commands for this setting ---------------------------------------

        /// <summary>
        /// Engine used to execute get/set commands. Can be empty if not required.
        /// </summary>
        [JsonPropertyName("engine")]
        public string Engine { get; set; } = string.Empty;

        /// <summary>
        /// Command to retrieve the current value of this setting.
        /// Example: <c>"config.py get --key port"</c>
        /// </summary>
        [JsonPropertyName("getExec")]
        public string GetExec { get; set; } = string.Empty;

        /// <summary>
        /// Command to apply a new value. Use <c>{value}</c> as a placeholder.
        /// Example: <c>"config.py set --key port --value {value}"</c>
        /// </summary>
        [JsonPropertyName("setExec")]
        public string SetExec { get; set; } = string.Empty;

        /// <summary>
        /// Restores non-null strings and converts JSON scalar values to CLR values.
        /// </summary>
        public void Normalize()
        {
            Key ??= string.Empty;
            Name ??= string.Empty;
            Description ??= string.Empty;
            Type = string.IsNullOrWhiteSpace(Type) ? "string" : Type;
            Engine ??= string.Empty;
            GetExec ??= string.Empty;
            SetExec ??= string.Empty;

            Default = NormalizeScalarValue(Default);
            Value = NormalizeScalarValue(Value);

            if (AllowedValues != null)
            {
                AllowedValues = AllowedValues
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToList();
            }
        }

        /// <summary>
        /// Converts raw text input from the settings UI into the most suitable persisted value.
        /// </summary>
        /// <param name="rawValue">The text entered by the user.</param>
        /// <returns>A scalar value suitable for JSON serialization.</returns>
        public object? ParseUserInput(string? rawValue)
        {
            if (rawValue is null)
                return null;

            return Type.Trim().ToLowerInvariant() switch
            {
                "bool" => bool.TryParse(rawValue, out var boolValue) ? boolValue : rawValue,
                "int" or "port" => int.TryParse(rawValue, out var intValue) ? intValue : rawValue,
                _ => rawValue
            };
        }

        private static object? NormalizeScalarValue(object? value)
        {
            if (value is not JsonElement jsonElement)
                return value;

            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number when jsonElement.TryGetInt64(out var int64Value) => int64Value,
                JsonValueKind.Number when jsonElement.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => jsonElement.GetRawText()
            };
        }
    }

    // --- Status --------------------------------------------------------------

    /// <summary>
    /// Tracks the persisted lifecycle state of the module manifest.
    /// </summary>
    public class ModuleStatus
    {
        /// <summary>
        /// Gets or sets whether the module manifest has been discovered in the installed modules directory.
        /// </summary>
        [JsonPropertyName("installed")]
        public bool Installed { get; set; }

        /// <summary>
        /// Gets or sets whether the module is enabled (running).
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets whether the first-run setup has completed.
        /// </summary>
        [JsonPropertyName("firstRunCompleted")]
        public bool FirstRunCompleted { get; set; }

        /// <summary>
        /// Gets or sets the currently installed version.
        /// </summary>
        [JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }

        /// <summary>
        /// Timestamp of the last update check.
        /// </summary>
        [JsonPropertyName("lastChecked")]
        public string? LastChecked { get; set; }

        /// <summary>
        /// Timestamp of the last successful update.
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public string? LastUpdated { get; set; }

        /// <summary>
        /// Normalizes optional persisted values after JSON deserialization.
        /// </summary>
        public void Normalize()
        {
            InstalledVersion = string.IsNullOrWhiteSpace(InstalledVersion) ? null : InstalledVersion;
            LastChecked = string.IsNullOrWhiteSpace(LastChecked) ? null : LastChecked;
            LastUpdated = string.IsNullOrWhiteSpace(LastUpdated) ? null : LastUpdated;
        }
    }
}
