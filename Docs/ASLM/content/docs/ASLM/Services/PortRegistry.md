---
title: "PortRegistry"
draft: false
---

## Class `PortRegistry`

`ASLM/Services/PortRegistry.cs` — **`public`** — allocates, validates, redistributes, and persists TCP ports for modules and internal ASLM listeners. Map file: **`Data/App/ASLM_Ports.json`** (`ownerId` → `portKey` → `int`). Ranges from [AppDataStore](AppDataStore/) **`Ports`**.

**DI:** `AddSingleton<PortRegistry>()` — [AppDataStore](AppDataStore/).

Private record: **`RedistributionOwner`**.

---

### Constants

| Name | Value |
| --- | --- |
| `AslmApiServiceId` | `__aslm-api` |
| `AslmApiPortKey` | `server-port` |
| `AslmModuleInteropServiceId` | `__aslm-module-interop` |
| `AslmModuleInteropPortKey` | `server-port` |

Internal owners use id prefix **`__`**.

---

### Events

| Event | When |
| --- | --- |
| `PortsRedistributed` | After **`RedistributePorts`** changes the map |

---

## Public methods

#### `public PortRegistry(AppDataStore appData)`

**Purpose:** Resolves **`_portMapPath`** under app root **`Data/App/ASLM_Ports.json`**.

---

#### `public IReadOnlyDictionary<string, int> GetOrAssignPorts(ModuleConfig module)`

**Purpose:** Loads map, periodic orphan cleanup, syncs keys with module **`port`** settings (default **`http`**), allocates/replaces invalid ports, saves when changed.

---

#### `public int GetOrAssignInternalServicePort(string serviceId, string portKey)`

**Purpose:** Official-pool allocation for internal services (e.g. API mirror, interop).

---

#### `public int? TryGetInternalServicePort(string serviceId, string portKey)`

**Purpose:** Read without allocating.

---

#### `public bool RedistributePorts(bool reserveAslmApiServer)`

**Purpose:** Rebuilds entire map in stable owner order; reserves API owner when requested; always reserves interop; fires **`PortsRedistributed`** when map changed. Returns whether persisted map changed.

---

#### `public int? TryGetPort(string moduleId, string portKey = "port")`

**Purpose:** Lookup with fallback to **`http`**, then first assigned port.

---

#### `public static string ResolveModulePagePortKey(ModuleConfig module)`

**Purpose:** Prefers **`ui-port`**, then **`http`**, then **`port`**, else first port setting or **`http`**.

---

#### `public string GetModuleUrl(ModuleConfig module)`

**Purpose:** **`http://127.0.0.1:{port}/`** using **`GetOrAssignPorts`** and **`ResolveModulePagePortKey`**.

---

#### `public void RemoveModulePorts(string moduleId)`

**Purpose:** Removes owner entry and saves.

---

#### `public void CleanupOrphanedPorts(IEnumerable<string> activeModuleIds)`

**Purpose:** Removes module ids not in **`activeSet`** (excluding internal **`__`** owners).

---

## Private methods

#### `private void CleanupOrphanedPortsIfDue()`

**Purpose:** Every 30 s, discovers installed module ids from manifests and calls **`CleanupOrphanedPorts`**.

---

#### `private static IEnumerable<string> DiscoverInstalledModuleIds(string modulesRoot)`

**Purpose:** Reads **`id`** from each **`ASLM_Module.json`** (best-effort).

---

#### `private bool IsPortValid(string ownerId, int port)`

**Purpose:** In configured official/third-party range, or legacy **40000–49999** fallback band.

---

#### `private int AllocatePort(string ownerId)`

**Purpose:** Next free port in owner category using global used set.

---

#### `private int AllocatePort(string ownerId, HashSet<int> usedPorts)`

**Purpose:** Explicit used-port set for redistribution.

---

#### `private static int AllocatePort(string ownerId, HashSet<int> usedPorts, int start, int count)`

**Purpose:** Linear scan in range, then deterministic **40000+** hash fallback; throws when exhausted.

---

#### `private List<RedistributionOwner> GetRedistributionOwners(bool reserveAslmApiServer)`

**Purpose:** Stable sort: internal API/interop rank 0, other **`__`** rank 1, official modules 2, dotted third-party 3.

---

#### `private static int GetRedistributionOwnerRank(string ownerId)`

**Purpose:** Ranking helper for redistribution order.

---

#### `private static bool PortMapsEqual(...)`

**Purpose:** Deep equality of nested dictionaries.

---

#### `private static bool IsInternalServiceId(string ownerId)`

**Purpose:** Starts with **`__`**.

---

#### `private void EnsureLoaded()`

**Purpose:** Lazy load JSON; migrates flat **`moduleId → int`** schema to per-key map.

---

#### `private void SavePortMap()`

**Purpose:** Writes indented JSON.

---

## Related types (same file)

### `RedistributionOwner` (private record)

`Id`, `PortKeys`, `FirstPort` — used when rebuilding port assignments.

---

## Related

- [AslmApiServer](AslmApiServer/)
- [AslmModuleInteropServer](AslmModuleInteropServer/)
- [ModuleRunner](ModuleRunner/)
- [AppDataStore](AppDataStore/)
