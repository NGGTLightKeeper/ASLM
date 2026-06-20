---
title: "ModuleRunner"
draft: false
---

## Class `ModuleRunner`

`ASLM/Services/ModuleRunner.cs` — **`public`** — executes module setup, runtime, and settings commands; tracks processes and console output.

**DI:** [EngineInstaller](EngineInstaller/), [ModuleEnvironmentResolver](ModuleEnvironmentResolver/), [PortRegistry](PortRegistry/), [ProcessTracker](ProcessTracker/), [ModuleConsoleStore](ModuleConsoleStore/), [ProcessSnapshotReader](ProcessSnapshotReader/), [ModuleThemePayloadBuilder](ModuleThemePayloadBuilder/), [ModuleLocalePayloadBuilder](ModuleLocalePayloadBuilder/), [ModuleInteropHostState](ModuleInteropHostState/).

Implements **`IDisposable`**. Subscribes to **`PortRegistry.PortsRedistributed`**.

---

## Public methods — construction and setup

#### `public ModuleRunner(EngineInstaller engineInstaller, ModuleEnvironmentResolver environmentResolver, PortRegistry ports, ProcessTracker processTracker, ModuleConsoleStore consoleStore, ProcessSnapshotReader processSnapshots, ModuleThemePayloadBuilder themePayloadBuilder, ModuleLocalePayloadBuilder localePayloadBuilder, ModuleInteropHostState interopHostState, IServiceProvider serviceProvider, ILogger<ModuleRunner> logger)`

**Purpose:** Creates the runner, stores optional services resolver, and wires port redistribution restarts.

---

#### `public async Task<bool> ExecuteFirstRunAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct, bool skipModuleDependencies = false)`

**Purpose:** Installs engine dependencies, synchronizes declared settings, runs **`Commands.FirstRun`** (non-tracked). Sets **`Installed`** and **`FirstRunCompleted`** on success.

---

#### `public async Task<bool> ExecuteRunAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct)`

**Purpose:** Allocates and ensures ports are available, synchronizes settings, and starts each **`Commands.Run`** entry in background with **`trackProcess: true`**.

---

## Public methods — lifecycle and settings

#### `public async Task StopModuleAsync(string moduleSourcePath)`

**Purpose:** Kills all tracked processes for one module instance (**`SourcePath`** key); updates console overview.

---

#### `public IReadOnlyList<string> GetRunningModuleSourcePaths()`

**Purpose:** Source paths with at least one live tracked process.

---

#### `public IReadOnlyList<ModuleConfig> GetRunningModuleConfigs()`

**Purpose:** Returns the module configurations for instances that currently have tracked live processes. The returned configs are the same snapshots stored at process launch time.

---

#### `public IReadOnlyList<RunningModuleSnapshot> GetRunningModulesSnapshot()`

**Purpose:** Id, name, and source path for modules with live processes.

---

#### `public async Task StopAllModulesAsync()`

**Purpose:** Stops and clears every tracked module process tree.

---

#### `public object? GetResolvedSettingValue(ModuleConfig module, ModuleSetting setting)`

**Purpose:** Effective value via **`ResolveSettingValue`** + **`ParseSerializedValue`**.

---

#### `public async Task<bool> RunCommandAsync(ModuleConfig module, ModuleCommand cmd, IProgress<string> log, CancellationToken ct, bool trackProcess = false, bool injectSettings = true, string sessionStage = "Command")`

**Purpose:** Resolves **`{setting}`** placeholders and optional engine; injects env; streams to console store; optional process tracking and descendant monitoring.

---

#### `public async Task<string?> ExecuteSettingCommandAsync(ModuleConfig module, ModuleSetting setting, bool isSet, string? newValue, CancellationToken ct)`

**Purpose:** Runs **`GetExec`** / **`SetExec`** (throttled, max 4 concurrent). Host **`theme`** / **`locale`** payloads use temp JSON files. Returns last non-empty stdout line.

---

#### `public void Dispose()`

**Purpose:** Unsubscribes port events; kills and disposes all tracked processes.

---

## Private methods — settings sync and resolution

#### `private async Task SynchronizeDeclaredModuleSettingsAsync(ModuleConfig module, IProgress<string> moduleLog, CancellationToken ct)`

**Purpose:** For settings with **`SetExec`**: optional **`GetExec`** compare, then apply via **`ExecuteSettingCommandAsync`**.

---

#### `private void InjectSettingsIntoEnvironment(ModuleConfig module, ProcessStartInfo psi)`

**Purpose:** Sets **`ASLM_{KEY}`** env vars (skips host-managed theme/locale), **`ASLM_MODULE_ID`**, **`ASLM_MODULE_DIR`**, optional interop URL/port.

---

#### `private string ResolveSettingValue(ModuleConfig module, ModuleSetting setting)`

**Purpose:** Resolves by type: **`port`**, **`engine`**, **`path`**, **`data`**, **`models`**, **`bool`**, **`theme`**, **`locale`**, or string default.

---

## Private methods — port redistribution

#### `private void OnPortsRedistributed(object? sender, EventArgs e)`

**Purpose:** Schedules **`RestartRunningModulesAfterPortRedistributionAsync`**.

---

#### `private async Task RestartRunningModulesAfterPortRedistributionAsync()`

**Purpose:** Stop + delay + **`ExecuteRunAsync`** for each running module with run commands.

---

#### `private List<ModuleConfig> GetRunningModuleSnapshots()`

**Purpose:** Tracked modules with live processes and non-empty run commands.

---

## Private methods — dependencies and commands

#### `private async Task<bool> InstallDependenciesAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct)`

**Purpose:** Per **`Dependencies.Engines`**: package manager install of declared libraries.

---

## Private methods — host-managed settings helpers

#### `private static bool IsHostManagedSetting(string normalizedType)`

**Purpose:** **`theme`** or **`locale`**.

---

#### `private static bool UsesHostFilePayload(string normalizedType)`

**Purpose:** Same as host-managed (temp JSON file for **`{value}`**).

---

#### `private static string GetHostPayloadFilePrefix(string normalizedType)`

**Purpose:** `aslm_locale` or `aslm_theme`.

---

## Private methods — console and streaming

#### `private IProgress<string> CreateModuleLog(ModuleConfig module, IProgress<string> log)`

**Purpose:** Forwards to caller log and **`ModuleConsoleStore.AppendOverviewLine`**.

---

#### `private static void ConfigureProcessForStreaming(ProcessStartInfo psi, string fileName, string? engineId)`

**Purpose:** Python **`-u`** and unbuffered env when applicable.

---

#### `private static bool IsPythonProcess(string fileName, string? engineId)`

**Purpose:** Detects Python engine or executable name.

---

#### `private static bool HasPythonUnbufferedFlag(string arguments)`

**Purpose:** True when **`-u`** already present.

---

## Private methods — observed subprocesses

#### `private async Task MonitorObservedProcessesAsync(ModuleConfig module, ModuleConsoleSessionHandle ownerHandle, int rootProcessId, CancellationToken ct)`

**Purpose:** Polls descendant PIDs every 1 s; syncs to console store until root exits.

---

#### `private List<ObservedProcessInfo> GetDescendantProcesses(int rootProcessId)`

**Purpose:** BFS over cached process snapshot children.

---

#### `private static bool IsProcessAlive(int processId)`

**Purpose:** **`Process.GetProcessById`** without exited.

---

#### `private static string ResolveObservedProcessName(ProcessSnapshotEntry entry)`

**Purpose:** Live **`ProcessName`** or snapshot executable basename.

---

## Private methods — shutdown and tracking

#### `private async Task KillProcessSafeAsync(Process process)`

**Purpose:** **`Kill(entireProcessTree: true)`** with 5 s wait; disposes process.

---

#### `private void RemoveProcess(string moduleSourcePath, Process process)`

**Purpose:** Removes from **`_runningProcesses`** / **`_runningModules`** when list empty.

---

## Private methods — command parsing

#### `private static List<string> SplitCommand(string command)`

**Purpose:** Quote-aware tokenizer.

---

#### `private static string ParseArguments(IEnumerable<string> args)`

**Purpose:** Joins args with quoting for spaces.

---

#### `private static string QuoteWindowsArgument(string argument)`

**Purpose:** Escaped double-quoted argument for CreateProcess.

---

#### `private static string SanitizeFileNameSegment(string moduleId)`

**Purpose:** Safe temp-file fragment from module id.

---

#### `private static string TrimEngineSettingSuffix(string settingKey)`

**Purpose:** Strips **`_path`**, **`_data`**, **`_models`** suffixes.

---

#### `private static string GetRootDirectory()`

**Purpose:** Parent of app base directory.

---

## Related

- [ModuleInstaller](ModuleInstaller/)
- [ModuleLaunchCoordinator](ModuleLaunchCoordinator/)
- [SettingsService](SettingsService/)
- [ModuleConsoleStore](ModuleConsoleStore/)
