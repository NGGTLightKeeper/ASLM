using System.Text.Json.Serialization;

namespace ASLM.Models
{
    /// <summary>
    /// Configuration for a machine learning model, deserialized from <c>ASLM_Model.json</c>.
    /// </summary>
    public class ModelConfig
    {
        /// <summary>
        /// Schema version of the JSON file. Current version: 1.
        /// Used to maintain backward compatibility when the file structure changes.
        /// </summary>
        [JsonPropertyName("fileVersion")]
        public int FileVersion { get; set; } = 1;

        /// <summary>
        /// Unique identifier for the model.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name of the model.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the model's capabilities.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Model version string.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Type of the resource (default "model").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "model";

        /// <summary>
        /// Category of the model (e.g., "ASR", "LLM").
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        // --- Source & Files --------------------------------------------------

        /// <summary>
        /// Information about where the model files are hosted.
        /// </summary>
        [JsonPropertyName("source")]
        public ModelSource Source { get; set; } = new();

        /// <summary>
        /// List of filenames to download. If empty, the installer will automatically 
        /// fetch the file list from the provider (e.g. HuggingFace).
        /// </summary>
        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = [];

        // --- Status ----------------------------------------------------------

        /// <summary>
        /// Current installation status of the model.
        /// </summary>
        [JsonPropertyName("status")]
        public ModelStatus Status { get; set; } = new();

        /// <summary>
        /// Absolute path to the JSON file this config was loaded from.
        /// Set at runtime after deserialization; not serialized to disk.
        /// </summary>
        [JsonIgnore]
        public string SourcePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Defines where the model is hosted.
    /// </summary>
    public class ModelSource
    {
        /// <summary>
        /// The type of source provider (default "huggingface").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "huggingface";

        /// <summary>
        /// The repository ID (e.g., "openai/whisper-large-v3").
        /// </summary>
        [JsonPropertyName("repoId")]
        public string RepoId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tracks the installation state of the model.
    /// </summary>
    public class ModelStatus
    {
        /// <summary>
        /// Gets or sets whether the model is fully installed.
        /// </summary>
        [JsonPropertyName("installed")]
        public bool Installed { get; set; }

        /// <summary>
        /// The version of the model currently installed.
        /// </summary>
        [JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }

        /// <summary>
        /// Timestamp of the last check or installation.
        /// </summary>
        [JsonPropertyName("lastChecked")]
        public string? LastChecked { get; set; }
    }
}
