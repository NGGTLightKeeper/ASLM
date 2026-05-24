// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    /// <summary>
    /// Allocates and persists per-module ports inside the configured application ranges.
    /// </summary>
    public class PortRegistry
    {
        // Port-map owner used by the internal ASLM API mirror server.
        public const string AslmApiServiceId = "__aslm-api";

        // Port key used by the internal ASLM API mirror server.
        public const string AslmApiPortKey = "server-port";

        // Port-map owner used by the internal module interop JSON server.
        public const string AslmModuleInteropServiceId = "__aslm-module-interop";

        // Port key used by the internal module interop server.
        public const string AslmModuleInteropPortKey = "server-port";

        private readonly AppDataStore _appData;
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
        private DateTimeOffset _lastOrphanCleanupUtc = DateTimeOffset.MinValue;

        /// <summary>
        /// Raised after runtime port assignments are redistributed and persisted.
        /// </summary>
        public event EventHandler? PortsRedistributed;

        // Initialization

        /// <summary>
        /// Creates the port manager.
        /// </summary>
        public PortRegistry(AppDataStore appData)
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

                CleanupOrphanedPortsIfDue();

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
                    if (existing.TryGetValue(key, out var port) && IsPortValid(module.Id, port))
                    {
                        continue;
                    }

                    existing[key] = AllocatePort(module.Id);
                    changed = true;
                }

                if (changed)
                {
                    SavePortMap();
                }

                return existing;
            }
        }

        /// <summary>
        /// Returns an official-pool port for an internal ASLM service and allocates it when needed.
        /// </summary>
        public int GetOrAssignInternalServicePort(string serviceId, string portKey)
        {
            lock (_lock)
            {
                EnsureLoaded();

                if (!_portMap.TryGetValue(serviceId, out var existing))
                {
                    existing = new Dictionary<string, int>();
                    _portMap[serviceId] = existing;
                }

                if (existing.TryGetValue(portKey, out var port) && IsPortValid(serviceId, port))
                {
                    return port;
                }

                // Internal ASLM services use the same official range and still reserve the port
                // in the shared runtime port map so modules cannot receive it later.
                port = AllocatePort(serviceId);
                existing[portKey] = port;
                SavePortMap();
                return port;
            }
        }

        /// <summary>
        /// Returns an existing internal service port without allocating a new one.
        /// </summary>
        public int? TryGetInternalServicePort(string serviceId, string portKey)
        {
            lock (_lock)
            {
                EnsureLoaded();

                return _portMap.TryGetValue(serviceId, out var existing) &&
                       existing.TryGetValue(portKey, out var port)
                    ? port
                    : null;
            }
        }

        /// <summary>
        /// Rebuilds persisted port assignments for modules and internal listeners.
        /// When <paramref name="reserveAslmApiServer"/> is true, reserves the ASLM API mirror port; always reserves the module interop listener port.
        /// </summary>
        public bool RedistributePorts(bool reserveAslmApiServer)
        {
            var changed = false;

            lock (_lock)
            {
                EnsureLoaded();

                if (reserveAslmApiServer)
                {
                    if (!_portMap.TryGetValue(AslmApiServiceId, out var apiPorts))
                    {
                        apiPorts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        _portMap[AslmApiServiceId] = apiPorts;
                    }

                    apiPorts.TryAdd(AslmApiPortKey, 0);
                }
                else
                {
                    _portMap.Remove(AslmApiServiceId);
                }

                if (!_portMap.TryGetValue(AslmModuleInteropServiceId, out var interopPorts))
                {
                    interopPorts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    _portMap[AslmModuleInteropServiceId] = interopPorts;
                }

                interopPorts.TryAdd(AslmModuleInteropPortKey, 0);

                var nextMap = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
                var usedPorts = new HashSet<int>();

                foreach (var owner in GetRedistributionOwners(reserveAslmApiServer))
                {
                    if (!nextMap.TryGetValue(owner.Id, out var ports))
                    {
                        ports = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        nextMap[owner.Id] = ports;
                    }

                    foreach (var portKey in owner.PortKeys)
                    {
                        ports[portKey] = AllocatePort(owner.Id, usedPorts);
                    }
                }

                if (PortMapsEqual(_portMap, nextMap))
                {
                    return false;
                }

                _portMap = nextMap;
                SavePortMap();
                changed = true;
            }

            PortsRedistributed?.Invoke(this, EventArgs.Empty);
            return changed;
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
        /// Returns the port-map key that should back a module WebView page.
        /// Prefers explicit UI listen ports over legacy <c>port</c>/<c>http</c> keys.
        /// </summary>
        public static string ResolveModulePagePortKey(ModuleConfig module)
        {
            var portKeys = module.Settings?
                .Where(setting => setting.Type.Equals("port", StringComparison.OrdinalIgnoreCase))
                .Select(setting => setting.Key)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToList() ?? [];

            if (portKeys.Count == 0)
            {
                return "http";
            }

            foreach (var preferred in new[] { "ui-port", "http", "port" })
            {
                var match = portKeys.FirstOrDefault(key =>
                    key.Equals(preferred, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            return portKeys[0];
        }

        /// <summary>
        /// Returns the base HTTP URL for a module page.
        /// </summary>
        public string GetModuleUrl(ModuleConfig module)
        {
            var ports = GetOrAssignPorts(module);
            var portKey = ResolveModulePagePortKey(module);

            if (!ports.TryGetValue(portKey, out var port) || port <= 0)
            {
                port = ports.Values.FirstOrDefault(value => value > 0);
            }

            if (port <= 0)
            {
                throw new InvalidOperationException($"No port assigned for module '{module.Id}'.");
            }

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
                var orphanedIds = _portMap.Keys
                    .Where(id => !activeSet.Contains(id) && !IsInternalServiceId(id))
                    .ToList();

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

        /// <summary>
        /// Periodically removes stale assignments without scanning Modules on every port lookup.
        /// </summary>
        private void CleanupOrphanedPortsIfDue()
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastOrphanCleanupUtc < TimeSpan.FromSeconds(30))
            {
                return;
            }

            _lastOrphanCleanupUtc = now;

            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var rootDir = Directory.GetParent(appDir)?.FullName ?? appDir;
            var modulesRoot = Path.Combine(rootDir, "Modules");

            if (!Directory.Exists(modulesRoot))
            {
                return;
            }

            var activeModuleIds = Directory.GetDirectories(modulesRoot)
                .Select(Path.GetFileName)
                .OfType<string>();
            CleanupOrphanedPorts(activeModuleIds);
        }


        // Port validation

        /// <summary>
        /// Checks whether an assigned port is still valid for the current module ranges.
        /// </summary>
        private bool IsPortValid(string ownerId, int port)
        {
            var ports = _appData.Data.Ports;
            var isOfficial = !ownerId.Contains('.');

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
        private int AllocatePort(string ownerId)
        {
            var ports = _appData.Data.Ports;
            var isOfficial = !ownerId.Contains('.');

            var start = isOfficial ? ports.OfficialStart : ports.ThirdPartyStart;
            var count = isOfficial ? ports.OfficialCount : ports.ThirdPartyCount;
            var usedPorts = new HashSet<int>(_portMap.Values.SelectMany(value => value.Values));

            return AllocatePort(ownerId, usedPorts, start, count);
        }

        /// <summary>
        /// Allocates the next available port for a module using an explicit used-port set.
        /// </summary>
        private int AllocatePort(string ownerId, HashSet<int> usedPorts)
        {
            var ports = _appData.Data.Ports;
            var isOfficial = !ownerId.Contains('.');

            var start = isOfficial ? ports.OfficialStart : ports.ThirdPartyStart;
            var count = isOfficial ? ports.OfficialCount : ports.ThirdPartyCount;
            return AllocatePort(ownerId, usedPorts, start, count);
        }

        /// <summary>
        /// Allocates the next available port inside the requested range or deterministic fallback.
        /// </summary>
        private static int AllocatePort(string ownerId, HashSet<int> usedPorts, int start, int count)
        {
            // Try the configured range first so assignments stay predictable.
            for (var offset = 0; offset < count; offset++)
            {
                var candidate = start + offset;
                if (!usedPorts.Contains(candidate))
                {
                    usedPorts.Add(candidate);
                    return candidate;
                }
            }

            // Fall back to a deterministic high range when the configured range is exhausted.
            const int fallbackStart = 40000;
            const int fallbackCount = 10000;
            var seed = (int)(uint)ownerId.GetHashCode();

            for (var offset = 0; offset < fallbackCount; offset++)
            {
                var candidate = fallbackStart + ((seed + offset) % fallbackCount);
                if (!usedPorts.Contains(candidate))
                {
                    usedPorts.Add(candidate);
                    return candidate;
                }
            }

            throw new InvalidOperationException("No ports are available in the configured or fallback ranges.");
        }

        /// <summary>
        /// Returns the owners and port keys in stable redistribution order.
        /// </summary>
        private List<RedistributionOwner> GetRedistributionOwners(bool reserveAslmApiServer)
        {
            return _portMap
                .Where(pair =>
                    reserveAslmApiServer || !pair.Key.Equals(AslmApiServiceId, StringComparison.OrdinalIgnoreCase))
                .Where(static pair => pair.Value.Count > 0)
                .Select(pair => new RedistributionOwner(
                    pair.Key,
                    pair.Value
                        .OrderBy(static port => port.Value)
                        .ThenBy(static port => port.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(static port => port.Key)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    pair.Value.Values.DefaultIfEmpty(int.MaxValue).Min()))
                .OrderBy(static owner => GetRedistributionOwnerRank(owner.Id))
                .ThenBy(static owner => owner.FirstPort)
                .ThenBy(static owner => owner.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Sorts internal and official owners before third-party owners during redistribution.
        /// </summary>
        private static int GetRedistributionOwnerRank(string ownerId)
        {
            if (ownerId.Equals(AslmApiServiceId, StringComparison.OrdinalIgnoreCase) ||
                ownerId.Equals(AslmModuleInteropServiceId, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (IsInternalServiceId(ownerId))
            {
                return 1;
            }

            return ownerId.Contains('.') ? 3 : 2;
        }

        /// <summary>
        /// Compares two port maps without relying on dictionary insertion order.
        /// </summary>
        private static bool PortMapsEqual(
            Dictionary<string, Dictionary<string, int>> left,
            Dictionary<string, Dictionary<string, int>> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (var owner in left)
            {
                if (!right.TryGetValue(owner.Key, out var rightPorts) ||
                    owner.Value.Count != rightPorts.Count)
                {
                    return false;
                }

                foreach (var port in owner.Value)
                {
                    if (!rightPorts.TryGetValue(port.Key, out var rightPort) ||
                        rightPort != port.Value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Returns whether a port map owner is managed by ASLM itself rather than by an installed module folder.
        /// </summary>
        private static bool IsInternalServiceId(string ownerId)
        {
            return ownerId.StartsWith("__", StringComparison.Ordinal);
        }

        /// <summary>
        /// Stores one owner and its port keys while rebuilding the runtime map.
        /// </summary>
        private sealed record RedistributionOwner(string Id, List<string> PortKeys, int FirstPort);

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
