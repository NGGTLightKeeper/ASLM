---
title: "PortRegistryTests"
draft: false
---

## Class `PortRegistryTests`

`ASLM/Tests/Services/PortRegistryTests.cs` — [PortRegistry](../../Services/PortRegistry/) allocation and URL key resolution.

**Helpers:** [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/), [AppDataStore](../../Services/AppDataStore/), [ModuleConfigBuilder](../TestSupport/ModuleConfigBuilder/).

---

## Test methods

#### `public void ResolveModulePagePortKey_prefers_http_setting()`

**Purpose:** When multiple `port`-type settings exist, `http` wins for module page URL key resolution.

| Step | Action |
| --- | --- |
| 1 | `ModuleConfigBuilder.Create` with settings `admin` (port) and `http` (port) |
| 2 | `PortRegistry.ResolveModulePagePortKey(module)` |
| 3 | Assert returns `"http"` |

---

#### `public void GetOrAssignPorts_allocates_from_modules_start()`

**Purpose:** Module `http` port is allocated starting from the configured modules start port.

| Step | Action |
| --- | --- |
| 1 | Layout + `AppDataStore`; set `ModulesStart = 25000` |
| 2 | `new PortRegistry(appData)`; module `id: "sample-module"` |
| 3 | `GetOrAssignPorts(module)` |
| 4 | Assert `ports` contains `http` equal to `25000` |

---

#### `public void EnsurePortsAvailable_keeps_free_ports_unchanged()`

**Purpose:** Validates that already-free ports are kept when `EnsurePortsAvailable` is called.

| Step | Action |
| --- | --- |
| 1 | Layout + `AppDataStore`; set `ModulesStart = 30000` |
| 2 | `new PortRegistry(appData)`; module `id: "availability-module"` |
| 3 | `GetOrAssignPorts(module)` |
| 4 | Assert `EnsurePortsAvailable(module.Id)` returns `false` |

---

#### `public void GetOrAssignInternalServicePort_is_stable_for_service_id()`

**Purpose:** Internal ASLM API port is stable across repeated `GetOrAssignInternalServicePort` calls.

| Step | Action |
| --- | --- |
| 1 | Layout + app data; `ModulesStart = 26000` |
| 2 | Two calls with `AslmApiServiceId` / `AslmApiPortKey` |
| 3 | Assert second equals first; `TryGetInternalServicePort` matches |

---

#### `public void RemoveModulePorts_clears_assignments()`

**Purpose:** `RemoveModulePorts` drops module port map entries.

| Step | Action |
| --- | --- |
| 1 | `GetOrAssignPorts(module)` then `RemoveModulePorts(module.Id)` |
| 2 | `TryGetPort(module.Id, "http")` → assert `null` |

---

## Related

- [PortRegistry](../../Services/PortRegistry/)
