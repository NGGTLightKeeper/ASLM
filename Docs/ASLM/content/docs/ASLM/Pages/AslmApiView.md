---
title: "AslmApiView"
draft: false
---

## Class `AslmApiView`

`ASLM/Pages/AslmApiView.xaml` + `AslmApiView.xaml.cs` — lists the local **ASLM API mirror** server URL and per-module host endpoints. Implements **`ILocalizable`**, **`INotifyPropertyChanged`**.

`BindingContext`: **`this`**.

Shell hides navigation when `AppData.Api.ServerEnabled` is false ([AppShellPage](AppShellPage/) `ApplyAslmApiNavigationState`).

---

### Constants

| Name | Value |
| --- | --- |
| `ModuleStateRefreshInterval` | `15` seconds (module display-name cache TTL) |

---

### Fields

| Name | Description |
| --- | --- |
| `_hostRows` | Stable row map keyed by **`AslmApiHostViewModel.BuildKey`** |
| `_moduleDisplayStates` | Module id → **`AslmApiModuleDisplayState`** |
| `_moduleDisplayStatesLoadTask` | Background discovery task |
| `_moduleDisplayStateLoadVersion` | Stale-load guard |
| `_moduleDisplayStatesLoaded` / `_moduleDisplayStatesLoadedAt` | Cache flags |
| `_refreshLoopCts` | 4-second host refresh while visible |
| `_isVisible` | Loaded flag |
| `_serverUrl`, `_canOpenServer` | Bound server chrome |

---

### XAML elements

| Name | Role |
| --- | --- |
| `PageTitleLabel` | `AslmApi_Title` |
| `OpenServerButton` | Opens mirror root; `IsEnabled={Binding CanOpenServer}` |
| (row 1 border) | Read-only **`ServerUrl`** label |
| `HostsHeaderLabel` | `AslmApi_Hosts` |
| `HostsCollection` | `ItemsSource={Binding Hosts}` |
| Empty view | Label text set in **`ApplyLocalization`** (`AslmApi_NoHosts`) |

Item template: **`Title`**, disabled badge, copy button + **`MirrorUrl`**.

---

## Constructor

#### `AslmApiView(AslmApiServer, NotificationCenter, ModuleInstaller, AppLocalizationService)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Store services |
| 2 | `InitializeComponent()`, `BindingContext = this` |
| 3 | `Loaded` / `Unloaded`, **`LocalizableAttach.Hook`** |
| 4 | Subscribe **`_apiServer.StateChanged`**, **`_moduleInstaller.ModulesChanged`** |

---

## Methods (`AslmApiView`)

#### `void ApplyLocalization()`

**Purpose:** Localizes title, open button, hosts header, empty view. Calls **`host.RefreshLocalizationLabels()`** for each row; **`OnPropertyChanged(Hosts)`**.

---

#### `protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** Raises **`PropertyChanged`** for view bindings.

---

#### `private void OnLoaded(object? sender, EventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `_isVisible = true` |
| 2 | Subscribe **`ThemeService.PaletteApplied`**, **`RequestedThemeChanged`** |
| 3 | `RefreshHostCopyButtonChrome()` |
| 4 | `RefreshAsync()` (fire-and-forget) |
| 5 | `StartRefreshLoop()` |

---

#### `private void OnUnloaded(object? sender, EventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `_isVisible = false` |
| 2 | Unsubscribe theme events |
| 3 | `StopRefreshLoop()` |

---

#### `private void OnPaletteAppliedForCopyButtons()`

**Purpose:** **`MainThread.BeginInvokeOnMainThread(RefreshHostCopyButtonChrome)`**.

---

#### `private void OnApplicationRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)`

**Purpose:** **`MainThread.BeginInvokeOnMainThread(RefreshHostCopyButtonChrome)`**.

---

#### `private void RefreshHostCopyButtonChrome()`

**Purpose:** Calls **`RefreshCopyButtonChrome()`** on every **`Hosts`** row.

---

#### `private async void OnOpenServerClicked(object? sender, EventArgs e)`

**Purpose:** If **`CanOpenServer`**, **`Launcher.Default.OpenAsync(_apiServer.BaseUrl)`**.

---

#### `internal async Task RefreshAsync()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `ApplyServerState()` |
| 2 | `EnsureModuleDisplayStatesLoading()` |
| 3 | `hosts = await Task.Run(_apiServer.GetHosts)` |
| 4 | `SynchronizeHostRows(hosts, _moduleDisplayStates)` |

---

#### `private void SynchronizeHostRows(IReadOnlyList<AslmApiHostInfo> hosts, IReadOnlyDictionary<string, AslmApiModuleDisplayState> moduleStates)`

**Purpose:** Orders hosts by module name, port, host key. Updates or inserts **`AslmApiHostViewModel`** rows in **`Hosts`** without recreating the collection; removes stale keys from **`_hostRows`**.

---

#### `private static Dictionary<string, AslmApiModuleDisplayState> BuildModuleDisplayStates(IEnumerable<ModuleConfig> modules)`

**Purpose:** Distinct module id → **`(Name, Status.Enabled)`**.

---

#### `private static AslmApiModuleDisplayState ResolveModuleDisplayState(string moduleId, IReadOnlyDictionary<...> moduleStates)`

**Purpose:** Returns cached state or fallback **`(NormalizeDisplayName(moduleId), true)`**.

---

#### `private void MoveHostRowIfNeeded(AslmApiHostViewModel row, int desiredIndex)`

**Purpose:** **`Hosts.Move`** when sort order changes.

---

#### `private void EnsureModuleDisplayStatesLoading()`

**Purpose:** Starts **`LoadModuleDisplayStatesAsync`** when cache stale and no active load.

---

#### `private async Task LoadModuleDisplayStatesAsync(int loadVersion)`

**Purpose:** Background **`DiscoverModulesAsync`** → **`BuildModuleDisplayStates`**. On UI thread (if version matches): update cache, refresh row module state, optionally re-sync hosts from **`GetHosts()`**.

---

#### `private void StartRefreshLoop()`

**Purpose:** New CTS; **`RunRefreshLoopAsync`** every 4 seconds while visible.

---

#### `private void StopRefreshLoop()`

**Purpose:** Cancel/dispose **`_refreshLoopCts`**.

---

#### `private async Task RunRefreshLoopAsync(CancellationToken ct)`

**Purpose:** Loop: delay 4 s → **`MainThread.InvokeOnMainThreadAsync(RefreshAsync)`**.

---

#### `private void OnServerStateChanged(object? sender, EventArgs e)`

**Purpose:** **`MainThread.BeginInvokeOnMainThread`** → **`RefreshAsync`**.

---

#### `private void OnModulesChanged(object? sender, EventArgs e)`

**Purpose:** Invalidates module cache; if visible, **`RefreshAsync`**.

---

#### `private void ApplyServerState()`

**Purpose:** **`ServerUrl = _apiServer.BaseUrl`**, **`CanOpenServer = _apiServer.IsRunning`**.

---

#### `private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)`

**Purpose:** Assigns field, **`OnPropertyChanged`**, returns whether value changed.

---

## Record `AslmApiModuleDisplayState`

`(string Name, bool IsEnabled)` — lightweight module metadata for host rows.

---

## Class `AslmApiHostViewModel`

`public class`, **`INotifyPropertyChanged`** — one mirror host row.

### Bound properties (computed unless noted)

| Member | Description |
| --- | --- |
| `ModuleId`, `ModuleName`, `HostKey`, `HostName`, `Port`, `MirrorUrl` | Endpoint fields |
| `IsModuleDisabled` | `!moduleState.IsEnabled` |
| `Key` | `BuildKey(ModuleId, HostKey)` |
| `Title` | `"{ModuleName} / {HostName}"` |
| `ModuleStatusText` | `AslmApi_Disabled` when disabled |
| `CopyButtonBackground`, `CopyIconSource` | Themed copy affordance |
| `CopyCommand` | Lazy command → **`CopyMirrorUrlAsync`** |

---

#### `AslmApiHostViewModel(AslmApiHostInfo host, AslmApiModuleDisplayState moduleState, NotificationCenter notifications)`

**Purpose:** **`Update(host, moduleState)`**, **`RefreshCopyButtonChrome()`**.

---

#### `void RefreshLocalizationLabels()`

**Purpose:** **`OnPropertyChanged(nameof(ModuleStatusText))`**.

---

#### `void RefreshCopyButtonChrome()`

**Purpose:** White/black fill by dark mode; tinted **`icon_copy.png`** via **`PackagedIconTintCache`**.

---

#### `private async Task CopyMirrorUrlAsync()`

**Purpose:** **`Clipboard.SetTextAsync(MirrorUrl)`** + **`NotificationCenter.PublishSystemToast`**.

---

#### `public void Update(AslmApiHostInfo host, AslmApiModuleDisplayState moduleState)`

**Purpose:** Updates ids, host fields, **`MirrorUrl`**; **`OnPropertyChanged(Key)`**.

---

#### `public void UpdateModuleState(AslmApiModuleDisplayState moduleState)`

**Purpose:** Updates name/disabled flag; notifies **`Title`**, **`ModuleStatusText`**.

---

#### `private static bool IsAppDarkAppearance()`

**Purpose:** **`RequestedTheme`** or **`ThemeService.IsSystemDark()`** for Unspecified.

---

#### `public static string BuildKey(string moduleId, string hostKey)`

**Purpose:** `"{moduleId}\n{hostKey}"`.

---

#### `public static string NormalizeDisplayName(string value)`

**Purpose:** Splits on `-`/`_`/space; title-cases words; preserves `API`/`UI`.

---

#### `private static string NormalizeHostName(string hostKey)`

**Purpose:** Trims `-port` / `_port` / ` port` suffixes, then **`NormalizeDisplayName`**.

---

#### `private static string TrimHostPortSuffix(string value)`

**Purpose:** Strips conventional port suffixes from host keys.

---

#### `private static string FormatDisplayWord(string word)`

**Purpose:** Title case with acronym exceptions.

---

#### `private void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** INPC raise.

---

#### `private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)`

**Purpose:** INPC assign helper.

---

## Dependencies

`AslmApiServer`, `NotificationCenter`, `ModuleInstaller`, `AppLocalizationService`.
