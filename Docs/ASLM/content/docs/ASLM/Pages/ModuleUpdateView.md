---
title: "ModuleUpdateView"
draft: false
---

## Enum `ModuleUpdateMode`

| Value | UI title key | Purpose |
| --- | --- | --- |
| `Configure` | `ModuleUpdate_Title_Configure` | Edit update prefs |
| `Update` | `ModuleUpdate_Title_Update` | Check and apply update |

---

## Class `ModuleUpdateView`

`ASLM/Pages/ModuleUpdateView.xaml` + `ModuleUpdateView.xaml.cs` — modal overlay for one module’s GitHub update settings and install flow. Binds to [ModuleViewModel](ModulesView/) update state/log/progress.

Implements **`ILocalizable`**, **`INotifyPropertyChanged`**.

`BindingContext`: **`this`**.

---

### Dialog sizing constants

| Name | Value |
| --- | --- |
| `DialogWidthFactor` | `0.78` |
| `DialogHeightFactor` | `0.82` |
| `MinDialogWidth` × `MinDialogHeight` | `860` × `560` |
| `MaxDialogWidth` × `MaxDialogHeight` | `1200` × `820` |

---

### Fields

| Name | Description |
| --- | --- |
| `_emptyBranches`, `_emptyReleases` | Fallback picker sources |
| `_module` | Attached **`ModuleViewModel`** |
| `_mode` | Configure vs Update |
| `_isSynchronizingPickers` | Suppresses picker handlers during programmatic sync |
| `_logSyncCts`, `_logSyncQueued`, `_logSyncRequested` | Batched log binding flush |

---

### XAML elements

| Name | Role |
| --- | --- |
| (root) | `OverlayBackground`, backdrop tap |
| `DialogBorder` | Sized in code (`UpdateDialogSize`) |
| Close header | `DialogTitle`, `DialogSubtitle`, `CloseButton` |
| `CurrentVersionHeaderLabel` + bound value | Installed version |
| `SelectedTargetHeaderLabel` + bound value | Target release/branch |
| `UpdateSourceHeaderLabel`, `PrefsAutoSavedLabel` | |
| `SourceModePicker` | `release` / pre-release / `branch` |
| `SearchModeLabel`, `ReleaseVersionLabel`, `RepositoryBranchLabel` | |
| `ReleasePicker`, `BranchPicker` | GitHub refs |
| `CheckUpdatesButton`, `InstallUpdateButton` | Footer actions |
| `OverallProgressLabel`, progress bars, download detail | Activity section |
| `UpdateLogPanel` | Panel for the console, bound to `HasLog` |
| `UpdateLogConsole` | **`ConsoleOutputView`** ← `LogText`, `UpdateLogSessionKey` |

---

## Constructor

#### `ModuleUpdateView(AppLocalizationService localization)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `InitializeComponent()`, **`LocalizableAttach.Hook`** |
| 2 | **`ApplyBorderlessPickerStyle`** on three pickers |
| 3 | `BindingContext = this`, `SizeChanged += OnViewSizeChanged` |

---

## Events

#### `event EventHandler? CloseRequested`

**Purpose:** Host hides overlay; cancels log sync and **`DetachModule()`**.

---

## Bound properties

Delegate to **`_module`** when attached (empty/fallback when null). Key members: **`DialogTitle`**, **`DialogSubtitle`**, **`CurrentVersionLabel`**, **`TargetVersionLabel`**, **`SourceModeOptions`**, **`SelectedSourceMode`**, **`ReleaseOptions`**, **`SelectedReleaseOption`**, **`BranchOptions`**, **`SelectedBranch`**, **`IsBranchMode`**, **`IsReleaseMode`**, **`HasUpdate`**, **`CanInstallUpdate`**, **`CanCheckUpdates`**, **`ShowInstallAction`**, **`IsBusy`**, **`ActivityTitle`**, **`ActivityStatus`**, progress fields, **`LogText`**, **`HasLog`**, **`UpdateLogSessionKey`** (generates dynamically for new update sessions).

Setters on **`SelectedSourceMode`** / release / branch forward to **`ModuleViewModel`** and trigger option load.

---

## Member reference
#### `void ApplyLocalization()`

**Purpose:** Tooltips, section headers, buttons; refresh **`DialogTitle`**, **`DialogSubtitle`**, **`ActivityTitle`**, **`ActivityStatus`**.

---

#### `public Task OpenAsync(ModuleViewModel module, ModuleUpdateMode mode)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | **`AttachModule(module)`** |
| 2 | **`_mode = mode`**, **`ResetCompletedUpdateSession()`** |
| 3 | **`UpdateDialogSize()`**, **`RaiseDialogProperties()`** |
| 4 | **`SyncPickerSelections()`**, **`SyncLogView()`** |
| 5 | **`EnsureModeOptionsLoadedAsync(false)`** |

---

#### `private void OnBackgroundTapped(object? sender, EventArgs e)`

**Purpose:** **`RequestClose()`**.

---

#### `private void OnDialogTapped(object? sender, EventArgs e)`

**Purpose:** No-op — dialog taps do not close overlay.

---

#### `private void OnCloseClicked(object? sender, EventArgs e)`

**Purpose:** **`RequestClose()`**.

---

#### `private void RequestClose()`

**Purpose:** Raises **`CloseRequested`**; cancels log sync; **`DetachModule()`**.

---

#### `private void OnViewSizeChanged(object? sender, EventArgs e)`

**Purpose:** **`UpdateDialogSize()`**.

---

#### `private void UpdateDialogSize()`

**Purpose:** Sets **`DialogBorder`** width/height from host size × factors, clamped min/max.

---

#### `private static double ClampDialogSize(double value, double minValue, double maxValue)`

**Purpose:** `Math.Max(min, Math.Min(max, value))`.

---

#### `private void AttachModule(ModuleViewModel module)`

**Purpose:** If changed: **`DetachModule`**, assign **`_module`**, subscribe **`PropertyChanged`**.

---

#### `private void DetachModule()`

**Purpose:** Unsubscribe and clear **`_module`**.

---

#### `private void OnModulePropertyChanged(object? sender, PropertyChangedEventArgs e)`

**Purpose:** **`RaiseModuleProperties()`**; sync pickers when selection/options change; queue branch resync / log sync per property name.

---

#### `private static bool ShouldSyncPickerSelection(string? propertyName)`

**Purpose:** True for selection, release, branch, or option list changes.

---

#### `private async void OnCheckUpdatesClicked(object? sender, EventArgs e)`

**Purpose:** **`await CheckForUpdatesAsync(forceOptionLoad: false, announceInLog: true)`**.

---

#### `private void OnSourceModeSelectionChanged(object? sender, EventArgs e)`

**Purpose:** If not syncing and item is string → **`SelectedSourceMode = mode`**.

---

#### `private void OnReleaseSelectionChanged(object? sender, EventArgs e)`

**Purpose:** If not syncing and item is **`UpdateCandidate`** → **`SelectedReleaseOption`**.

---

#### `private void OnBranchSelectionChanged(object? sender, EventArgs e)`

**Purpose:** If not syncing and item is string → **`_module.ApplyBranchSelection(branch)`**.

---

#### `private async Task CheckForUpdatesAsync(bool forceOptionLoad, bool announceInLog)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | **`ApplyPickerSelectionsToModule()`** |
| 2 | Optional log line + activity status |
| 3 | **`await module.RefreshUpdateStateAsync(forceOptionLoad)`** |
| 4 | Log status; on error set failed status |
| 5 | **`RaiseModuleProperties`** / **`RaiseActivityProperties`** if still attached |

---

#### `private async Task EnsureModeOptionsLoadedAsync(bool forceRefresh)`

**Purpose:** **`await module.EnsureSelectionOptionsLoadedAsync`**, re-sync pickers; branch mode queues deferred resync; errors → activity status.

---

#### `private async void OnInstallUpdateClicked(object? sender, EventArgs e)`

**Purpose:** **`await InstallUpdateAsync()`**.

---

#### `private async Task InstallUpdateAsync()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | **`ApplyPickerSelectionsToModule()`** |
| 2 | Guard **`CanInstallSelectedUpdate`** / not updating |
| 3 | **`ResetUpdateSession`**, log start |
| 4 | **`await module.ApplyUpdateAsync()`** |
| 5 | Set success/failure activity + log lines |
| 6 | Refresh bindings if still attached |

---

#### `private bool ApplyPickerSelectionsToModule()`

**Purpose:** Pushes picker values into **`ModuleViewModel`**; branch mode requires non-empty branch or shows **`SelectBranchRequired`**.

---

#### `private string? ResolveSelectedBranchFromPicker()`

**Purpose:** **`SelectedItem`**, index into **`BranchOptions`**, or match **`SelectedBranch`**.

---

#### `private void SyncLogView()`

**Purpose:** Cancel pending sync; immediate **`OnPropertyChanged`** for **`LogText`**, **`HasLog`**.

---

#### `private void QueueLogViewSync()`

**Purpose:** Coalesced **`FlushLogViewAsync`** (~75 ms) while log streams.

---

#### `private async Task FlushLogViewAsync(CancellationToken ct)`

**Purpose:** Batched **`OnPropertyChanged(LogText)`** on UI thread until quiescent.

---

#### `private void SyncPickerSelections()`

**Purpose:** UI thread: set source/release/branch picker items from view model (**`ReapplyBranchPickerFromViewModel`**).

---

#### `private void QueueBranchPickerResyncAfterListChange()`

**Purpose:** **`BranchPickerDeferredResyncLoopAsync`** (3 attempts, 16 ms apart).

---

#### `private async Task BranchPickerDeferredResyncLoopAsync()`

**Purpose:** Re-applies branch selection after **`BranchOptions`** refresh (WinUI ComboBox).

---

#### `private async Task ResyncBranchPickerAfterBranchUiShownAsync()`

**Purpose:** 48 ms delay after branch UI shown → **`ReapplyBranchPickerFromViewModel`**.

---

#### `private void ReapplyBranchPickerFromViewModel()`

**Purpose:** Sets **`SelectedIndex`** / **`SelectedItem`**; **`ForceWinUiBranchPickerSelection`**.

---

#### `private void ForceWinUiBranchPickerSelection(int index)`

#if WINDOWS — sets WinUI **`ComboBox.SelectedIndex`** (guarded).

---

#### `private static void ApplyBorderlessPickerStyle(Picker picker)`

**Purpose:** Transparent WinUI ComboBox chrome on **`HandlerChanged`**.

---

#### `private void RaiseDialogProperties()`

**Purpose:** **`DialogTitle`**, **`DialogSubtitle`**, module + activity properties.

---

#### `private void RaiseModuleProperties()`

**Purpose:** All module-mirrored bindings + **`UpdateLogSessionKey`**.

---

#### `private void RaiseActivityProperties()`

**Purpose:** Activity section + **`CanInstallUpdate`**.

---

#### `protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** INPC raise.

---

## Dependencies

`AppLocalizationService` (constructor). Operational work on attached **`ModuleViewModel`** (`UpdateManager`, `ModuleInstaller`, etc.).
