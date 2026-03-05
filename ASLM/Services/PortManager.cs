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

        // moduleId → assigned port
        private Dictionary<string, int> _portMap = [];
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
        /// Returns the port assigned to a module, allocating one if needed.
        /// </summary>
        /// <param name="module">The module configuration.</param>
        /// <returns>The assigned port number.</returns>
        public int GetOrAssignPort(ModuleConfig module)
        {
            lock (_lock)
            {
                EnsureLoaded();

                if (_portMap.TryGetValue(module.Id, out var existing))
                {
                    if (IsPortValid(module, existing))
                        return existing;
                    
                    // Assigned port is no longer valid (range changed), remove it
                    _portMap.Remove(module.Id);
                }

                var port = AllocatePort(module);
                _portMap[module.Id] = port;
                SavePortMap();
                return port;
            }
        }

        /// <summary>
        /// Returns the port assigned to a module, or null if none has been assigned yet.
        /// </summary>
        public int? TryGetPort(string moduleId)
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _portMap.TryGetValue(moduleId, out var port) ? port : null;
            }
        }

        /// <summary>
        /// Returns the full base URL (http://127.0.0.1:{port}/) for a module's web page.
        /// </summary>
        public string GetModuleUrl(ModuleConfig module)
            => $"http://127.0.0.1:{GetOrAssignPort(module)}/";

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

            var usedPorts = new HashSet<int>(_portMap.Values);

            for (int offset = 0; offset < count; offset++)
            {
                var candidate = start + offset;
                if (!usedPorts.Contains(candidate))
                    return candidate;
            }

            // Fallback: port range exhausted — use a high ephemeral port.
            return 40000 + (Math.Abs(module.Id.GetHashCode()) % 10000);
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
                _portMap = JsonSerializer.Deserialize<Dictionary<string, int>>(json, _jsonOptions)
                           ?? [];
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
