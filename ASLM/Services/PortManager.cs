// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    // Port manager

    /// <summary>
    /// Allocates and persists per-module ports inside the configured application ranges.
    /// </summary>
    public class PortManager
    {
        private readonly AppDataService _appData;
        private readonly string _portMapPath;
        private readonly object _lock = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // moduleId -> portName -> assigned port
        private Dictionary<string, Dictionary<string, int>> _portMap = [];
        private bool _loaded;

        // Initialization

        /// <summary>
        /// Creates the port manager.
        /// </summary>
        public PortManager(AppDataService appData)
        {
            _appData = appData;

            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var rootDir = Directory.GetParent(appDir)?.FullName ?? appDir;
            _portMapPath = Path.Combine(rootDir, "Data", "App", "ASLM_Ports.json");
        }


        // Port access

        /// <summary>
        /// Returns all ports assigned to a module and allocates missing ones when needed.
        /// </summary>
        public IReadOnlyDictionary<string, int> GetOrAssignPorts(ModuleConfig module)
        {
            lock (_lock)
            {
                EnsureLoaded();

                // Remove stale assignments before calculating new ones.
                var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                var rootDir = Directory.GetParent(appDir)?.FullName ?? appDir;
                var modulesRoot = Path.Combine(rootDir, "Modules");

                if (Directory.Exists(modulesRoot))
                {
                    var activeModuleIds = Directory.GetDirectories(modulesRoot)
                        .Select(Path.GetFileName)
                        .OfType<string>();
                    CleanupOrphanedPorts(activeModuleIds);
                }

                var portSettings = module.Settings?
                    .Where(setting => setting.Type.Equals("port", StringComparison.OrdinalIgnoreCase))
                    .Select(setting => setting.Key)
                    .ToList() ?? [];

                if (portSettings.Count == 0)
                {
                    portSettings.Add("http");
                }

                if (!_portMap.TryGetValue(module.Id, out var existing))
                {
                    existing = new Dictionary<string, int>();
                    _portMap[module.Id] = existing;
                }

                var changed = false;

                // Remove assignments that no longer correspond to current port settings.
                var keysToRemove = existing.Keys.Except(portSettings).ToList();
                foreach (var key in keysToRemove)
                {
                    existing.Remove(key);
                    changed = true;
                }

                // Allocate any missing or invalid ports inside the active range.
                foreach (var key in portSettings)
                {
                    if (existing.TryGetValue(key, out var port) && IsPortValid(module, port))
                    {
                        continue;
                    }

                    existing[key] = AllocatePort(module);
                    changed = true;
                }

                if (changed)
                {
                    SavePortMap();
                }

                return existing;
            }
        }

        // Single port

        /// <summary>
        /// Returns one assigned port for a module when it already exists.
        /// </summary>
        public int? TryGetPort(string moduleId, string portKey = "port")
        {
            lock (_lock)
            {
                EnsureLoaded();

                if (_portMap.TryGetValue(moduleId, out var ports))
                {
                    if (ports.TryGetValue(portKey, out var port))
                    {
                        return port;
                    }

                    if (ports.ContainsKey("http"))
                    {
                        return ports["http"];
                    }

                    if (ports.Count > 0)
                    {
                        return ports.Values.First();
                    }
                }

                return null;
            }
        }

        // Module URL

        /// <summary>
        /// Returns the base HTTP URL for a module page.
        /// </summary>
        public string GetModuleUrl(ModuleConfig module)
        {
            var ports = GetOrAssignPorts(module);
            var port = ports.ContainsKey("port")
                ? ports["port"]
                : ports.ContainsKey("http")
                    ? ports["http"]
                    : ports.Values.FirstOrDefault();

            return $"http://127.0.0.1:{port}/";
        }

        // Module cleanup

        /// <summary>
        /// Removes all port assignments for one module.
        /// </summary>
        public void RemoveModulePorts(string moduleId)
        {
            lock (_lock)
            {
                EnsureLoaded();

                if (_portMap.Remove(moduleId))
                {
                    SavePortMap();
                }
            }
        }

        // Orphan cleanup

        /// <summary>
        /// Removes assignments for modules that are no longer present.
        /// </summary>
        public void CleanupOrphanedPorts(IEnumerable<string> activeModuleIds)
        {
            lock (_lock)
            {
                EnsureLoaded();

                var activeSet = new HashSet<string>(activeModuleIds);
                var orphanedIds = _portMap.Keys.Where(id => !activeSet.Contains(id)).ToList();

                if (orphanedIds.Count == 0)
                {
                    return;
                }

                foreach (var id in orphanedIds)
                {
                    _portMap.Remove(id);
                }

                SavePortMap();
            }
        }


        // Port validation

        /// <summary>
        /// Checks whether an assigned port is still valid for the current module ranges.
        /// </summary>
        private bool IsPortValid(ModuleConfig module, int port)
        {
            var ports = _appData.Data.Ports;
            var isOfficial = !module.Id.Contains('.');

            var start = isOfficial ? ports.OfficialStart : ports.ThirdPartyStart;
            var count = isOfficial ? ports.OfficialCount : ports.ThirdPartyCount;

            // Accept ports inside the configured range for the module category.
            if (port >= start && port < start + count)
            {
                return true;
            }

            // Keep older fallback assignments valid to avoid unnecessary churn.
            return port >= 40000 && port < 50000;
        }

        // Port allocation

        /// <summary>
        /// Allocates the next available port for a module.
        /// </summary>
        private int AllocatePort(ModuleConfig module)
        {
            var ports = _appData.Data.Ports;
            var isOfficial = !module.Id.Contains('.');

            var start = isOfficial ? ports.OfficialStart : ports.ThirdPartyStart;
            var count = isOfficial ? ports.OfficialCount : ports.ThirdPartyCount;
            var usedPorts = new HashSet<int>(_portMap.Values.SelectMany(value => value.Values));

            // Try the configured range first so assignments stay predictable.
            for (var offset = 0; offset < count; offset++)
            {
                var candidate = start + offset;
                if (!usedPorts.Contains(candidate))
                {
                    return candidate;
                }
            }

            // Fall back to a deterministic high range when the configured range is exhausted.
            const int fallbackStart = 40000;
            const int fallbackCount = 10000;
            var seed = (int)(uint)module.Id.GetHashCode();

            for (var offset = 0; offset < fallbackCount; offset++)
            {
                var candidate = fallbackStart + ((seed + offset) % fallbackCount);
                if (!usedPorts.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No ports are available in the configured or fallback ranges.");
        }

        // Lazy load

        /// <summary>
        /// Loads the persisted port map once on first access.
        /// </summary>
        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            if (!File.Exists(_portMapPath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_portMapPath);

                try
                {
                    _portMap = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json, _jsonOptions) ?? [];
                }
                catch
                {
                    // Migrate the old flat schema into the current per-port-key format.
                    var oldMap = JsonSerializer.Deserialize<Dictionary<string, int>>(json, _jsonOptions);
                    _portMap = oldMap?.ToDictionary(
                        pair => pair.Key,
                        pair => new Dictionary<string, int> { { "port", pair.Value } }) ?? [];
                }
            }
            catch
            {
                _portMap = [];
            }
        }

        // Persistence

        /// <summary>
        /// Saves the current port map to disk.
        /// </summary>
        private void SavePortMap()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_portMapPath)!);

            var json = JsonSerializer.Serialize(_portMap, _jsonOptions);
            File.WriteAllText(_portMapPath, json);
        }
    }
}
