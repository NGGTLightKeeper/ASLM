using System.Text.Json.Serialization;

namespace ASLM.Models
{
    /// <summary>
    /// Configuration for an installable module, deserialized from <c>ASLM_Module.json</c>.
    /// </summary>
    public class ModuleConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        // --- Source ----------------------------------------------------------

        [JsonPropertyName("source")]
        public ModuleSource Source { get; set; } = new();

        // --- Dependencies ----------------------------------------------------

        [JsonPropertyName("dependencies")]
        public ModuleDependencies Dependencies { get; set; } = new();

        // --- Commands --------------------------------------------------------

        [JsonPropertyName("commands")]
        public ModuleCommands Commands { get; set; } = new();

        // --- UI ----------------------------------------------------------

        /// <summary>
        /// Whether this module provides a web page accessible from the main panel.
        /// </summary>
        [JsonPropertyName("hasPage")]
        public bool HasPage { get; set; }

        // --- Settings --------------------------------------------------------

        [JsonPropertyName("settings")]
        public List<ModuleSetting> Settings { get; set; } = [];

        // --- Status ----------------------------------------------------------

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
    /// Defines where the module is hosted (GitHub).
    /// </summary>
    public class ModuleSource
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "github";

        [JsonPropertyName("repo")]
        public string Repo { get; set; } = string.Empty;
    }

    // --- Dependencies --------------------------------------------------------

    /// <summary>
    /// Declares what this module needs to run.
    /// </summary>
    public class ModuleDependencies
    {
        [JsonPropertyName("engines")]
        public List<ModuleEngineDependency> Engines { get; set; } = [];

        /// <summary>
        /// Required model categories (e.g. "ASR", "LLM").
        /// </summary>
        [JsonPropertyName("models")]
        public List<string> Models { get; set; } = [];
    }

    public class ModuleEngineDependency
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("libraries")]
        public List<string> Libraries { get; set; } = [];
    }

    // --- Commands ------------------------------------------------------------

    /// <summary>
    /// Groups commands by their purpose.
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
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

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
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Data type: "select", "string", "int", "port".
        /// "port" is managed by the application itself.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

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
        [JsonPropertyName("installed")]
        public bool Installed { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("firstRunCompleted")]
        public bool FirstRunCompleted { get; set; }

        [JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }

        [JsonPropertyName("lastChecked")]
        public string? LastChecked { get; set; }

        [JsonPropertyName("lastUpdated")]
        public string? LastUpdated { get; set; }
    }
}
