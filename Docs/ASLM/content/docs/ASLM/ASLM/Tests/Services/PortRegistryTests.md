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

#### `public void GetOrAssignPorts_allocates_within_official_range()`

**Purpose:** Official module `http` port is allocated inside configured official range.

| Step | Action |
| --- | --- |
| 1 | Layout + `AppDataStore`; set `OfficialStart = 25000`, `OfficialCount = 50` |
| 2 | `new PortRegistry(appData)`; module `id: "official-module"` |
| 3 | `GetOrAssignPorts(module)` |
| 4 | Assert `ports` contains `http` in range `25000`…`25049` |

---

#### `public void GetOrAssignInternalServicePort_is_stable_for_service_id()`

**Purpose:** Internal ASLM API port is stable across repeated `GetOrAssignInternalServicePort` calls.

| Step | Action |
| --- | --- |
| 1 | Layout + app data; `OfficialStart = 26000`, `OfficialCount = 100` |
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
