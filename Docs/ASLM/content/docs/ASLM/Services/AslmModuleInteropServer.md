---
title: "AslmModuleInteropServer"
draft: false
---

## Class `AslmModuleInteropServer`

`ASLM/Services/AslmModuleInteropServer.cs` — **`public sealed`** — local JSON **`HttpListener`** API for module registry, port discovery, and coordinated starts.

**DI:** singleton — [AppDataStore](AppDataStore/), [PortRegistry](PortRegistry/), [AslmApiServer](AslmApiServer/), [ModuleInstaller](ModuleInstaller/), [ModuleRunner](ModuleRunner/), [ModuleLaunchCoordinator](ModuleLaunchCoordinator/), [ModuleInteropHostState](ModuleInteropHostState/).

Implements **`IDisposable`**. Port owner: **`PortRegistry.AslmModuleInteropServiceId`** / **`AslmModuleInteropPortKey`**.

---

### Events

| Event | When |
| --- | --- |
| `StateChanged` | After start, stop, or failed start |

---

### State properties

| Member | Description |
| --- | --- |
| `IsRunning` | Listener is active |
| `Port` | Active or reserved interop port |
| `BaseUrl` | `http://127.0.0.1:{port}/` |
| `LastError` | Most recent startup failure |

---

## Public methods — lifecycle

#### `public AslmModuleInteropServer(AppDataStore appData, PortRegistry ports, AslmApiServer apiServer, ModuleInstaller moduleInstaller, ModuleRunner moduleRunner, ModuleLaunchCoordinator launchCoordinator, ModuleInteropHostState interopHostState, ILogger<AslmModuleInteropServer> logger)`

**Purpose:** Stores dependencies for registry, port exposure, and launch coordination.

---

#### `public async Task EnsureStartedAsync()`

**Purpose:** Calls **`RedistributePorts`** (respecting API server enabled flag), then **`StartAsync`**. Always-on host service.

---

#### `public Task StartAsync()`

**Purpose:** Starts **`HttpListener`** on assigned port, updates **`ModuleInteropHostState`**, runs **`RunListenerLoopAsync`** on background task. On failure: sets **`LastError`**, clears interop state, **`CleanupListener`**. Idempotent when already listening.

---

#### `public async Task StopAsync()`

**Purpose:** Cancels listener, stops/closes **`HttpListener`**, **`CleanupListener`**, clears **`ModuleInteropHostState`**, waits up to 2 s for listener task.

---

#### `public void Dispose()`

**Purpose:** Calls **`StopAsync()`** synchronously once.

---

## Private methods — request loop

#### `private async Task RunListenerLoopAsync(HttpListener listener, CancellationToken ct)`

**Purpose:** Accept loop until cancelled or listener stopped; dispatches each context to **`HandleContextAsync`** via **`Task.Run`**.

---

#### `private async Task HandleContextAsync(HttpListenerContext context)`

**Purpose:** Rejects non-loopback clients (**403**). Routes:

| Method | Path | Handler |
| --- | --- | --- |
| GET | `/v1/registry` | **`HandleRegistryGetAsync`** |
| GET | `/v1/ports` | **`HandlePortsGetAsync`** |
| POST | `/v1/modules/start` | **`HandleModulesStartPostAsync`** |

Unknown routes → **404** JSON error. Unhandled exceptions → **500**.

---

## Private methods — registry

#### `private async Task HandleRegistryGetAsync(HttpListenerContext context)`

**Purpose:** Builds **`RegistryResponse`**: interop base URL, ASLM API state (**`BuildAslmApiResponseDto`**), installed modules (**`BuildInstalledModulesAsync`**), running modules with port/host data (**`BuildRunningModulesResponseDtos`**). Returns **200** JSON.

---

#### `private async Task HandlePortsGetAsync(HttpListenerContext context)`

**Purpose:** Builds **`PortsResponse`**: ASLM API state and running modules with port/host data only — omits the installed-modules list for a lighter-weight response. Returns **200** JSON.

---

#### `private AslmApiResponseDto BuildAslmApiResponseDto()`

**Purpose:** Reads `AppDataStore.Api.ServerEnabled`, resolves the API port from **`PortRegistry`**, and checks `AslmApiServer.IsRunning` to produce the `aslmApi` block. When disabled: `enabled: false`, all other fields `null`.

---

#### `private List<RunningModuleDto> BuildRunningModulesResponseDtos()`

**Purpose:** Calls **`ModuleRunner.GetRunningModuleConfigs()`** (live processes only), then for each config calls **`ModuleInteropPortsBuilder.BuildRunningModulePorts`** to obtain port and host data. Mirror URLs are included when the ASLM API server is enabled.

---

#### `private async Task<List<InstalledModuleDto>> BuildInstalledModulesAsync()`

**Purpose:** **`DiscoverModulesAsync`**, group by module id, one DTO per instance folder (ordered by **`SourcePath`**).

---

## Private methods — module start

#### `private async Task HandleModulesStartPostAsync(HttpListenerContext context)`

**Purpose:** Requires JSON body with **`callerModuleId`** and non-empty **`moduleIds`**. Caller must be in running snapshot. For each id: **`LaunchCoordinator.LaunchOrEnsureRunningAsync`**, maps status via **`MapLaunchStatus`**. Returns **200** with per-module results.

---

#### `private static (string status, string? message) MapLaunchStatus(ModuleLaunchStatus status, string? message)`

**Purpose:** Maps coordinator status to interop strings: `started`, `alreadyRunning`, `notFound`, `noRunCommands`, `firstRunFailed`, `error`.

---

## Private methods — helpers

#### `private static bool IsLoopback(HttpListenerRequest request)`

**Purpose:** **`IPAddress.IsLoopback`** on **`RemoteEndPoint`**.

---

#### `private static string NormalizePath(string path)`

**Purpose:** Trims trailing slash (except root); empty → **`/`**.

---

#### `private async Task WriteJsonAsync<T>(HttpListenerContext context, int statusCode, T payload, CancellationToken ct)`

**Purpose:** Camel-case JSON, UTF-8, closes response.

---

#### `private void RaiseStateChanged()`

**Purpose:** Invokes **`StateChanged`**.

---

#### `private void CleanupListener()`

**Purpose:** Nulls listener, port, CTS, and task; disposes CTS.

---

#### `private int GetAssignedPort()`

**Purpose:** **`PortRegistry.GetOrAssignInternalServicePort`** for interop service id/key.

---

#### `private static string BuildBaseUrl(int port)`

**Purpose:** `http://127.0.0.1:{port}/`

---

## Private JSON DTOs (same file)

#### `private sealed record ErrorEnvelope(string Code, string Message)`

**Purpose:** Standard API error body.

---

#### `private sealed record RegistryResponse(string InteropBaseUrl, AslmApiResponseDto AslmApi, List<InstalledModuleDto> InstalledModules, List<RunningModuleDto> RunningModules)`

**Purpose:** GET `/v1/registry` payload.

---

#### `private sealed record PortsResponse(AslmApiResponseDto AslmApi, List<RunningModuleDto> RunningModules)`

**Purpose:** GET `/v1/ports` payload — same running-modules structure without installed list.

---

#### `private sealed record AslmApiResponseDto(bool Enabled, bool? Running, int? Port, string? BaseUrl)`

**Purpose:** ASLM API mirror server state block. `Running` and `Port` are `null` when `Enabled` is `false`.

---

#### `private sealed record InstalledModuleDto(...)`

**Purpose:** Per installed instance: id, name, version, flags, instance folder name.

---

#### `private sealed record RunningModuleDto(string Id, string Name, string InstanceFolder, string SourcePath, string? PageUrl, List<ModuleHostDto> Hosts)`

**Purpose:** Per running instance with loopback page URL and all assigned host entries.

---

#### `private sealed record ModuleHostDto(string HostKey, string RouteKey, int Port, string TargetUrl, string? MirrorUrl)`

**Purpose:** One port-map entry for a running module. `RouteKey` is the URL segment used by the ASLM API mirror (e.g. `ui-port` → `ui`). `MirrorUrl` is `null` when the ASLM API server is disabled.

---

#### `private sealed record StartModulesRequest(string? CallerModuleId, List<string>? ModuleIds)`

**Purpose:** POST `/v1/modules/start` body.

---

#### `private sealed record StartModuleItemResult(string ModuleId, string Status, string? Message)`

**Purpose:** One module launch result.

---

#### `private sealed record StartModulesResponse(List<StartModuleItemResult> Results)`

**Purpose:** POST response wrapper.

---

## HTTP API reference

### GET `/v1/registry`

Returns installed and running module snapshots. Also includes ASLM API state and port/host data for every running module.

**Response shape:**

```json
{
  "interopBaseUrl": "http://127.0.0.1:20001/",
  "aslmApi": {
    "enabled": true,
    "running": true,
    "port": 20000,
    "baseUrl": "http://127.0.0.1:20000/"
  },
  "installedModules": [ ... ],
  "runningModules": [
    {
      "id": "aslm-chat",
      "name": "ASLM Chat",
      "instanceFolder": "ASLM-Chat",
      "sourcePath": "...",
      "pageUrl": "http://127.0.0.1:20002/",
      "hosts": [
        {
          "hostKey": "ui-port",
          "routeKey": "ui",
          "port": 20002,
          "targetUrl": "http://127.0.0.1:20002/",
          "mirrorUrl": "http://127.0.0.1:20000/aslm-chat/ui/"
        }
      ]
    }
  ]
}
```

### GET `/v1/ports`

Lighter-weight endpoint for port and host discovery only — omits `installedModules`.

**Response shape:**

```json
{
  "aslmApi": { "enabled": true, "running": true, "port": 20000, "baseUrl": "http://127.0.0.1:20000/" },
  "runningModules": [ ... ]
}
```

**Rules:**
- Only modules with live tracked processes appear in `runningModules`.
- `hosts` are built from `PortRegistry` without allocating new entries (read-only).
- `mirrorUrl` is `null` when the ASLM API server is disabled (`aslmApi.enabled: false`).
- `pageUrl` resolves the primary WebView port key for the module.

---

## Related

- [ModuleLaunchCoordinator](ModuleLaunchCoordinator/)
- [ModuleInteropHostState](ModuleInteropHostState/)
- [ModuleInteropPortsBuilder](ModuleInteropPortsBuilder/)
- [AslmApiServer](AslmApiServer/)
- [PortRegistry](PortRegistry/)
