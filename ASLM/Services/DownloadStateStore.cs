// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services
{
    // Download catalog state
    // Persist shared resource installation state
    public class DownloadStateStore
    {
        private readonly string _filePath;
        private readonly ILogger<DownloadStateStore> _logger;
        private readonly object _lock = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private DownloadCatalogStateFile? _state;

        // Initialization
        // Build the state service and resolve the storage path
        public DownloadStateStore(ILogger<DownloadStateStore> logger)
        {
            _logger = logger;

            var rootDir = GetRootDirectory();
            _filePath = Path.Combine(rootDir, "Data", "App", "ASLM_Downloads.json");
        }

        // State access
        // Return the persisted state for one resource
        public DownloadCatalogResourceState? GetResourceState(string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return null;
            }

            lock (_lock)
            {
                var state = EnsureLoaded();
                return state.Resources.TryGetValue(resourceKey, out var resourceState)
                    ? resourceState
                    : null;
            }
        }

        // State writes
        // Mark one resource as installed and persist the change
        public async Task MarkInstalledAsync(string resourceKey, string version, string providerModuleId)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return;
            }

            lock (_lock)
            {
                var state = EnsureLoaded();
                state.Resources[resourceKey] = new DownloadCatalogResourceState
                {
                    Installed = true,
                    InstalledVersion = string.IsNullOrWhiteSpace(version) ? null : version,
                    LastInstalledUtc = DateTime.UtcNow.ToString("o"),
                    ProviderModuleId = string.IsNullOrWhiteSpace(providerModuleId) ? null : providerModuleId
                };
            }

            await SaveAsync();
        }

        // Mark one resource as removed and persist the change
        public async Task MarkUninstalledAsync(string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return;
            }

            lock (_lock)
            {
                var state = EnsureLoaded();
                state.Resources[resourceKey] = new DownloadCatalogResourceState
                {
                    Installed = false,
                    InstalledVersion = null,
                    LastInstalledUtc = null,
                    ProviderModuleId = null
                };
            }

            await SaveAsync();
        }


        // Loading and saving
        // Load the persisted state on first access
        private DownloadCatalogStateFile EnsureLoaded()
        {
            if (_state != null)
            {
                return _state;
            }

            try
            {
                // Read the existing file only once and normalize it before reuse
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        _state = JsonSerializer.Deserialize<DownloadCatalogStateFile>(json, _jsonOptions) ?? new DownloadCatalogStateFile();

                        _state.Normalize();
                        return _state;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load download catalog state from {FilePath}. Falling back to defaults.", _filePath);
            }

            _state = new DownloadCatalogStateFile();
            _state.Normalize();
            return _state;
        }

        // Persist the current state snapshot to disk
        private async Task SaveAsync()
        {
            DownloadCatalogStateFile snapshot;

            lock (_lock)
            {
                snapshot = EnsureLoaded();
            }

            try
            {
                // Ensure the state directory exists before writing the serialized file
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save download catalog state to {FilePath}.", _filePath);
            }
        }


        // Path helpers
        // Resolve the application root directory
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }
    }
}
