---
title: "ModulesView"
draft: false
---

## Class `ModulesView`

`ASLM/Pages/ModulesView.xaml.cs` — responsive grid of **module cards** inside the shell. Implements **`ILocalizable`**, **`INotifyPropertyChanged`**.

---

### Constants

| Name | Value |
| --- | --- |
| `MinCardWidth` | `440` — used in `RecalculateGridSpan` |

---

### Fields

| Name | Description |
| --- | --- |
| `_updateManager` | App/module update checks and apply |
| `_moduleTrustService` | Resolves trust badges per card |
| `_launchCoordinator` | Launch/restart orchestration |
| `_localization` | `LocalizableAttach` |
| `_shell` | `AppShellPage` — overlays and state callbacks |
| `_gridSpan` | `CollectionView` column span |
| `_moreIconSource` | Tinted `icon_more.png` for card menus |

---

### XAML elements (`ModulesView.xaml`)

| Name | Role |
| --- | --- |
| `RootView` | Page root `ContentView` |
| `PageTitleLabel` | `Modules_Title` |
| `DashboardView` | `CollectionView`; `ItemsSource={Binding Modules}`; `Span={Binding GridSpan}` |
| `EmptyModulesLabel` | Shown when no modules |

Card template (data template) binds to `ModuleViewModel` properties and commands.

---

### Properties

| Name | Description |
| --- | --- |
| `Modules` | `ObservableCollection<ModuleViewModel>` |
| `GridSpan` | Responsive column count |
| `MoreIconSource` | Shared more-menu glyph |

---

## Public methods — `ModulesView`

#### `public ModulesView(UpdateManager, ModuleTrustService, ModuleLaunchCoordinator, AppLocalizationService)`

**Purpose:** Creates dashboard; hooks resize, theme, and localization.

**Steps:**

1. Store services; `InitializeComponent`; `BindingContext = this`.
2. Subscribe `DashboardView.HandlerChanged`, `Loaded`, `Unloaded`, `SizeChanged`.
3. `LocalizableAttach.Hook`; `RefreshMoreIconChrome`.

---

#### `public void ApplyLocalization()`

**Purpose:** Localizes page title, empty label, and every card.

**Steps:**

1. Set `PageTitleLabel`, `EmptyModulesLabel`.
2. `module.RefreshLocalizedLabels()` for each entry in `Modules`.

---

#### `protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** Raises view-level `PropertyChanged`.

---

#### `internal void Initialize(AppShellPage shell, List<ModuleConfig> modules, ModuleInstaller installer, ModuleRunner runner)`

**Purpose:** First open from shell — store shell and populate cards.

**Steps:**

1. `_shell = shell`; `PopulateModules(...)`.

---

#### `internal void RefreshModules(List<ModuleConfig> modules, ModuleInstaller installer, ModuleRunner runner)`

**Purpose:** Rebuilds all card view models after module list changes.

**Steps:**

1. `PopulateModules(...)`.

---

#### `internal void FocusModule(string sourcePath)`

**Purpose:** Scrolls matching card into view.

**Steps:**

1. Find `ModuleViewModel` by `SourcePath`.
2. `Dispatcher.Dispatch` → `DashboardView.ScrollTo(..., MakeVisible, animate: true)`.

---

#### `internal static ModuleViewModel CreateViewModelForDeferredUpdateOverlay(...)`

**Purpose:** Factory for notification-driven update UI without dashboard instance.

**Steps:**

1. `new ModuleViewModel` with no-op menu/configure/update callbacks.

---

## Private methods — `ModulesView`

#### `private void PopulateModules(List<ModuleConfig> modules, ModuleInstaller installer, ModuleRunner runner)`

**Purpose:** Clears and recreates all `ModuleViewModel` instances.

**Steps:**

1. `Modules.Clear`.
2. Add one VM per config with shell callbacks.

---

#### `private void OnModuleStateChanged()`

**Purpose:** Forwards card state changes to shell.

**Steps:**

1. `_shell?.OnModuleStateChanged()`.

---

#### `private void OnMenuToggleRequested(ModuleViewModel module)`

**Purpose:** Exclusive mini-menu open (one card at a time).

**Steps:**

1. Each card `SetMenuOpen` only for clicked card when it was closed.

---

#### `private void OpenConfigureUpdates(ModuleViewModel module)` / `OpenUpdateDialog(ModuleViewModel module)`

**Purpose:** Opens shell update overlay in Configure or Update mode.

**Steps:**

1. `CloseAllMenus`; `_shell?.OpenModuleUpdateOverlay(module, mode)`.

---

#### `private void CloseAllMenus()`

**Purpose:** Closes every card flyout.

**Steps:**

1. `SetMenuOpen(false)` on all modules.

---

#### `private void OnSizeChanged(object? sender, EventArgs e)`

**Purpose:** Recomputes grid columns on resize.

**Steps:**

1. `RecalculateGridSpan()`.

---

#### `private void OnLoaded(object? sender, EventArgs e)`

**Purpose:** Theme listeners and WinUI list transition disable.

**Steps:**

1. Subscribe `ThemeService.PaletteApplied`, `Application.RequestedThemeChanged`.
2. `RefreshMoreIconChrome`; `DisableDashboardItemTransitions`.

---

#### `private void OnUnloaded(object? sender, EventArgs e)`

**Purpose:** Unsubscribes theme events.

---

#### `private void OnPaletteAppliedForMoreIcon()` / `OnApplicationRequestedThemeChanged(...)`

**Purpose:** Main-thread `RefreshMoreIconChrome` on theme change.

---

#### `private void RefreshMoreIconChrome()`

**Purpose:** Tinted more icon from `LabelPrimary` palette.

**Steps:**

1. `PackagedIconTintCache.Get("icon_more.png", iconTint)` → `MoreIconSource`.

---

#### `private void OnDashboardViewHandlerChanged(object? sender, EventArgs e)`

**Purpose:** Re-applies transition disable when handler materializes.

---

#### `private void OnBackgroundTapped(object? sender, TappedEventArgs e)`

**Purpose:** Tap outside closes open menus.

**Steps:**

1. `CloseAllMenus()`.

---

#### `private void RecalculateGridSpan()`

**Purpose:** `GridSpan = max(1, floor((Width - 60) / MinCardWidth))`.

---

#### `private void DisableDashboardItemTransitions()`

**Purpose:** Entry point for platform list transition removal.

**Steps:**

1. Windows: `DisableWinUiItemTransitions` on platform view.

---

#### `private static void DisableWinUiItemTransitions(DependencyObject element)` (Windows)

**Purpose:** Clears `ItemContainerTransitions` on `ListViewBase` and recurses visual tree.

---

## Class `ModuleViewModel`

`public class ModuleViewModel` in the same file — one card per [ModuleConfig](../Models/ModuleConfig/).

### Constants and static

| Name | Value |
| --- | --- |
| `SharedSourceModeOptions` | `release`, `pre-release`, `branch` |
| `LatestReleaseSelection` | `ModuleUpdateConfig.LatestReleaseTag` |

### Private fields (selected)

| Name | Description |
| --- | --- |
| `_config`, `_installer`, `_runner`, `_launchCoordinator`, `_updateManager` | Core services |
| `_trustLevel` | Resolved once at construction |
| `_onStateChanged`, menu/update callbacks | Parent dashboard wiring |
| `_branchOptions`, `_releaseOptions` | Picker data |
| `_selectedSourceMode`, `_selectedBranch`, `_selectedReleaseOption` | Update UI selection |
| `_updateStatusPhase`, `_updateStatusFormatArg`, `_updateStatus` | Badge text |
| `_updateCandidate`, `_hasUpdate` | Check/install state |
| `_isCheckingUpdate`, `_isUpdating`, `_isRestarting`, `_isStarting`, `_isMenuOpen` | Busy flags |
| `_updateLogBuffer`, progress fields | Update dialog session |
| Branch/release load guards | `_hasLoadedBranchOptions`, `_isRefreshingBranchOptions`, etc. |

### Enum `UpdateStatusPhase` (private)

`ReadyToCheck`, `Checking`, `UpToDate`, `Available`, `CheckFailed`, `Updating`, `Updated`, `Failed`.

---

## `ModuleViewModel` — methods

### Initialization and localization

#### `public ModuleViewModel(ModuleConfig config, ModuleInstaller, ModuleRunner, ModuleLaunchCoordinator, UpdateManager, ModuleTrustService, Action onStateChanged, Action<ModuleViewModel> onMenuToggleRequested, Action<ModuleViewModel> onConfigureUpdatesRequested, Action<ModuleViewModel> onUpdateDialogRequested)`

**Purpose:** Creates card VM, commands, and hydrates pending update from disk.

**Steps:**

1. Store deps; `_config.Normalize()`; init branch list and commands.
2. `TryHydratePendingUpdateFromConfig`; default phase if needed.
3. `RefreshLocalizedLabels()`.

---

#### `private void SetUpdatePhase(UpdateStatusPhase phase, string? formatArg = null)`

**Purpose:** Sets phase and localized `UpdateStatus` text.

---

#### `private static string FormatUpdateStatus(UpdateStatusPhase phase, string? formatArg)`

**Purpose:** Maps phase to `LocalizationKeys.ModuleUpdate_Status_*`.

---

#### `private void RefreshUpdateStatusLocalization()`

**Purpose:** Re-applies localization for current phase.

---

#### `internal void RefreshLocalizedLabels()`

**Purpose:** Refreshes all card action labels and update status.

**Steps:**

1. Load strings from `LocalizationKeys.Modules_*` and `OnPropertyChanged` each label property.

---

### Menu commands

#### `private void ExecuteToggleMenuCommand()` / `ExecuteCloseMenuCommand()` / `ExecuteOpenConfigureUpdatesCommand()` / `ExecuteOpenUpdateDialogCommand()`

**Purpose:** Menu toggle and shell overlay entry points.

---

#### `internal void SetMenuOpen(bool isOpen)`

**Purpose:** Sets `IsMenuOpen` visibility.

---

### Command guards

#### `private bool CanCheckOrUpdate()` / `CanApplyUpdate()` / `CanLaunch()` / `CanStop()` / `CanRestart()`

**Purpose:** `Command` `CanExecute` predicates from runtime flags.

---

#### `private void RefreshCommandStates()`

**Purpose:** `ChangeCanExecute` on all commands.

---

### Update preferences persistence

#### `private void PersistUpdatePreferences()`

**Purpose:** Normalizes and saves module update section via `UpdateManager`.

---

#### `private void PersistPendingUpdateSnapshot()`

**Purpose:** Writes `PendingUpdate` to manifest for badge survival across rebuilds.

---

#### `private void TryHydratePendingUpdateFromConfig()`

**Purpose:** Restores badge from disk via `TryRestorePendingUpdateCandidate`.

---

#### `private void ClearStalePendingUpdateOnDisk()`

**Purpose:** Removes invalid `PendingUpdate` block.

---

#### `private void SynchronizeSelectionForUpdateOperation()`

**Purpose:** Syncs UI selection into `_config.Update` before check/install.

---

#### `internal void ApplyBranchSelection(string branch)`

**Purpose:** Public entry for dialog picker with refresh guard bypass.

**Steps:**

1. `SetBranchSelection(branch, ignoreRefreshGuard: true)`.

---

#### `private void SetBranchSelection(string branch, bool ignoreRefreshGuard)`

**Purpose:** Sets branch; clears candidate; resets phase.

---

### Update check and apply

#### `private async void ExecuteCheckUpdateCommand()`

**Purpose:** Closes menu; `RefreshUpdateStateAsync(false)`.

---

#### `private async void ExecuteApplyUpdateCommand()`

**Purpose:** `ApplyUpdateAsync()`.

---

#### `internal async Task RefreshUpdateStateAsync(bool forceOptionLoad)`

**Purpose:** Checks for updates; updates badge and candidate.

**Steps:**

1. Guard `IsCheckingUpdate`; set checking phase.
2. `EnsureSelectionOptionsLoadedAsync`; `SynchronizeSelectionForUpdateOperation`.
3. `CheckModuleUpdateAsync`; set `HasUpdate` and phase.
4. `PersistPendingUpdateSnapshot`; clear checking flag.

---

#### `internal async Task EnsureSelectionOptionsLoadedAsync(bool forceRefresh)`

**Purpose:** Loads branches or releases for current mode.

---

#### `private async Task LoadBranchesAsync()`

**Purpose:** Fills `BranchOptions` from GitHub; preserves selection.

---

#### `private async Task LoadReleaseOptionsAsync()`

**Purpose:** Fills `ReleaseOptions` with virtual “latest” + releases.

---

#### `private static List<UpdateCandidate> BuildReleaseOptions(IReadOnlyList<UpdateCandidate> releases)`

**Purpose:** Prepends `BuildLatestReleaseOption` when releases exist.

---

#### `private static UpdateCandidate BuildLatestReleaseOption(UpdateCandidate latest)`

**Purpose:** Virtual latest row keeping concrete install tag.

---

#### `private string ResolvePreferredReleaseSelectionKey(IEnumerable<UpdateCandidate> releases)`

**Purpose:** Persisted tag or `LatestReleaseSelection`.

---

#### `private void ApplyCheckResultSelection()`

**Purpose:** Refreshes install button visibility after check.

---

#### `internal async Task<bool> ApplyUpdateAsync(IProgress<string>? log, IProgress<DownloadProgress>? progress)`

**Purpose:** Runs install; reloads config; re-checks state.

**Steps:**

1. Resolve install candidate; `IsUpdating = true`; disable module if running.
2. Combined log/progress sinks; `ApplyModuleUpdateAsync`.
3. On success `ReloadInstalledConfigAsync`; clear update; refresh options/state.
4. `_onStateChanged` when apply entered.

---

### Launch, stop, restart

#### `private async void OnLaunch()`

**Purpose:** `LaunchOrEnsureRunningBySourcePathAsync` with fresh settings/commands.

**Steps:**

1. `IsStarting = true`; `ReloadEditableConfigAsync`.
2. Apply `EffectiveConfig` on success; `NotifyStateChanged` + shell callback.

---

#### `private async void OnStop()`

**Purpose:** Stops runner; persists `Status.Enabled = false`.

---

#### `private async void OnRestart()`

**Purpose:** Stop, delay, relaunch via coordinator.

**Steps:**

1. `IsRestarting = true`; stop; 1s delay; launch; sync enabled state.

---

### Config reload

#### `private async Task ReloadEditableConfigAsync()`

**Purpose:** Reloads `Settings` and `Commands` before launch/restart.

---

#### `private async Task ReloadInstalledConfigAsync()` / `private void ApplyReloadedConfig(ModuleConfig freshConfig)`

**Purpose:** After update, copies manifest fields into live `_config` and rebuilds pickers.

---

### UI state notifications

#### `private void NotifyStateChanged()` / `private void NotifyModuleMetadataChanged()`

**Purpose:** Raises running/metadata property changes and command states.

---

### Install candidate resolution

#### `private async Task<UpdateCandidate?> ResolveSelectedInstallCandidateAsync()`

**Purpose:** Branch mode → manager resolve; release mode → selected release install candidate.

---

#### `private UpdateCandidate? ResolveSelectedReleaseInstallCandidate()`

**Purpose:** Null if same as installed ref via `ReleaseTagOrdering`.

---

#### `private static string? ResolveReleaseSelectionKey(UpdateCandidate? candidate)`

**Purpose:** Latest virtual → `LatestReleaseSelection`; else `ReleaseTag`.

---

#### `private static bool IsLatestReleaseOption(UpdateCandidate? candidate)`

**Purpose:** `IsVirtualLatest == true`.

---

#### `private static string? FormatSelectedReleaseTarget(UpdateCandidate? candidate)`

**Purpose:** Display string for dialog header including latest + version.

---

### Update dialog session

#### `internal void ResetUpdateSession(bool clearLog)`

**Purpose:** Clears progress/log cache for new dialog action.

---

#### `internal void ResetCompletedUpdateSession()`

**Purpose:** Clears finished session unless busy/updating phase.

---

#### `internal void AppendUpdateLog(string message)` / `internal void SetUpdateActivityStatus(string value)`

**Purpose:** Dialog log and status line updates.

---

#### `private void AdvanceProgressFromLog(string message)`

**Purpose:** Coarse progress from installer log keywords.

---

#### `private void UpdateCachedDownloadProgress(DownloadProgress progress)`

**Purpose:** File progress, speed estimate, detail line.

---

#### `private void SetOverallProgress(double value)` / `SetFileProgress(double value)` / `SetDownloadDetail(string value)`

**Purpose:** Clamped progress property updates.

---

#### `private static string FormatBytes(long bytes)`

**Purpose:** Human-readable B/KB/MB/GB.

---

#### `protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** Raises VM `PropertyChanged`.

---

## Layout behavior (summary)

| Event | Action |
| --- | --- |
| `OnSizeChanged` | `RecalculateGridSpan` |
| Theme / palette | `RefreshMoreIconChrome` |
| `OnModuleStateChanged` | `_shell.OnModuleStateChanged()` |
| WinUI `CollectionView` | Item transitions cleared |

---

## Dependencies

`UpdateManager`, `ModuleTrustService`, `ModuleLaunchCoordinator`, `AppLocalizationService`; per-card `ModuleInstaller`, `ModuleRunner`.
