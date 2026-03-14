using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    /// <summary>
    /// Manages port assignment for modules.
    /// Allocates one port per module from the configured ranges and persists
    /// the mapping in <c>Data/App/ASLM_Ports.json</c> so assignments survive restarts.
    /// </summary>
    public class PortManager
    {
        private readonly AppDataService _appData;
        private readonly string _portMapPath;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // moduleId -> portName -> assigned port
        private Dictionary<string, Dictionary<string, int>> _portMap = [];
        private bool _loaded;
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of <see cref="PortManager"/>.
        /// </summary>
        public PortManager(AppDataService appData)
        {
            _appData = appData;

            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var rootDir = Directory.GetParent(appDir)?.FullName ?? appDir;
            _portMapPath = Path.Combine(rootDir, "Data", "App", "ASLM_Ports.json");
        }

        // --- Public API -------------------------------------------------------

        /// <summary>
        /// Returns the ports assigned to a module, allocating as needed based on its settings.
        /// </summary>
        /// <param name="module">The module configuration.</param>
        /// <returns>A dictionary of assigned port numbers.</returns>
        public IReadOnlyDictionary<string, int> GetOrAssignPorts(ModuleConfig module)
        {
            lock (_lock)
            {
                EnsureLoaded();

                var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                var rootDir = Directory.GetParent(appDir)?.FullName ?? appDir;
                var modulesRoot = Path.Combine(rootDir, "Modules");
                if (Directory.Exists(modulesRoot))
                {
                    var activeModuleIds = Directory.GetDirectories(modulesRoot).Select(Path.GetFileName).OfType<string>();
                    CleanupOrphanedPorts(activeModuleIds);
                }

                var portSettings = module.Settings?.Where(s => s.Type.Equals("port", StringComparison.OrdinalIgnoreCase)).Select(s => s.Key).ToList() ?? [];
                if (portSettings.Count == 0) portSettings.Add("http"); // Fallback

                if (!_portMap.TryGetValue(module.Id, out var existing))
                {
                    existing = new Dictionary<string, int>();
                    _portMap[module.Id] = existing;
                }

                bool changed = false;

                // Remove ports no longer needed
                var keysToRemove = existing.Keys.Except(portSettings).ToList();
                foreach (var k in keysToRemove)
                {
                    existing.Remove(k);
                    changed = true;
                }

                // Allocate missing or invalid ports
                foreach (var k in portSettings)
                {
                    if (existing.TryGetValue(k, out var p) && IsPortValid(module, p))
                        continue;

                    existing[k] = AllocatePort(module);
                    changed = true;
                }

                if (changed)
                    SavePortMap();

                return existing;
            }
        }

        /// <summary>
        /// Returns a specific port assigned to a module, or null if none has been assigned yet.
        /// </summary>
        public int? TryGetPort(string moduleId, string portKey = "port")
        {
            lock (_lock)
            {
                EnsureLoaded();
                if (_portMap.TryGetValue(moduleId, out var ports))
                {
                    if (ports.TryGetValue(portKey, out var port)) return port;
                    if (ports.ContainsKey("http")) return ports["http"];
                    if (ports.Count > 0) return ports.Values.First();
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the full base URL (http://127.0.0.1:{port}/) for a module's web page.
        /// </summary>
        public string GetModuleUrl(ModuleConfig module)
        {
            var ports = GetOrAssignPorts(module);
            var port = ports.ContainsKey("port") ? ports["port"] : ports.ContainsKey("http") ? ports["http"] : ports.Values.FirstOrDefault();
            return $"http://127.0.0.1:{port}/";
        }

        /// <summary>
        /// Removes port assignments for a specific module.
        /// </summary>
        /// <param name="moduleId">The ID of the module to remove.</param>
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

        /// <summary>
        /// Clears port assignments for any modules not present in the provided list of active module IDs.
        /// </summary>
        /// <param name="activeModuleIds">A collection of currently active/installed module IDs.</param>
        public void CleanupOrphanedPorts(IEnumerable<string> activeModuleIds)
        {
            lock (_lock)
            {
                EnsureLoaded();

                var activeSet = new HashSet<string>(activeModuleIds);
                var orphanedIds = _portMap.Keys.Where(id => !activeSet.Contains(id)).ToList();

                if (orphanedIds.Count > 0)
                {
                    foreach (var id in orphanedIds)
                    {
                        _portMap.Remove(id);
                    }
                    SavePortMap();
                }
            }
        }

        // --- Private ----------------------------------------------------------

        private bool IsPortValid(ModuleConfig module, int port)
        {
            var ports = _appData.Data.Ports;
            bool isOfficial = !module.Id.Contains('.');

            int start = isOfficial ? ports.OfficialStart : ports.ThirdPartyStart;
            int count = isOfficial ? ports.OfficialCount : ports.ThirdPartyCount;

            // Check if it's within the currently configured range
            if (port >= start && port < (start + count))
                return true;

            // Check if it's in the fallback range (40000-50000)
            if (port >= 40000 && port < 50000)
                return true;

            return false;
        }

        private int AllocatePort(ModuleConfig module)
        {
            var ports = _appData.Data.Ports;

            // Determine which range to use based on module author / type.
            // Official modules (no domain prefix in id) get the official range.
            bool isOfficial = !module.Id.Contains('.');

            int start = isOfficial ? ports.OfficialStart : ports.ThirdPartyStart;
            int count = isOfficial ? ports.OfficialCount : ports.ThirdPartyCount;

            var usedPorts = new HashSet<int>(_portMap.Values.SelectMany(v => v.Values));

            for (int offset = 0; offset < count; offset++)
            {
                var candidate = start + offset;
                if (!usedPorts.Contains(candidate))
                    return candidate;
            }

            // Fallback: range exhausted, probe the high ephemeral band deterministically.
            const int fallbackStart = 40000;
            const int fallbackCount = 10000;
            var seed = (int)(uint)module.Id.GetHashCode();

            for (int offset = 0; offset < fallbackCount; offset++)
            {
                var candidate = fallbackStart + ((seed + offset) % fallbackCount);
                if (!usedPorts.Contains(candidate))
                    return candidate;
            }

            throw new InvalidOperationException("No ports are available in the configured or fallback ranges.");
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            if (!File.Exists(_portMapPath))
                return;

            try
            {
                var json = File.ReadAllText(_portMapPath);
                try
                {
                    _portMap = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json, _jsonOptions)
                               ?? [];
                }
                catch
                {
                    // Migration from old schema
                    var oldMap = JsonSerializer.Deserialize<Dictionary<string, int>>(json, _jsonOptions);
                    _portMap = oldMap?.ToDictionary(kvp => kvp.Key, kvp => new Dictionary<string, int> { { "port", kvp.Value } }) ?? [];
                }
            }
            catch
            {
                _portMap = [];
            }
        }

        private void SavePortMap()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_portMapPath)!);
            var json = JsonSerializer.Serialize(_portMap, _jsonOptions);
            File.WriteAllText(_portMapPath, json);
        }
    }
}
