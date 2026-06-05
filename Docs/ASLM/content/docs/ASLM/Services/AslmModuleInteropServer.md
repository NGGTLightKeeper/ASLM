---
title: "AslmModuleInteropServer"
draft: false
---

## Class `AslmModuleInteropServer`

`ASLM/Services/AslmModuleInteropServer.cs` — **`public sealed`** — local JSON **`HttpListener`** API for module registry and coordinated starts.

**DI:** singleton — [AppDataStore](AppDataStore/), [PortRegistry](PortRegistry/), [ModuleInstaller](ModuleInstaller/), [ModuleRunner](ModuleRunner/), [ModuleLaunchCoordinator](ModuleLaunchCoordinator/), [ModuleInteropHostState](ModuleInteropHostState/).

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

#### `public AslmModuleInteropServer(AppDataStore appData, PortRegistry ports, ModuleInstaller moduleInstaller, ModuleRunner moduleRunner, ModuleLaunchCoordinator launchCoordinator, ModuleInteropHostState interopHostState, ILogger<AslmModuleInteropServer> logger)`

**Purpose:** Stores dependencies for registry and launch coordination.

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
| POST | `/v1/modules/start` | **`HandleModulesStartPostAsync`** |

Unknown routes → **404** JSON error. Unhandled exceptions → **500**.

---

## Private methods — registry

#### `private async Task HandleRegistryGetAsync(HttpListenerContext context)`

**Purpose:** Builds **`RegistryResponse`**: interop base URL, installed modules (**`BuildInstalledModulesAsync`**), running snapshots from **`ModuleRunner.GetRunningModulesSnapshot()`**. Returns **200** JSON.

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

#### `private sealed record RegistryResponse(string InteropBaseUrl, List<InstalledModuleDto> InstalledModules, List<RunningModuleDto> RunningModules)`

**Purpose:** GET `/v1/registry` payload.

---

#### `private sealed record InstalledModuleDto(...)`

**Purpose:** Per installed instance: id, name, version, flags, instance folder name.

---

#### `private sealed record RunningModuleDto(string Id, string Name, string InstanceFolder, string SourcePath)`

**Purpose:** Per running instance in registry.

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

## Related

- [ModuleLaunchCoordinator](ModuleLaunchCoordinator/)
- [ModuleInteropHostState](ModuleInteropHostState/)
- [AslmApiServer](AslmApiServer/)
- [PortRegistry](PortRegistry/)
