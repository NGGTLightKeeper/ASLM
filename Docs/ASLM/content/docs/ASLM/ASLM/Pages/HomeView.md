---
title: "HomeView"
draft: false
---

## Class `HomeView`

`ASLM/Pages/HomeView.xaml` + `HomeView.xaml.cs` — **`public partial`** **`ContentView`** — main dashboard: host metrics (summary cards), managed process tree, and per-module quick actions. UI in XAML; presenter, diagnostics, and view models live in the same code-behind file.

Implements **`IHomeDashboardView`** (render contract) and **`ILocalizable`**.

Related types in the same file: `HomePaletteColors`, `HomeDashboardPresenter`, `HomeDiagnosticsCollector`, `HomeDashboardPageViewModel`, and supporting snapshot/view-model types.

---

### Constants

| Name | Value | Description |
| --- | --- | --- |
| `MinSummaryCardWidth` | `400` | Minimum width (px) used to compute summary-card grid span |
| `CompactBreakpoint` | `1480` | Below this width, `WorkspaceLayout` stacks process tree above modules |

Refresh timer interval: **1 second** while the view is loaded (`CreateRefreshTimer`).

---

### XAML elements

| Name | Type / role |
| --- | --- |
| `Root` | `x:Name` on root `ContentView`; `SummaryGridSpan` binding source |
| `HomeHeaderTitleLabel` | Style key for page title (`FontSize` 24, bold) |
| `HomeTitleLabel` | Page title (`LocalizationKeys.Home_Title`) |
| *(row 1)* | `CollectionView` bound to `{Binding SummaryCards}`; `GridItemsLayout` span from `Root.SummaryGridSpan` |
| `WorkspaceLayout` | `Grid` for process tree + modules (responsive columns/rows) |
| `ProcessTreePanel` | `Border` around managed process tree |
| `ManagedProcessesHeaderLabel` | Section title |
| `ColumnNameLabel` … `ColumnDetailsLabel` | Tree column headers (Name, CPU, GPU, RAM, Disk, Net, Details) |
| *(process tree)* | `CollectionView` bound to `{Binding ProcessNodes}` (no `x:Name`) |
| `ModulesPanel` | `Border` around module list |
| `ModulesHeaderLabel` | Section title |
| `ModulesCollection` | `CollectionView` bound to `{Binding Modules}` |

`BindingContext`: **`HomeDashboardPageViewModel`**.

Data templates bind summary metrics (`Name`, `ValueText`, `DetailText`, `ProgressValue`, `HasProgress`, `AccentColor`), tree rows (`Title`, `CpuText`, `ToggleCommand`, `IndentMargin`, …), and module rows (`LaunchCommand`, `StopCommand`, `IsBusy`, …).

---

## `HomePaletteColors`

`internal static` — reads semantic colors from `Application.Current.Resources` so presenter snapshots stay readable in light and dark themes.

### Properties

| Member | Description |
| --- | --- |
| `LabelPrimary` | `{DynamicResource LabelPrimary}` or `#FF000000` |
| `LabelSecondary` | `{DynamicResource LabelSecondary}` or `#FF636366` |

#### `private static Color Resolve(string key, Color fallback)`

**Purpose:** Returns the resource color when `Application.Current.Resources` contains `key` as a `Color`; otherwise `fallback`.

---

## `HomeView` methods

#### `public HomeView(ModuleInstaller moduleInstaller, ModuleRunner moduleRunner, ModuleLaunchCoordinator launchCoordinator, ModuleConsoleStore consoleStore, ProcessSnapshotReader processSnapshots, AppDataStore appData, AppLocalizationService localization)`

**Purpose:** `InitializeComponent()`, sets `BindingContext` to `_viewModel`, constructs **`HomeDashboardPresenter`**, hooks **`LocalizableAttach`**, subscribes **`Loaded`** / **`Unloaded`** / **`SizeChanged`**, calls **`UpdateResponsiveLayout()`**.

---

#### `public void ApplyLocalization()`

**Purpose:** Localizes all named header/column labels via **`L.Get`**, then `_presenter.RefreshAsync(forceModuleReload: false)`.

---

#### `internal void Initialize(AppShellPage shell)`

**Purpose:** Forwards to `_presenter.AttachShell(shell)` for shell navigation.

---

#### `internal Task RefreshAsync()`

**Purpose:** Forwards to `_presenter.RefreshAsync()`.

---

#### `private async void OnLoaded(object? sender, EventArgs e)`

**Purpose:** `await _presenter.ActivateAsync()` then **`StartRefreshTimer()`**.

---

#### `private void OnUnloaded(object? sender, EventArgs e)`

**Purpose:** **`StopRefreshTimer()`**, then `_presenter.Deactivate()`.

---

#### `private void OnSizeChanged(object? sender, EventArgs e)`

**Purpose:** Calls **`UpdateResponsiveLayout()`**.

---

#### `void IHomeDashboardView.Render(HomeDashboardState state)`

**Purpose:** **`IHomeDashboardView`** explicit implementation. **`SyncKeyedItems`** on `SummaryCards`, `ProcessNodes` (with `removeMissingItemsBeforeReorder: true`), and `Modules` using each item’s **`CopyFrom`**.

---

#### `private void UpdateResponsiveLayout()`

**Purpose:** Computes `SummaryGridSpan` from `Width` and `MinSummaryCardWidth`. When width &lt; `CompactBreakpoint`, stacks `ProcessTreePanel` and `ModulesPanel` in two rows; otherwise places them side-by-side (1.35 : 1 star columns).

---

#### `private void StartRefreshTimer()`

**Purpose:** Creates the dispatcher timer via **`CreateRefreshTimer`** when needed and starts it if not running.

---

#### `private void StopRefreshTimer()`

**Purpose:** Stops `_refreshTimer` when present.

---

#### `private IDispatcherTimer CreateRefreshTimer(IDispatcher dispatcher)`

**Purpose:** Builds a repeating **1 s** `IDispatcherTimer` wired to **`OnRefreshTimerTick`**.

---

#### `private async void OnRefreshTimerTick(object? sender, EventArgs e)`

**Purpose:** `await _presenter.RefreshAsync(forceModuleReload: false)` on each tick.

---

#### `private static void SyncKeyedItems<T>(ObservableCollection<T> target, IReadOnlyList<T> source, Func<T, string> keySelector, Action<T, T> updateExisting, bool removeMissingItemsBeforeReorder = false) where T : class`

**Purpose:** In-place collection sync by key: optional **`RemoveMissingItems`**, then update/move/insert so `CollectionView` row instances stay stable between refreshes; trims trailing extras.

---

#### `private static void RemoveMissingItems<T>(ObservableCollection<T> target, IReadOnlyList<T> source, Func<T, string> keySelector) where T : class`

**Purpose:** Removes target items whose keys are absent from `source` (before tree reorder).

---

#### `private static Dictionary<string, int> BuildIndex<T>(ObservableCollection<T> target, Func<T, string> keySelector) where T : class`

**Purpose:** Builds key → index map for the current `target` collection.

---

## `IHomeDashboardView`

`internal interface` — rendering contract for **`HomeDashboardPresenter`**.

#### `void Render(HomeDashboardState state)`

**Purpose:** Applies a fully prepared dashboard state to the view.

---

## `HomeDashboardPresenter`

`internal sealed` — builds **`HomeDashboardState`**, coordinates refresh ( **`SemaphoreSlim`** ), module launch/stop/restart, tree expand/collapse, and navigation via **`AppShellPage`**.

### Fields (behavioral)

| Field | Role |
| --- | --- |
| `_refreshLock` | Coalesces overlapping refreshes (`WaitAsync(0)`) |
| `_forceRefreshQueued` | Queues a module rediscover on the next refresh pass |
| `_expandedNodes` | Persisted tree expansion (`root:aslm` defaults expanded) |
| `_moduleOperations` | Per–`sourcePath` **`HomeModuleOperationState`** |
| `_diagnosticsCollector` | **`HomeDiagnosticsCollector`** (500 ms CPU sample window via **`ProcessSnapshotReader`**) |

---

#### `public HomeDashboardPresenter(IHomeDashboardView view, ModuleInstaller moduleInstaller, ModuleRunner moduleRunner, ModuleLaunchCoordinator launchCoordinator, ModuleConsoleStore consoleStore, ProcessSnapshotReader processSnapshots, AppDataStore appData)`

**Purpose:** Stores dependencies and constructs **`HomeDiagnosticsCollector`**.

---

#### `public void AttachShell(AppShellPage shell)`

**Purpose:** Sets `_shell` for navigation.

---

#### `public async Task ActivateAsync()`

**Purpose:** Idempotent: subscribes **`ModuleConsoleStore.StateChanged`**, sets `_isActive`, `await RefreshAsync(forceModuleReload: true)`.

---

#### `public void Deactivate()`

**Purpose:** Unsubscribes console events and clears `_isActive`.

---

#### `public Task RefreshAsync()`

**Purpose:** Delegates to **`RefreshAsync(forceModuleReload: true)`**.

---

#### `public async Task RefreshAsync(bool forceModuleReload)`

**Purpose:** Acquires `_refreshLock` (no-op if busy). Optionally queues module reload via **`Interlocked`**. Loop: discover modules when needed, background **`HomeDiagnosticsCollector.Capture`**, **`BuildState`**, **`MainThread.InvokeOnMainThreadAsync`** → **`_view.Render`**. Re-runs while `_forceRefreshQueued` is set. Always releases the lock in `finally`.

---

#### `public void OpenModules()`

**Purpose:** `MainThread.BeginInvokeOnMainThread(() => _shell?.OpenModules())`.

---

#### `public void OpenConsoles()`

**Purpose:** `MainThread.BeginInvokeOnMainThread(() => _shell?.OpenConsoles())`.

---

#### `private void OnConsoleStateChanged(object? sender, EventArgs e)`

**Purpose:** No-op; periodic 1 s refresh already updates the dashboard.

---

#### `private HomeDashboardState BuildState(IReadOnlyList<ModuleConfig> modules, IReadOnlyList<ModuleConsoleModuleSnapshot> consoleSnapshots, HomeDiagnosticsSnapshot diagnostics)`

**Purpose:** Assembles **`SummaryCards`**, **`FlattenTree(diagnostics.RootNode)`**, and **`BuildModuleCards`**.

---

#### `private IReadOnlyList<HomeSummaryCardViewModel> BuildSummaryCards(...)`

**Purpose:** Builds three cards (`system`, `runtime`, `modules`) with CPU/GPU/RAM/disk/network metrics, localized footers, and session/process counts from diagnostics and console snapshots.

---

#### `private IReadOnlyList<HomeModuleCardViewModel> BuildModuleCards(IReadOnlyList<ModuleConfig> modules, HomeDiagnosticsSnapshot diagnostics)`

**Purpose:** One row per module: status text, launch/stop/restart/console commands, busy flags from **`GetOperationState`**, console button gated by **`AppDataStore`** sidebar visibility.

---

#### `private IReadOnlyList<HomeProcessTreeNodeViewModel> FlattenTree(HomeProcessTreeNodeSnapshot root)`

**Purpose:** Depth-first visible list via **`AppendVisibleNode`**.

---

#### `private void AppendVisibleNode(HomeProcessTreeNodeSnapshot node, int depth, List<HomeProcessTreeNodeViewModel> visibleNodes)`

**Purpose:** Adds a **`HomeProcessTreeNodeViewModel`** (indent, expander icon, **`ToggleCommand`**) and recurses when expanded.

---

#### `private bool GetExpandedState(string nodeKey, bool defaultExpanded)`

**Purpose:** Reads `_expandedNodes` or falls back to `defaultExpanded`.

---

#### `private static bool IsFixedRootNode(string nodeKey)`

**Purpose:** `true` for `root:aslm` (always expanded, no toggle).

---

#### `private void ToggleNodeExpansion(string nodeKey)`

**Purpose:** Flips expansion (except fixed root), then **`RenderCachedState()`**.

---

#### `private void RenderCachedState()`

**Purpose:** Rebuilds state from `_knownModules`, `_lastConsoleSnapshots`, `_lastDiagnosticsSnapshot` without re-sampling.

---

#### `private HomeModuleOperationState GetOperationState(string sourcePath)`

**Purpose:** Gets or creates per-module operation state in `_moduleOperations`.

---

#### `private async Task LaunchModuleAsync(string sourcePath)`

**Purpose:** Guards on busy; **`StartLaunching`**, refresh, **`ModuleLaunchCoordinator.LaunchOrEnsureRunningBySourcePathAsync`**, **`ReloadModuleAsync`**, clear + `RefreshAsync(forceModuleReload: true)`.

---

#### `private async Task StopModuleAsync(string sourcePath)`

**Purpose:** **`StartStopping`**, **`ModuleRunner.StopModuleAsync`**, disable module, **`SaveConfigAsync`**, **`ModuleConsoleStore.UpdateModuleEnabledState`**, clear + forced refresh.

---

#### `private async Task RestartModuleAsync(string sourcePath)`

**Purpose:** Stop, 1 s delay, launch via coordinator, reload, clear + forced refresh.

---

#### `private async Task<ModuleConfig?> ReloadModuleAsync(string sourcePath)`

**Purpose:** **`LoadModuleConfig`** and patch `_knownModules` list entry.

---

#### `private void OpenModuleConsole(string sourcePath)`

**Purpose:** `_shell?.OpenConsoles(sourcePath)` on main thread.

---

#### `private static HomeSummaryMetricViewModel CreatePercentMetric(string name, double percentValue, string detailText)`

**Purpose:** Percent row with progress bar and **`GetSeverityColor`** accent.

---

#### `private static HomeSummaryMetricViewModel CreateOptionalPercentMetric(string name, double? percentValue, string detailText)`

**Purpose:** **`CreatePercentMetric`** or localized “not available” placeholder.

---

#### `private static HomeSummaryMetricViewModel CreateInformationalMetric(string name, string valueText, string detailText, Color accentColor)`

**Purpose:** Metric row without progress bar.

---

#### `private static IReadOnlyList<HomeSummaryMetricViewModel> BuildGpuMetrics(IReadOnlyList<HomeGpuAdapterSnapshot> adapters, bool useRuntimeValues)`

**Purpose:** One metric per adapter (`SystemPercent` vs `RuntimePercent`) or single “unavailable” row.

---

#### `internal static IReadOnlyList<HomeMetricBadgeViewModel> BuildUsageBadges(HomeUsageSnapshot usage, int processCount, int sessionCount)`

**Purpose:** CPU/GPU/RAM/disk/net badges plus optional process/session badges.

---

#### `private static HomeMetricBadgeViewModel CreateBadge(string text, Color backgroundColor)`

**Purpose:** Constructs one badge VM.

---

#### `private static string BuildModuleSecondaryText(ModuleConfig module)`

**Purpose:** Description, or author, or default “installed” localized string.

---

#### `internal static string FormatBytes(long bytes)`

**Purpose:** Compact binary scale (`B` … `TB`).

---

#### `internal static string FormatRate(double bytesPerSecond)`

**Purpose:** Compact throughput scale (`B/s` … `TB/s`).

---

#### `private static string FormatConnectionCount(int connectionCount)`

**Purpose:** Localized singular/plural connection label.

---

#### `internal static string FormatPercent(double value)`

**Purpose:** `"{value:F1}%"`.

---

#### `internal static string FormatPercentOrNa(double? value)`

**Purpose:** Percent string or `"n/a"`.

---

#### `internal static string FormatCompactCount(int count)`

**Purpose:** Invariant integer string for narrow columns.

---

#### `private static Color GetSeverityColor(double normalizedValue)`

**Purpose:** Green / yellow / red thresholds at 0.5 and 0.8.

---

## `HomeDashboardState`

`internal sealed` — full render payload for **`IHomeDashboardView.Render`**.

### Properties

| Member | Description |
| --- | --- |
| `SummaryCards` | Top metric cards |
| `ProcessNodes` | Flattened visible tree rows |
| `Modules` | Module quick-control rows |

---

## `HomeRefreshSnapshot`

`internal sealed` — background refresh result before UI-thread render.

### Properties

| Member | Description |
| --- | --- |
| `ConsoleSnapshots` | Latest **`ModuleConsoleStore`** snapshot |
| `Diagnostics` | Latest **`HomeDiagnosticsSnapshot`** |
| `State` | Render-ready **`HomeDashboardState`** |

---

## `HomeDashboardPageViewModel`

`public sealed` — **`INotifyPropertyChanged`** page binding context.

### Properties

| Member | Description |
| --- | --- |
| `SummaryCards` | `ObservableCollection<HomeSummaryCardViewModel>` |
| `ProcessNodes` | `ObservableCollection<HomeProcessTreeNodeViewModel>` |
| `Modules` | `ObservableCollection<HomeModuleCardViewModel>` |
| `SummaryGridSpan` | Column span for summary `GridItemsLayout` (default `1`) |

---

#### `public HomeDashboardPageViewModel()`

**Purpose:** Calls **`SeedSummaryCards()`** for placeholder loading cards.

---

#### `private void SeedSummaryCards()`

**Purpose:** Adds three **`CreateLoadingCard`** placeholders (`system`, `runtime`, `modules`).

---

#### `private static HomeSummaryCardViewModel CreateLoadingCard(string key, string title, string subtitle, string statusText)`

**Purpose:** Placeholder card with `"..."` CPU/RAM/disk metrics.

---

#### `private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)`

**Purpose:** Raises **`PropertyChanged`** when `SummaryGridSpan` (or other VM fields) change.

---

## `HomeBindableItemViewModel`

`public abstract` — shared **`INotifyPropertyChanged`** base for keyed dashboard rows.

#### `protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)`

**Purpose:** Equality-checked field update + **`PropertyChanged`**.

---

## `HomeSummaryCardViewModel`

`public sealed` — one summary card (`Key`, `Title`, `Subtitle`, `StatusText`, `AccentOverlayColor`, `Metrics`, `FooterText`).

#### `public void CopyFrom(HomeSummaryCardViewModel source)`

**Purpose:** Copies all bindable fields from a presenter-built snapshot instance.

---

## `HomeSummaryMetricViewModel`

`public sealed` — one metric row inside a summary card (properties only: `Name`, `ValueText`, `DetailText`, `HasProgress`, `ProgressValue`, `AccentColor`).

---

## `HomeMetricBadgeViewModel`

`public sealed` — compact badge (`Text`, `BackgroundColor`).

---

## `HomeModuleCardViewModel`

`public sealed` — module row with commands and visibility flags (`IsRunningAndIdle`, `IsStoppedAndIdle`, `IsBusy`, `CanOpenConsole`, …).

#### `public void CopyFrom(HomeModuleCardViewModel source)`

**Purpose:** Copies identity, text, visibility, and command references from the latest snapshot.

---

## `HomeProcessTreeNodeViewModel`

`public sealed` — one visible process-tree row (columns, indent, expander, **`ToggleCommand`**).

#### `public void CopyFrom(HomeProcessTreeNodeViewModel source)`

**Purpose:** Copies all row fields from the latest snapshot.

---

## `HomeModuleOperationState`

`internal sealed` — busy state for launch/stop/restart from the home dashboard.

### Properties

| Member | Description |
| --- | --- |
| `IsBusy` | Whether an action is in progress |
| `BusyText` | Localized busy label |

---

#### `public void StartLaunching()`

**Purpose:** Sets busy + `Home_Module_Launching`.

---

#### `public void StartStopping()`

**Purpose:** Sets busy + `Home_Module_Stopping`.

---

#### `public void StartRestarting()`

**Purpose:** Sets busy + `Modules_Restarting`.

---

#### `public void Clear()`

**Purpose:** Clears busy state and text.

---

## `HomeDiagnosticsCollector`

`internal sealed` — captures system/runtime/module diagnostics: CPU (kernel + per-process deltas), RAM, disk (WMI), network interfaces, GPU (WMI), TCP/UDP endpoint counts (P/Invoke), and process tree snapshots.

### Cache intervals

| Sample | TTL |
| --- | --- |
| Disk WMI | 3000 ms |
| GPU WMI | 3000 ms |
| Connection table | 2000 ms (invalidated when managed PID set changes) |

---

#### `public HomeDiagnosticsCollector(ProcessSnapshotReader processSnapshots)`

**Purpose:** Stores **`ProcessSnapshotReader`**.

---

#### `public HomeDiagnosticsSnapshot Capture(IReadOnlyList<ModuleConfig> modules, IReadOnlyList<ModuleConsoleModuleSnapshot> consoleSnapshots)`

**Purpose:** Full capture: managed PID tree, memory, CPU, disk, network, GPU, per-process samples, **`BuildModuleDiagnostics`**, internal/unassigned processes, **`BuildRootNode`**, returns **`HomeDiagnosticsSnapshot`**.

---

#### `private HomeDiskStats GetDiskStatsSample(DateTimeOffset capturedUtc)`

**Purpose:** Returns cached **`QueryDiskStats`** when younger than 3 s.

---

#### `private HomeGpuQueryResult GetGpuUsageSample(DateTimeOffset capturedUtc)`

**Purpose:** Returns cached **`QueryGpuUsage`** when younger than 3 s.

---

#### `private IReadOnlyDictionary<int, int> GetConnectionCountsSample(DateTimeOffset capturedUtc, IReadOnlyCollection<int> processIds)`

**Purpose:** Returns cached **`QueryProcessConnectionCounts`** when younger than 2 s and PID set unchanged.

---

#### `private static Dictionary<string, HomeModuleDiagnostics> BuildModuleDiagnostics(...)`

**Purpose:** Per-module process assignment from console sessions, tracked roots, and descendant branches; global de-duplication of PIDs.

---

#### `private static void CollectProcessBranch(int rootProcessId, IReadOnlyDictionary<int, List<int>> childrenByParent, IReadOnlyDictionary<int, HomeLiveProcessInfo> liveProcesses, ISet<int> results)`

**Purpose:** Recursive descendant collection into `results`.

---

#### `private static HomeProcessTreeNodeSnapshot BuildRootNode(...)`

**Purpose:** `root:aslm` node with module children + optional “ASLM Internal” node for unassigned PIDs.

---

#### `private static HomeProcessTreeNodeSnapshot BuildModuleNode(...)`

**Purpose:** Module aggregate row + root process child nodes.

---

#### `private static HomeProcessTreeNodeSnapshot BuildProcessNode(...)`

**Purpose:** Single process row with children, session-aware title/details (observed service styling).

---

#### `private static string FormatProcessCount(int processCount)`

**Purpose:** `"1 process"` / `"{n} processes"`.

---

#### `private bool TrySampleProcess(int processId, DateTimeOffset capturedUtc, IReadOnlyDictionary<int, ProcessSnapshotEntry> entriesByPid, IReadOnlyDictionary<int, double> gpuByPid, IReadOnlyDictionary<int, int> connectionCounts, long totalPhysicalBytes, out HomeLiveProcessInfo liveProcess)`

**Purpose:** `Process.GetProcessById`, CPU/IO deltas from `_processSamples`, GPU and connection counts; returns `false` on exit/failure.

---

#### `private void CleanupProcessSamples(IEnumerable<int> liveProcessIds)`

**Purpose:** Removes stale entries from `_processSamples`.

---

#### `private static HomeUsageSnapshot AggregateUsage(IEnumerable<HomeUsageSnapshot> usages, long totalPhysicalBytes)`

**Purpose:** Sums CPU, GPU (optional), memory, disk IO, connections; clamps CPU/GPU to 0–100.

---

#### `private double SampleSystemCpu(DateTimeOffset capturedUtc)`

**Purpose:** **`GetSystemTimes`** delta vs `_lastSystemCpuSample`.

---

#### `private HomeNetworkStats SampleNetwork(DateTimeOffset capturedUtc)`

**Purpose:** Sums active non-loopback/tunnel adapter byte counters; delta vs `_lastNetworkSample`.

---

#### `private static HomeDiskStats QueryDiskStats()`

**Purpose:** WMI `Win32_PerfFormattedData_PerfDisk_PhysicalDisk` `_Total` (busy %, read/write B/s).

---

#### `private static HomeGpuQueryResult QueryGpuUsage()`

**Purpose:** WMI `Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine`; parses instances via **`TryParseGpuInstance`**.

---

#### `private static IReadOnlyList<HomeGpuAdapterSnapshot> BuildGpuAdapterSnapshots(HomeGpuQueryResult gpuQuery, IReadOnlyCollection<int> managedProcessIds)`

**Purpose:** System vs runtime per-adapter utilization from engine samples.

---

#### `private static Dictionary<int, double> AggregateGpuUsageByAdapter(IEnumerable<HomeGpuEngineSample> samples)`

**Purpose:** Max summed engine utilization per adapter.

---

#### `private static IReadOnlyList<string> QueryGpuAdapterNames()`

**Purpose:** Cached WMI `Win32_VideoController` names.

---

#### `private static Dictionary<int, int> QueryProcessConnectionCounts(IReadOnlyCollection<int> processIds)`

**Purpose:** TCP/UDP IPv4+IPv6 owner tables for tracked PIDs.

---

#### `private static void IncrementCountsFromTcpTable(IDictionary<int, int> counts, ISet<int> trackedProcessIds, AddressFamily addressFamily)`

**Purpose:** **`GetExtendedTcpTable`** (`OwnerPidAll`) two-pass buffer allocation.

---

#### `private static void IncrementCountsFromUdpTable(IDictionary<int, int> counts, ISet<int> trackedProcessIds, AddressFamily addressFamily)`

**Purpose:** **`GetExtendedUdpTable`** (`OwnerPid`) two-pass buffer allocation.

---

#### `private static HashSet<int> CollectDescendantPids(int rootProcessId, IReadOnlyDictionary<int, List<int>> childrenByParent)`

**Purpose:** BFS descendant PIDs under ASLM.

---

#### `private static HomeMemoryStatus GetMemoryStatus()`

**Purpose:** **`GlobalMemoryStatusEx`** total/used physical bytes.

---

#### `private static IoCounters GetProcessIoCountersSafe(Process process)`

**Purpose:** **`GetProcessIoCounters`** or default.

---

#### `private static string TryResolveProcessName(Process process, string? executableName)`

**Purpose:** `ProcessName` or snapshot executable base name or `Process {id}`.

---

#### `private static bool TryParseGpuInstance(string? instanceName, out int processId, out int adapterIndex, out string engineId)`

**Purpose:** Regex `pid_(\d+).*?phys_(\d+).*?eng_(\d+)`.

---

#### `private static double ConvertToDouble(object? value)`

**Purpose:** WMI numeric → `double` (invariant parse fallback).

---

#### `private static ulong ToUInt64(FileTime fileTime)`

**Purpose:** Combines high/low `FileTime` dwords.

---

#### `private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime)`

**Purpose:** `kernel32.dll` — system CPU ticks.

---

#### `private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx memoryStatus)`

**Purpose:** `kernel32.dll` — physical memory status.

---

#### `private static extern bool GetProcessIoCounters(IntPtr hProcess, out IoCounters ioCounters)`

**Purpose:** `kernel32.dll` — per-process I/O counters.

---

#### `private static extern uint GetExtendedTcpTable(...)`

**Purpose:** `iphlpapi.dll` — TCP owner PID table.

---

#### `private static extern uint GetExtendedUdpTable(...)`

**Purpose:** `iphlpapi.dll` — UDP owner PID table.

---

### Nested types (`HomeDiagnosticsCollector`)

| Type | Role |
| --- | --- |
| `TcpTableClass` | `OwnerPidAll = 5` |
| `UdpTableClass` | `OwnerPid = 1` |
| `FileTime` | Win32 FILETIME layout |
| `MemoryStatusEx` | `GlobalMemoryStatusEx` buffer |
| `IoCounters` | Process I/O counter struct |
| `MibTcpRowOwnerPid` / `MibTcp6RowOwnerPid` | TCP row layouts |
| `MibUdpRowOwnerPid` / `MibUdp6RowOwnerPid` | UDP row layouts |

Constants: `ErrorSuccess = 0`, `ErrorInsufficientBuffer = 122`.

---

## Snapshot and helper types (same file)

### `HomeDiagnosticsSnapshot`

| Property | Description |
| --- | --- |
| `CapturedUtc` | Sample timestamp |
| `SystemUsage` / `RuntimeUsage` | **`HomeUsageSnapshot`** |
| `ModulesBySourcePath` | Per-module diagnostics |
| `GpuAdapters` | Adapter utilization list |
| `RootNode` | Tree root before flattening |
| `ActiveProcessCount` / `ActiveModuleCount` | Aggregate counts |
| `LastModuleActivityUtc` | Max console activity time |

### `HomeUsageSnapshot`

Usage fields plus computed **`MemoryPercent`** (`MemoryBytes` / `MemoryTotalBytes`).

### `HomeModuleDiagnostics`

Module, console snapshot, PID sets, **`Usage`**, computed **`LastActivityUtc`**, **`ConsoleSessionCount`**.

### `HomeLiveProcessInfo`

`ProcessId`, `ParentProcessId`, `ProcessName`, **`Usage`**.

### `HomeGpuAdapterSnapshot`

`AdapterIndex`, `Name`, `SystemPercent`, `RuntimePercent`.

### `HomeGpuEngineSample` / `HomeGpuQueryResult`

Raw GPU engine samples and aggregated `ProcessByPid` / `AdapterNames`.

### `HomeProcessTreeNodeSnapshot`

Tree node before flattening (`Key`, column texts, `Children`, `DefaultExpanded`).

### `HomeProcessSample` / `HomeSystemCpuSample` / `HomeNetworkSample`

Delta sampling state for process CPU/IO, system CPU, and network totals.

### `HomeMemoryStatus` / `HomeDiskStats` / `HomeNetworkStats`

Normalized host memory, disk, and network throughput snapshots.

---

## Dependencies

| Service | Use |
| --- | --- |
| [ModuleInstaller](../Services/ModuleInstaller/) | Discover/load/save modules |
| [ModuleRunner](../Services/ModuleRunner/) | Stop modules |
| [ModuleLaunchCoordinator](../Services/ModuleLaunchCoordinator/) | Launch/restart |
| [ModuleConsoleStore](../Services/ModuleConsoleStore/) | Console snapshots, enable state |
| [ProcessSnapshotReader](../Services/ProcessSnapshotReader/) | Process tree entries |
| [AppDataStore](../Services/AppDataStore/) | Console sidebar visibility |
| [AppLocalizationService](../Services/AppLocalizationService/) | **`ILocalizable`** |

Related models: [ModuleConfig](../Models/ModuleConfig/), [RunningModuleSnapshot](../Models/RunningModuleSnapshot/).
