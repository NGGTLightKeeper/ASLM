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

        // --- UI ----------------------------------------------------------

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
        /// Absolute path to the icon file. Resolved at runtime from <see cref="SourcePath"/>.
        /// </summary>
        [JsonIgnore]
        public string? IconFullPath =>
            !string.IsNullOrEmpty(Icon) && !string.IsNullOrEmpty(SourcePath)
                ? Path.Combine(Path.GetDirectoryName(SourcePath)!, Icon)
                : null;

        /// <summary>
        /// Relative path to the sidebar icon (e.g. "sidebar_icon.svg"). Optional.
        /// </summary>
        [JsonPropertyName("sidebarIcon")]
        public string? SidebarIcon { get; set; }

        /// <summary>
        /// Absolute path to the sidebar icon file. Resolved at runtime from <see cref="SourcePath"/>.
        /// </summary>
        [JsonIgnore]
        public string? SidebarIconFullPath =>
            !string.IsNullOrEmpty(SidebarIcon) && !string.IsNullOrEmpty(SourcePath)
                ? Path.Combine(Path.GetDirectoryName(SourcePath)!, SidebarIcon)
                : null;

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
        /// Data type: "select", "string", "int", "port".
        /// "port" is managed by the application itself.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        /// <summary>
        /// Default value for the setting.
        /// </summary>
        [JsonPropertyName("default")]
        public string Default { get; set; } = string.Empty;

        /// <summary>
        /// Current value, updated at runtime. If null, <see cref="Default"/> is used.
        /// </summary>
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        /// <summary>
        /// Valid only for type "select". Lists the allowed options.
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
    }

    // --- Status --------------------------------------------------------------

    /// <summary>
    /// Tracks the installation and runtime state of the module.
    /// </summary>
    public class ModuleStatus
    {
        /// <summary>
        /// Gets or sets whether the module is installed.
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
    }
}
