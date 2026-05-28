---
title: "ConsolesView"
draft: false
---

## Class `ConsolesView`

`ASLM/Pages/ConsolesView.xaml` + `ConsolesView.xaml.cs` — three-pane consoles workspace in [AppShellPage](AppShellPage/). Implements **`IConsolesView`** (presenter render target) and **`ILocalizable`**.

`BindingContext`: **`ConsolesPageViewModel`**.

---

### Constants

| Name | Value |
| --- | --- |
| `CompactBreakpoint` | `1180` (stacked layout when `Width` is below) |
| `AutoRefreshInterval` | `3` seconds |

---

### Fields

| Name | Type | Description |
| --- | --- | --- |
| `_viewModel` | `ConsolesPageViewModel` | Page binding state |
| `_presenter` | `ConsolesPresenter` | Dashboard builder |
| `_localization` | `AppLocalizationService` | Culture hook |
| `_suppressSelection` | `bool` | Skips selection handlers during programmatic sync |
| `_layoutRefreshQueued` | `int` | Coalesces delayed layout invalidation |
| `_autoRefreshCts` | `CancellationTokenSource?` | Periodic refresh while loaded |

---

### XAML elements

| Name | Role |
| --- | --- |
| `PageTitleLabel` | `Consoles_Title` |
| `WorkspaceLayout` | Responsive 3-column / stacked grid |
| `ModulesPanel` | Module list border |
| `ModulesHeaderLabel` | `Consoles_ModulesHeader` |
| `ModulesCollection` | `ItemsSource={Binding Modules}`, `OnModuleSelectionChanged` |
| `ModulesEmptyLabel` | `Consoles_NoActiveModules` |
| `SessionsPanel` | Session list border (hidden when only unified console) |
| `SessionsHeaderLabel` | Reuses `Consoles_Title` in localization |
| `SessionsCollection` | `ItemsSource={Binding Sessions}`, `OnSessionSelectionChanged` |
| `SessionsEmptyLabel` | `Consoles_NoConsoles` |
| `OutputPanel` | Output header + native console host |
| `OutputHeaderPanel` | Binds `SelectedSessionTitle` |
| `ConsoleOutputHost` | `ConsoleOutputView` — `Text`, `SessionKey` from view model |

---

## Constructor

#### `ConsolesView(ModuleInstaller, ModuleConsoleStore, AppDataStore, AppLocalizationService)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `InitializeComponent()` |
| 2 | `BindingContext = _viewModel` |
| 3 | 
ew ConsolesPresenter(this, moduleInstaller, consoleStore, appData)` |
| 4 | Wire `Loaded`, `Unloaded`, `SizeChanged` |
| 5 | `LocalizableAttach.Hook(this, _localization, this)` |
| 6 | `UpdateResponsiveLayout()` |

---

## Methods (`ConsolesView`)

#### `void ApplyLocalization()`

**Purpose:** Sets localized text on page chrome (`Consoles_Title`, `Consoles_ModulesHeader`, empty labels). Calls **`_presenter.RefreshAsync()`** (fire-and-forget).

---

#### `internal Task RefreshAsync()`

**Purpose:** Delegates to **`_presenter.RefreshAsync()`** — public refresh entry for the shell.

---

#### `internal Task ShowModuleAsync(string sourcePath)`

**Purpose:** Delegates to **`_presenter.SelectModuleAsync(sourcePath)`** — focuses workspace on one module.

---

#### `private async void OnLoaded(object? sender, EventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `await _presenter.ActivateAsync()` |
| 2 | `StartAutoRefresh()` |
| 3 | `QueueConsoleLayoutRefresh()` |

---

#### `private void OnUnloaded(object? sender, EventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `StopAutoRefresh()` |
| 2 | `_presenter.Deactivate()` |

---

#### `private void OnSizeChanged(object? sender, EventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `UpdateResponsiveLayout()` |
| 2 | `QueueConsoleLayoutRefresh()` |

---

#### `private async void OnModuleSelectionChanged(object? sender, SelectionChangedEventArgs e)`

**Purpose:** If **`_suppressSelection`**, returns. If current item is **`ConsoleModuleItemViewModel`**, **`await _presenter.SelectModuleAsync(module.SourcePath)`**.

---

#### `private async void OnSessionSelectionChanged(object? sender, SelectionChangedEventArgs e)`

**Purpose:** If **`_suppressSelection`**, returns. If current item is **`ConsoleSessionItemViewModel`**, **`await _presenter.SelectSessionAsync(session.Id)`**.

---

#### `void IConsolesView.Render(ConsolesDashboardState state)

**Purpose:** Applies presenter snapshot to UI (suppresses selection events during update).

| Step | Action |
| --- | --- |
| 1 | `_suppressSelection = true` |
| 2 | Copy selected session fields into `_viewModel` (title, status, description, command line, footer, joined line text) |
| 3 | `SyncModuleItems` / `SyncSessionItems` on observable collections |
| 4 | Sync `ModulesCollection.SelectedItem` / `SessionsCollection.SelectedItem` |
| 5 | `SessionsPanel.IsVisible = state.ShowSessionList` |
| 6 | `UpdateResponsiveLayout(state.ShowSessionList)` |
| 7 | `QueueConsoleLayoutRefresh()` |
| 8 | `_suppressSelection = false` |

---

#### `private void StartAutoRefresh()`

**Purpose:** `StopAutoRefresh()`, new **`CancellationTokenSource`**, starts **`RunAutoRefreshAsync`**.

---

#### `private void StopAutoRefresh()`

**Purpose:** Cancels and disposes **`_autoRefreshCts`**.

---

#### `private async Task RunAutoRefreshAsync(CancellationToken cancellationToken)`

**Purpose:** Loop: **`Task.Delay(AutoRefreshInterval)`** → **`_presenter.RefreshAsync(forceModuleReload: false)`**. Swallows **`OperationCanceledException`**.

---

#### `private void UpdateResponsiveLayout(bool? showSessionList = null)

**Purpose:** Rebuilds **`WorkspaceLayout`** row/column definitions.

| Mode | Layout |
| --- | --- |
| Compact (`Width < CompactBreakpoint`) | Stacked rows: modules, optional sessions, output |
| Wide | Columns `260` + optional `260` + `*` for modules / sessions / output |

Uses **`showSessionList ?? SessionsPanel.IsVisible`** to omit session column when unified-only.

---

#### `private void QueueConsoleLayoutRefresh()`

**Purpose:** Calls **`RefreshConsoleLayout()`** immediately. If dispatcher available and not already queued, schedules 16 ms and 48 ms delayed **`RefreshConsoleLayout`** (WinUI measure settle).

---

#### `private void RefreshConsoleLayout()`

**Purpose:** `InvalidateMeasure()` on view, **`WorkspaceLayout`**, **`ModulesPanel`**, **`SessionsPanel`**, **`OutputPanel`**, **`ConsoleOutputHost`**.

---

#### `private static void SyncModuleItems(ObservableCollection<ConsoleModuleItemViewModel> target, IReadOnlyList<ConsoleModuleItemViewModel> source)`

**Purpose:** **`SyncKeyedItems`** with key **`SourcePath`**, updater **`UpdateFrom`**.

---

#### `private static void SyncSessionItems(ObservableCollection<ConsoleSessionItemViewModel> target, IReadOnlyList<ConsoleSessionItemViewModel> source)`

**Purpose:** **`SyncKeyedItems`** with key **`Id`**, updater **`UpdateFrom`**.

---

#### `private static void SyncKeyedItems<T>(...)`

**Purpose:** In-place observable collection sync by key (stable item instances for MAUI lists): update in place, move, insert, or trim tail.

---

#### `private static Dictionary<string, int> BuildIndex<T>(ObservableCollection<T> target, Func<T, string> keySelector)`

**Purpose:** Builds key → index map for **`SyncKeyedItems`**.

---

## Interface `IConsolesView`

#### `void Render(ConsolesDashboardState state)`

**Purpose:** Presenter pushes a fully built dashboard snapshot to the view.

---

## Class `ConsolesPresenter`

`internal sealed` — builds **`ConsolesDashboardState`** from **`ModuleConsoleStore`** + **`ModuleInstaller`**, honors [AppData](../Models/AppData/) console preferences.

### Constants

| Name | Value |
| --- | --- |
| `AllModulesModuleId` | `__all_modules__` |
| `GlobalUnifiedSessionId` | `__all_modules_unified__` |
| `UnifiedSessionId` | `__module_unified__` |

### Fields

| Name | Description |
| --- | --- |
| `_refreshLock` | `SemaphoreSlim(1)` — serializes refresh |
| `_knownModules` | Last discovered **`ModuleConfig`** list |
| `_isActive` | Subscribed to store events |
| `_refreshQueued` | Coalesces **`StateChanged`** bursts |
| `_showCompletedProcesses` | From **`AppDataStore.Data.Consoles`** |
| `_selectedModuleSourcePath`, `_selectedSessionId` | User selection |

---

#### `ConsolesPresenter(IConsolesView view, ModuleInstaller, ModuleConsoleStore, AppDataStore)`

**Purpose:** Stores dependencies; seeds **`_showCompletedProcesses`** from app data.

---

#### `public async Task ActivateAsync()`

**Purpose:** If already active, returns. Sets **`_isActive = true`**, subscribes **`_consoleStore.StateChanged`**, **`LoadPreferences()`**, **`await RefreshAsync(forceModuleReload: true)`**.

---

#### `public void Deactivate()`

**Purpose:** Unsubscribes **`StateChanged`**, **`_isActive = false`**.

---

#### `public async Task SelectModuleAsync(string sourcePath)`

**Purpose:** If same module (ordinal ignore case), returns. Sets **`_selectedModuleSourcePath`**, clears **`_selectedSessionId`**, **`await RefreshAsync(false)`**.

---

#### `public async Task SelectSessionAsync(string sessionId)`

**Purpose:** If same session id, returns. Sets **`_selectedSessionId`**, **`await RefreshAsync(false)`**.

---

#### `public Task RefreshAsync()`

**Purpose:** Calls **`RefreshAsync(forceModuleReload: true)`**.

---

#### `public async Task RefreshAsync(bool forceModuleReload)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `await _refreshLock.WaitAsync()` |
| 2 | `LoadPreferences()` |
| 3 | If `forceModuleReload` or empty `_knownModules`, **`DiscoverModulesAsync`** on background |
| 4 | Background: **`EnsureModules`**, **`GetSnapshot`**, **`BuildState`** |
| 5 | **`MainThread.InvokeOnMainThreadAsync(() => _view.Render(state))`** |
| 6 | Release lock |

---

#### `private void OnConsoleStateChanged(object? sender, EventArgs e)`

**Purpose:** If inactive or refresh already queued, returns. Coalesces: 100 ms delay → **`RefreshAsync(false)`**.

---

#### `private void LoadPreferences()`

**Purpose:** **`_appData.Data.Consoles.Normalize()`**; copies **`ShowCompletedProcesses`**.

---

#### `private ConsolesDashboardState BuildState(IReadOnlyList<ModuleConsoleModuleSnapshot> snapshots)`

**Purpose:** Builds module list (optional “all modules” row + active modules), session list (global unified / per-module unified / individual sessions), and output metadata. Resolves selection defaults, line buffers via **`ModuleConsoleStore`** (`GetUnifiedOverviewLines`, `GetUnifiedModuleLines`, `GetSessionLines`). Sets **`ShowSessionList`** when more than one session and not global scope.

---

#### `private static string LocalizeConsoleStage(string stage)`

**Purpose:** Maps store stage ids to **`LocalizationKeys.Consoles_*`** (`Run`, `Command`, `Service`, `Lifecycle`).

---

#### `private static ConsoleModuleItemViewModel MapModule(ModuleConsoleModuleSnapshot module)`

**Purpose:** Sidebar row: name, enabled/disabled status, active process count, last activity.

---

#### `private static ConsoleSessionItemViewModel MapSession(ModuleConsoleModuleSnapshot module, ModuleConsoleSessionSnapshot session)`

**Purpose:** List row with localized running/exit/observed status and preview text.

---

#### `private static string BuildSelectedStatus(ModuleConsoleSessionSnapshot session)`

**Purpose:** Status line for output header (running, exit code, observed process).

---

#### `private static string BuildFooter(ModuleConsoleSessionSnapshot session)`

**Purpose:** Footer with started/ended timestamps, line count, observed note.

---

#### `private static string FormatPid(int? processId)`

**Purpose:** Localized PID suffix or empty.

---

## Class `ConsolesDashboardState`

`internal sealed` — immutable render DTO (properties: **`Modules`**, **`SelectedModuleSourcePath`**, **`Sessions`**, **`ShowSessionList`**, **`SelectedSessionId`**, **`SelectedSessionKey`**, title/status/description/command line, **`SelectedSessionLines`**, **`SelectedSessionFooter`**).

---

## Class `ConsolesPageViewModel`

`public sealed`, **`INotifyPropertyChanged`** — binding surface for collections and selected session chrome.

| Property | Description |
| --- | --- |
| `Modules`, `Sessions` | `ObservableCollection` of item view models |
| `SelectedSessionId` … `SelectedSessionText` | Output pane bindings |

#### `private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)`

**Purpose:** Standard INPC helper.

---

## Class `ConsoleModuleItemViewModel`

#### `bool IsSameAs(ConsoleModuleItemViewModel other)`

**Purpose:** Compares **`SourcePath`**, **`Name`**, **`StatusText`**, **`ActivityText`**.

---

#### `void UpdateFrom(ConsoleModuleItemViewModel other)`

**Purpose:** Copies all bindable fields from another instance.

---

#### `private void SetProperty<T>(...)`

**Purpose:** INPC helper.

---

## Class `ConsoleSessionItemViewModel`

#### `bool IsSameAs(ConsoleSessionItemViewModel other)`

**Purpose:** Compares **`Id`**, paths, **`Title`**, **`StatusText`**, **`Preview`**.

---

#### `void UpdateFrom(ConsoleSessionItemViewModel other)`

**Purpose:** Copies all bindable fields from another instance.

---

#### `private void SetProperty<T>(...)`

**Purpose:** INPC helper.

---

## Dependencies

`ModuleInstaller`, `ModuleConsoleStore`, `AppDataStore`, `AppLocalizationService`. Uses [ConsoleOutputView](../Services/ConsoleOutputView/) from [MauiProgram](../MauiProgram/).
