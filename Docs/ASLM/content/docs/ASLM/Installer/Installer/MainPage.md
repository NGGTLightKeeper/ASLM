---
title: "MainPage"
draft: false
---

## Class `MainPage`

`Installer/Installer/MainPage.xaml` + `MainPage.xaml.cs` — **`public partial ContentPage`** — installer wizard (legal → path → confirm → install).

Constructs its own **`InstallerService`** / **`LegalDocumentService`** instances (also registered in [MauiProgram](MauiProgram/) for DI elsewhere).

---

### Fields

| Field | Type | Role |
| --- | --- | --- |
| `_installerService` | `InstallerService` | Path validation, install, launch |
| `_legalDocumentService` | `LegalDocumentService` | Loads `legal-documents.json` |
| `_installCancellation` | `CancellationTokenSource` | Passed to `InstallAsync` |
| `_acceptedLegalDocumentIds` | `HashSet<string>` | Accepted legal doc IDs (ordinal ignore case) |
| `_legalDocuments` | `IReadOnlyList<LegalDocument>` | Loaded once on appear |
| `_manifest` | `InstallManifest?` | Set after successful install |
| `_step` | `int` | Current wizard index (0 = welcome) |
| `_isInstalling` | `bool` | Install in progress |
| `_isInstalled` | `bool` | Install completed successfully |
| `_isRendering` | `bool` | Suppresses checkbox handler during `RenderStep` |
| `_showLegalRequired` | `bool` | Show red legal validation when Next blocked |

---

## Constructor

#### `public MainPage()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `InitializeComponent()` |
| 2 | `BasePathEntry.Text = _installerService.GetDefaultInstallBasePath()` |
| 3 | `UpdatePathPreview()` |

---

## Lifecycle

#### `protected override async void OnAppearing()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `base.OnAppearing()` |
| 2 | `await LoadLegalDocumentsAsync()` |
| 3 | `RenderStep()` |

---

#### `private async Task LoadLegalDocumentsAsync()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Return if `_legalDocuments.Count > 0` |
| 2 | `_legalDocuments = await _legalDocumentService.LoadAsync()` |
| 3 | On exception → `FooterLabel.Text = ex.Message` |

---

## Navigation handlers

#### `private async void OnNextClicked(object? sender, EventArgs e)`

| Condition | Action |
| --- | --- |
| `_isInstalling` | Return (ignore) |
| `_isInstalled` | `CloseOrLaunch()` |
| Legal step, not accepted | `_showLegalRequired = true`, footer message, `RenderLegalDocumentValidation()` |
| `PathStep`, invalid path | Footer = validation message |
| `ConfirmStep` | `_step = InstallStep`, `RenderStep()`, `await StartInstallAsync()` |
| Else | `_step = Min(_step + 1, InstallStep)`, `RenderStep()` |

Clears `FooterLabel` at start (except legal-required path).

---

#### `private void OnDeclineClicked(object? sender, EventArgs e)`

**Purpose:** `Application.Current?.Quit()` — user declined legal terms.

---

#### `private void OnBackClicked(object? sender, EventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Return if `_isInstalling` or `_isInstalled` |
| 2 | Clear footer |
| 3 | `_step = Max(_step - 1, 0)` |
| 4 | `RenderStep()` |

---

## Input handlers

#### `private void OnAcceptLegalChanged(object? sender, CheckedChangedEventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Return if `_isRendering` or not legal step |
| 2 | Get `GetCurrentLegalDocument()`; return if null |
| 3 | If checked → add ID to `_acceptedLegalDocumentIds`, clear `_showLegalRequired`; else remove ID |
| 4 | On legal step: clear footer if not showing required; `RenderLegalDocumentValidation()` |

---

#### `private async void OnBrowseClicked(object? sender, EventArgs e)`

**Purpose:** **Windows:** `WindowsFolderPicker.PickFolder(window, "Select installation directory")` → updates `BasePathEntry`; errors in footer.

**Non-Windows:** `DisplayAlert` that browsing is Windows-only.

---

#### `private void OnInstallPathChanged(object? sender, TextChangedEventArgs e)`

**Purpose:** Calls **`UpdatePathPreview()`**.

---

## Installation

#### `private async Task StartInstallAsync()`

| Phase | Action |
| --- | --- |
| Start | `_isInstalling = true`; disable Back/Next |
| Run | `CreateInstallOptions()` → `InstallerService.InstallAsync` with `Progress<InstallProgress>` → status label + progress bar |
| Success | `_manifest` set; `_isInstalled = true`; show launch panel; Next = "Finish"; progress 100% |
| Failure | Footer error; Next = "Retry"; Back enabled; `_step = ConfirmStep` |
| Finally | `_isInstalling = false`; `RenderStep()` |

---

#### `private InstallOptions CreateInstallOptions()`

**Purpose:** Builds **`InstallOptions`**:

| Field | Source |
| --- | --- |
| Base path | `BasePathEntry.Text` |
| Folder name | `FolderNameEntry.Text` or `"ASLM"` |
| Version | `"1.0"` (fixed) |
| Accepted docs | Legal docs whose IDs are in `_acceptedLegalDocumentIds` → **`AcceptedLegalDocument`** (id, title, fileName, sha256, `acceptedAtUtc`) |
| Shortcuts | `DesktopShortcutSwitch`, `StartMenuShortcutSwitch` |

---

## Path validation

#### `private InstallPathValidation ValidateCurrentInstallPath()`

**Purpose:** Delegates to **`_installerService.ValidateInstallPath(BasePathEntry, FolderNameEntry)`**.

---

#### `private void UpdatePathPreview()`

| Valid | Invalid |
| --- | --- |
| `FinalPathLabel` = full install path; clear `PathValidationLabel` | Combined path preview if both fields non-empty; `PathValidationLabel` = error message |

---

## Wizard rendering

#### `private void RenderStep()`

**Purpose:** Sets `_isRendering = true`, then:

| Area | Logic |
| --- | --- |
| Panels | `WelcomeView` (0), `LegalView` (legal steps), `PathView`, `ConfirmView`, `InstallView` |
| Buttons | Decline visible on legal; Back if `_step > 0` and not installed; Next enabled unless installing |
| `StepLabel` | `Step {_step+1} of {TotalStepCount}` |
| Copy | Per-step titles/subtitles and Next button text (see source `if` chain) |

Calls **`RenderLegalDocumentStep`**, **`UpdatePathPreview`**, or **`UpdateConfirmation`** on relevant steps. Clears `_isRendering` at end.

---

#### `private void RenderLegalDocumentStep()`

| UI | Value |
| --- | --- |
| Title / subtitle | Document title; `Legal document {n} of {count}` |
| `LegalDocumentEditor` | `document.Markdown` |
| Checkbox | Synced from `_acceptedLegalDocumentIds` |

Then **`RenderLegalDocumentValidation()`**.

---

#### `private void RenderLegalDocumentValidation()`

**Purpose:** If `_showLegalRequired` and not accepted → red (`SystemRed`) on label + border; else default label/separator colors.

---

#### `private void UpdateConfirmation()`

**Purpose:** `ConfirmPathLabel` = install path or validation error from **`ValidateCurrentInstallPath()`**.

---

## Wizard step helpers

#### `private bool IsLegalStep(int step)`

**Purpose:** `step >= 1 && step < PathStep`.

---

#### `private int CurrentLegalDocumentIndex`

**Purpose:** `_step - 1` (zero-based index into `_legalDocuments`).

---

#### `private int PathStep`

**Purpose:** `1 + _legalDocuments.Count`.

---

#### `private int ConfirmStep`

**Purpose:** `PathStep + 1`.

---

#### `private int InstallStep`

**Purpose:** `PathStep + 2`.

---

#### `private int TotalStepCount`

**Purpose:** `InstallStep + 1`.

---

## Legal document state

#### `private LegalDocument? GetCurrentLegalDocument()`

**Purpose:** Returns `_legalDocuments[CurrentLegalDocumentIndex]` if in range, else 
ull`.

---

#### `private bool IsCurrentLegalDocumentAccepted()`

**Purpose:** Current document non-null and ID in `_acceptedLegalDocumentIds`.

---

## Completion

#### `private void CloseOrLaunch()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | If `_manifest` and `LaunchAfterInstallCheckBox` checked → `_installerService.Launch(_manifest.InstallPath)`; on error show footer and return |
| 2 | `Application.Current?.Quit()` |

---

## XAML (`MainPage.xaml`)

Three-row grid: header (title, subtitle, step), stacked step views (`WelcomeView`, `LegalView`, `PathView`, `ConfirmView`, `InstallView`), footer + Back / Decline / Next. Uses `App.xaml` theme brushes.

---

## Related

- [InstallerService](Services/InstallerService/)
- [LegalDocumentService](Services/LegalDocumentService/)
- [WindowsFolderPicker](Platforms/Windows/WindowsFolderPicker/)
