using System.Text.Json.Serialization;

namespace ASLM.Models
{
    /// <summary>
    /// Configuration for a machine learning model, deserialized from <c>ASLM_Model.json</c>.
    /// </summary>
    public class ModelConfig
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
        public string Type { get; set; } = "model";

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        // --- Source & Files --------------------------------------------------

        [JsonPropertyName("source")]
        public ModelSource Source { get; set; } = new();

        /// <summary>
        /// List of filenames to download. If empty, the installer will automatically 
        /// fetch the file list from the provider (e.g. HuggingFace).
        /// </summary>
        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = [];

        // --- Status ----------------------------------------------------------

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
        [JsonPropertyName("type")]
        public string Type { get; set; } = "huggingface"; // default

        [JsonPropertyName("repoId")]
        public string RepoId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tracks the installation state of the model.
    /// </summary>
    public class ModelStatus
    {
        [JsonPropertyName("installed")]
        public bool Installed { get; set; }

        [JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }

        [JsonPropertyName("lastChecked")]
        public string? LastChecked { get; set; }
    }
}
