---
title: "SetupWizardPage"
draft: false
---

## Class `SetupWizardPage`

`ASLM/Pages/SetupWizardPage.xaml` + `SetupWizardPage.xaml.cs` — first-run **`ContentPage`** (`Title`: ASLM Setup). Shown from [LoadingPage](LoadingPage/) when `AppData.IsFirstRun`. Implements **`ILocalizable`**, **`INotifyPropertyChanged`** (install log bindings).

---

### Constants

| Name | Value |
| --- | --- |
| `TotalSteps` | `3` (Numbered steps, excludes welcome step `0`) |

---

### Fields

| Name | Description |
| --- | --- |
| `_currentStep` | `0…3` active step |
| `_moduleChecks` | `(ModuleConfig, CheckBox)` for step 3 |
| `_logBuffer` | Install log for **`ConsoleOutputView`** |
| `_installLogSessionKey` | Scroll reset key per install run |
| `_pendingFastSetup` | Fast path skips profile/port panels |
| `_showDockerGate` | Docker CLI gate on step 1 |
| `_skipDockerStep` | Set when Docker CLI already installed |
| `_cts` | Cancels running install |
| `_logLock`, `_logFlushQueued`, `_logFlushRequested` | Batched log UI |
| `_installLogLayoutRefreshQueued` | Console host measure coalescing |
| `_lastDownloadedBytes`, `_lastSpeedUpdate`, `_lastSpeed` | Download speed label |

---

### XAML elements

Three-row grid: **header** | **content** | **footer**.

| Name | Role |
| --- | --- |
| `HeaderRow` | Title + step label + log toggle (`IsVisible` when step > 0) |
| `HeaderTitleLabel` | `SetupWizard_Title` |
| `StepLabel` | Dynamic step caption |
| `ToggleLogButton` | Show/hide install log (install phase only) |
| `Step0Panel` | Welcome |
| `WelcomeTitleLabel`, `WelcomeSubtitleLabel` | |
| `SetupButton`, `FastSetupButton`, `FastSetupHintLabel` | |
| `DockerGatePanel` | Docker install prompt |
| `DockerTitleLabel`, `DockerDescriptionLabel`, `InstallDockerButton` | Opens guide via **`DockerService`** |
| `Step1Panel` | Display name |
| `DisplayNameTitleLabel`, `UsernameEntry` | **`SettingsService.TryValidateDisplayName`** |
| `Step2Panel` | Port ranges |
| `PortAllocationTitleLabel`, `OfficialPortsLabel`, `ThirdPartyPortsLabel` | |
| `OfficialPortEntry`, `ThirdPartyPortEntry`, `PortErrorLabel` | **`SettingsService.TryParsePorts`** |
| `Step3Panel` | Module list + install UI |
| `ModuleListScroll` / `ModuleList` | Dynamic checkboxes |
| `InstallPanel` | Progress + log |
| `InstallStatusLabel`, `OverallProgressLabel`, `InstallProgress` | |
| `DownloadDetailLabel`, `FileProgress` | Per-file download |
| `InstallLogConsole` | **`ConsoleOutputView`** → `InstallLogText`, `InstallLogSessionKey` |
| `ButtonPanel` | `BackButton`, `NextButton` |

---

## Constructor

#### `SetupWizardPage(AppDataStore, DockerService, EngineInstaller, ModuleInstaller, ModuleRunner, UpdateManager, AppLocalizationService, IServiceProvider)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Store services, `InitializeComponent()`, `BindingContext = this` |
| 2 | **`LocalizableAttach.Hook`** |
| 3 | Seed **`UsernameEntry`** from saved name or **`Environment.UserName`** |
| 4 | Seed port entries from **`AppDataStore.Data.Ports`** |
| 5 | `Loaded += OnLoaded` |

---

## Member reference

#### `private async void OnLoaded(object? sender, EventArgs e)`

**Purpose:** Once: **`PopulateModuleListAsync()`**, set **`_skipDockerStep`** from **`DockerService.IsCliInstalledAsync()`**.

---

#### `private void OnSetupClicked(object? sender, EventArgs e)`

**Purpose:** **`_pendingFastSetup = false`**, **`_currentStep = 1`**, **`_showDockerGate = !_skipDockerStep`**, **`UpdateStepUI()`**.

---

#### `private async void OnFastSetupClicked(object? sender, EventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Save default user name and **`AppPortConfig`** ports |
| 2 | **`await _appData.SaveAsync()`** |
| 3 | **`_pendingFastSetup = true`** — jump to step 3 or Docker gate |
| 4 | **`UpdateStepUI()`** |

---

#### `private async void OnDockerOpenGuideClicked(object? sender, EventArgs e)`

**Purpose:** **`await _dockerService.OpenInstallGuideAsync()`**; alert on failure.

---

#### `private async Task PopulateModuleListAsync()`

**Purpose:** **`DiscoverModulesAsync`** → checkbox rows in **`ModuleList`**; error label on failure; empty-state label when no modules.

---

#### `private void OnBackClicked(object? sender, EventArgs e)`

**Purpose:** From Docker gate on step 1 → step 0. Else decrement step if **`_currentStep > 1`**, **`UpdateStepUI()`**.

---

#### `private async void OnNextClicked(object? sender, EventArgs e)`

| Case | Action |
| --- | --- |
| Step < 3 | Docker gate re-check, validate display name (step 1), validate ports (step 2), increment step |
| Step 3 | **`await StartInstallAsync()`** |

---

#### `public void ApplyLocalization()`

**Purpose:** Localizes all wizard labels/placeholders/buttons; **`UpdateStepUI()`**.

---

#### `private void UpdateStepUI()`

**Purpose:** Toggles **`Step0Panel`**, **`DockerGatePanel`**, **`Step1Panel`**, **`Step2Panel`**, **`Step3Panel`**, header/button visibility, **`NextButton`** text (`Install` on last step), **`StepLabel`** (including **`SetupWizard_StepFormat`**).

---

#### `private bool ValidatePorts()`

**Purpose:** **`SettingsService.TryParsePorts`**; **`ShowPortError`** on failure.

---

#### `private void ShowPortError(string message)`

**Purpose:** Sets **`PortErrorLabel`** visible with message.

---

#### `private void OnToggleLogClicked(object? sender, EventArgs e)`

**Purpose:** Toggles **`_logVisible`**, updates button text, **`OnPropertyChanged(IsInstallLogVisible)`**, **`QueueInstallLogLayoutRefresh()`**.

---

#### `private void QueueInstallLogLayoutRefresh()`

**Purpose:** Immediate **`RefreshInstallLogLayout()`** + delayed invalidation (16 ms / 48 ms) like **`ConsolesView`**.

---

#### `private void RefreshInstallLogLayout()`

**Purpose:** **`InstallPanel`**, **`InstallLogConsole`** **`InvalidateMeasure()`**.

---

#### `private async Task StartInstallAsync()

**Purpose:** Full install workflow.

| Phase | Action |
| --- | --- |
| Persist | User name + ports → **`SaveAsync`** |
| Select | Checked modules; if none → **`FinishSetupAsync`** |
| UI mode | Hide module list, show **`InstallPanel`**, new **`_installLogSessionKey`**, clear log |
| Plan | Count engine + module steps; **`ResetDownloadMetrics`** |
| Engines | **`DiscoverEngines`** → **`InstallAsync`** per required engine |
| Modules | Update pipeline (`ShouldUseConfiguredUpdateInstall`) or download + **`ExecuteFirstRunAsync`** |
| End | Success → **`ConfigureFinishButton`**; failure → **`ConfigureRetryAndSkipButtons`** |

Uses **`InlineProgress<string>`** for logs and **`Progress<DownloadProgress>`** for bars.

---

#### `private void ResetNavigationButtons()`

**Purpose:** Restores default **`OnNextClicked`** / **`OnBackClicked`** handlers and button colors.

---

#### `private void ConfigureFinishButton()`

**Purpose:** **`NextButton`** → **`OnFinishClicked`**, text **`SetupWizard_Finish`**, hide back.

---

#### `private void ConfigureRetryAndSkipButtons()`

**Purpose:** **`NextButton`** → **`OnRetryInstallClicked`**; **`BackButton`** → **`OnSkipClicked`** (“Skip”).

---

#### `private async void OnRetryInstallClicked(object? sender, EventArgs e)`

**Purpose:** Hide buttons → **`await StartInstallAsync()`**.

---

#### `private async void OnFinishClicked(object? sender, EventArgs e)`

**Purpose:** **`await FinishSetupAsync()`**.

---

#### `private async void OnSkipClicked(object? sender, EventArgs e)`

**Purpose:** **`await FinishSetupAsync()`** after failed install.

---

#### `private void UpdateInstallStatus(string message)`

**Purpose:** UI thread: **`InstallStatusLabel.Text = message`**.

---

#### `private void UpdateOverallProgress(int completed, int total)`

**Purpose:** UI thread: **`InstallProgress.Progress = completed / total`**.

---

#### `private void ResetFileProgress()`

**Purpose:** Clears **`FileProgress`** and **`DownloadDetailLabel`**.

---

#### `private void ResetDownloadMetrics()`

**Purpose:** Zeros speed rolling counters.

---

#### `private static bool ShouldUseConfiguredUpdateInstall(ModuleConfig module)`

**Purpose:** Manifest update + GitHub **`source`** with repo.

---

#### `private static int GetModuleInstallStepCount(ModuleConfig module)`

**Purpose:** `1` (update pipeline) or `2` (download + first-run).

---

#### `private async Task<bool> InstallModuleFromUpdateConfigAsync(...)`

**Purpose:** **`ResolveModuleInstallCandidateAsync`** → **`ApplyModuleUpdateAsync`**.

---

#### `private static string FormatBytes(long bytes)`

**Purpose:** Human-readable B / KB / MB / GB.

---

#### `private async Task FinishSetupAsync()`

**Purpose:** **`FirstRunCompleted = true`**, **`SaveAsync`**, **`Window.Page = AppShellPage`**, **`SyncFlowDirection`**.

---

#### `private void AddLog(string message)`

**Purpose:** Append to **`_logBuffer`**; queue **`FlushLogAsync`** (~75 ms batching).

---

#### `private async Task FlushLogAsync()`

**Purpose:** Batched **`OnPropertyChanged(InstallLogText)`** + layout refresh while **`_logFlushRequested`** set.

---

#### `protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** Install-log INPC.

---

#### `private static Color GetColorResource(string key, Color fallback)`

**Purpose:** Resolves dynamic resource or fallback.

---

### Bindable install log properties

| Property | Description |
| --- | --- |
| `IsInstallLogVisible` | Log panel expanded |
| `InstallLogText` | Thread-safe copy of **`_logBuffer`** |
| `InstallLogSessionKey` | `setup-install-{ticks}` per run |

---

### Nested `InlineProgress<T>`

#### `void Report(T value)`

**Purpose:** Invokes handler synchronously on producer thread.

---

## Dependencies

`AppDataStore`, `DockerService`, `EngineInstaller`, `ModuleInstaller`, `ModuleRunner`, `UpdateManager`, `AppLocalizationService`, `IServiceProvider`.
