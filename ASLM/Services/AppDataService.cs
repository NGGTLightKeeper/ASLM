using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services
{
    /// <summary>
    /// Manages the persistent application data stored in <c>Data/App/ASLM_Data.json</c>.
    /// Registered as a singleton — loads once at startup, saves on demand.
    /// </summary>
    public class AppDataService
    {
        private readonly string _filePath;
        private readonly ILogger<AppDataService> _logger;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Gets the current application data. This property is never null after construction.
        /// </summary>
        public AppData Data { get; private set; } = new();

        /// <summary>
        /// Gets a value indicating whether the first-run wizard has been completed.
        /// </summary>
        public bool IsFirstRun => !Data.FirstRunCompleted;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppDataService"/> class.
        /// automatically loading data from disk.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public AppDataService(ILogger<AppDataService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var rootDir = GetRootDirectory();
            _filePath = Path.Combine(rootDir, "Data", "App", "ASLM_Data.json");
            Load();
        }

        /// <summary>
        /// Loads data from disk synchronously. Creates default data if the file is missing or empty.
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load app data from {FilePath}. Corrupted file — falling back to defaults.", _filePath);
                // Corrupted file — fall through to defaults.
                // In a production app, we might want to backup the corrupted file.
            }

            Data = new AppData();
            Save();
        }

        /// <summary>
        /// Persists the current <see cref="Data"/> to disk synchronously.
        /// </summary>
        public void Save()
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(Data, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }

        /// <summary>
        /// Persists the current <see cref="Data"/> to disk asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task SaveAsync()
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(Data, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }

        /// <summary>
        /// Ensures the directory for the data file exists.
        /// </summary>
        private void EnsureDirectoryExists()
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        /// <summary>
        /// Returns the application root directory (one level above the App/ directory).
        /// </summary>
        /// <returns>The full path to the root directory.</returns>
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }
    }
}
