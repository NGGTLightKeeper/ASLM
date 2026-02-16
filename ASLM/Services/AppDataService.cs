using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    /// <summary>
    /// Manages the persistent application data stored in <c>Data/App/ASLM_Data.json</c>.
    /// Registered as a singleton — loads once at startup, saves on demand.
    /// </summary>
    public class AppDataService
    {
        private readonly string _filePath;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>Current application data. Never null after construction.</summary>
        public AppData Data { get; private set; } = new();

        public AppDataService()
        {
            var rootDir = GetRootDirectory();
            _filePath = Path.Combine(rootDir, "Data", "App", "ASLM_Data.json");
            Load();
        }

        /// <summary>
        /// Loads data from disk. Creates default data if file is missing or empty.
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        Data = JsonSerializer.Deserialize<AppData>(json, _jsonOptions) ?? new AppData();
                        return;
                    }
                }
            }
            catch
            {
                // Corrupted file — fall through to defaults.
            }

            Data = new AppData();
            Save();
        }

        /// <summary>
        /// Persists the current <see cref="Data"/> to disk synchronously.
        /// </summary>
        public void Save()
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(Data, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }

        /// <summary>
        /// Persists the current <see cref="Data"/> to disk asynchronously.
        /// </summary>
        public async Task SaveAsync()
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(Data, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }

        /// <summary>Whether the first-run wizard has been completed.</summary>
        public bool IsFirstRun => !Data.FirstRunCompleted;

        /// <summary>
        /// Returns the application root directory (one level above the App/ directory).
        /// </summary>
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }
    }
}
