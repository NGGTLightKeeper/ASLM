---
title: "ModuleInteropPortsBuilder"
draft: false
---

## Class `ModuleInteropPortsBuilder`

`ASLM/Services/ModuleInteropPortsBuilder.cs` — **`internal static`** — pure helper that assembles port and host payload DTOs for the module interop API. All methods are side-effect-free and do not allocate new ports.

---

## Internal types

#### `internal sealed record AslmApiInfoDto(bool Enabled, bool? Running, int? Port, string? BaseUrl)`

Intermediate DTO for ASLM API server state. `Running` and `Port` are `null` when `Enabled` is `false`.

---

#### `internal sealed record ModuleHostDto(string HostKey, string RouteKey, int Port, string TargetUrl, string? MirrorUrl)`

One port-map entry for a running module. `RouteKey` is the URL segment used by the ASLM API mirror (e.g. `ui-port` → `ui`). `MirrorUrl` is `null` when `apiMirrorBaseUrl` is `null`.

---

#### `internal sealed record RunningModulePortsDto(string Id, string Name, string InstanceFolder, string SourcePath, string? PageUrl, List<ModuleHostDto> Hosts)`

Assembled port information for one running module instance.

---

## Methods

#### `internal static AslmApiInfoDto BuildAslmApiDto(bool apiEnabled, int? apiPort, bool apiRunning)`

**Purpose:** Builds the `aslmApi` block. When `apiEnabled` is `false` all optional fields are `null`. When enabled, fills `Port` and `BaseUrl` from `apiPort`, and `Running` from `apiRunning`.

---

#### `internal static List<ModuleHostDto> BuildHosts(ModuleConfig module, IReadOnlyDictionary<string, int>? assignedPorts, string? apiMirrorBaseUrl)`

**Purpose:** Maps each assigned port-map entry to a `ModuleHostDto`. Skips entries with port ≤ 0. `RouteKey` is resolved via `PortRegistry.BuildHostRouteKey`. `MirrorUrl` is formed as `{apiMirrorBaseUrl}/{moduleId}/{routeKey}/` when `apiMirrorBaseUrl` is not `null`.

---

#### `internal static RunningModulePortsDto BuildRunningModulePorts(ModuleConfig module, PortRegistry portRegistry, string? apiMirrorBaseUrl)`

**Purpose:** Calls `PortRegistry.TryGetAssignedPorts` and `PortRegistry.TryGetModulePageUrl` (read-only, no allocation), then delegates to `BuildHosts`. Returns a `RunningModulePortsDto` with `PageUrl: null` and empty `Hosts` when no ports are assigned.

---

#### `internal static string? ResolveMirrorBaseUrl(bool apiEnabled, int? apiPort)`

**Purpose:** Returns `PortRegistry.BuildLoopbackUrl(apiPort.Value)` when the API is enabled and a port is available; otherwise `null`. Used to pass a single mirror base URL into `BuildHosts`.

---

## Related

- [AslmModuleInteropServer](AslmModuleInteropServer/)
- [PortRegistry](PortRegistry/)
- [ModuleRunner](ModuleRunner/)
