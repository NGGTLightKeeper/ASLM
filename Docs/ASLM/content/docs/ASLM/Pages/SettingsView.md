---
title: "SettingsView"
draft: false
---

## Class `SettingsView`

`ASLM/Pages/SettingsView.xaml.cs` — full-screen settings overlay (dimmed shell). ASLM preferences, personalization, Ollama account, app updates, and dynamic per-module settings from each [ModuleConfig](../Models/ModuleConfig/). Implements **`ILocalizable`**.

Raises **`CloseRequested`** when the user dismisses the dialog.

Category kinds (via `SettingsService`): **`Aslm`**, **`AslmProfile`**, **`Updates`**, **`Ollama`**, **`Personalization`**, **`Module`**.

---

### Constants

| Name | Value |
| --- | --- |
| `PasswordIconHidden` / `PasswordIconVisible` | `icon_password_off.png` / `icon_password_on.png` |
| `DialogWidthFactor` / `DialogHeightFactor` | `0.8` |
| `MinDialogWidth` × `MinDialogHeight` | `960` × `540` |
| `MaxDialogWidth` × `MaxDialogHeight` | `1280` × `720` |
| `OllamaSignInPollInterval` | 3 seconds |
| `OllamaSignInPollDuration` | 5 minutes |
| `PersonalizationPickerMaxWidth` | `256` |
| `TitleDescriptionSpacing` | `8` |
| `AppearanceOptions` | `Dark`, `Light`, `System`, `Custom` |

Style resource keys: `FooterButtonStyleKey`, `FooterPrimaryButtonStyleKey`, `FooterDangerButtonStyleKey`, `SelectorHeaderLabelStyleKey`, `SelectorButtonBorderStyleKey`, `SelectorButtonLabelStyleKey`, `TransparentBorderStyleKey`, `FieldBorderStyleKey`, `TextEntryStyleKey`, `PickerStyleKey`, `SubGroupHeaderLabelStyleKey`, `CardTitleLabelStyleKey`, `CardDescriptionLabelStyleKey`, `SecondaryLabelStyleKey`, `InlineActionButtonStyleKey`, `PasswordToggleImageStyleKey`.

---

### Fields

| Name | Description |
| --- | --- |
| `_appData`, `_settingsService`, `_localization`, `_ollamaSettings`, `_updateManager`, `_apiServer`, `_notifications`, `_themeService`, `_customThemesStore` | Injected services |
| `_settingMappings` | UI control ↔ `ModuleSetting` bindings |
| `_settingBaselines` | Per-key baselines for dirty detection |
| `_categoryButtons` | Sidebar selector `Border` by category id |
| `_runtimeLoadedModuleIds` | Modules with runtime `getExec` loaded |
| `_loadedModules`, `_categories`, `_activeCategory` | Discovery and navigation |
| `_aslmBaseline`, `_consoleBaseline`, `_updateBaseline` | Saved ASLM snapshots |
| `_consoleDraft`, `_updateDraft`, `_ollamaDraft` | In-memory edits |
| `_userNameDraft`, `_officialPortDraft`, `_thirdPartyPortDraft`, `_apiServerEnabledDraft` | ASLM/account drafts |
| `_personalizationDraft`, `_personalizationBaseline`, `_editingThemeDraft` | Theme/language drafts |
| `_hasLoaded`, `_isRefreshingVisibility`, `_isSwitchingCategory`, `_isSaving` | Lifecycle guards |
| `_isOllamaAccountActionRunning`, `_isOllamaMetadataRefreshRunning`, `_ollamaAccountAction` | Ollama UI state |
| `_actionButtonUpdateQueued` | Coalesced footer button refresh |
| `_ollamaAccountButton`, `_ollamaAccountStatusLabel` | Dynamic Ollama card controls |
| `_checkUpdatesToggle` … `_restartAppUpdateButton`, `_pendingAppUpdateCandidate` | Updates category controls |
| `_apiServerToggle`, `_consoleSidebarToggle`, `_consoleCompletedToggle`, `_consoleIndividualToggle` | ASLM built-in toggles |
| `_ollamaMetadataRefreshCts`, `_ollamaStatusPollingCts` | Background Ollama work |
| `_settingsReconcileTimerStarted`, `_settingsReconcileTimerStopRequested` | WinUI reconcile timer |
| `_appearancePicker`, `_languagePicker`, `_customThemeSection`, `_customThemePicker`, `_themeEditorSection` | Personalization UI |
| `_suppressCustomThemePickerEvents` | Prevents recursive picker handling |

---

### XAML elements (`SettingsView.xaml`)

| Name | Role |
| --- | --- |
| `SettingsDialog` | Root bordered dialog; `OnViewSizeChanged` sizing |
| `SettingsSidebarTitleLabel` | Sidebar header |
| `CategoryScroll` / `CategoryPanel` | Left category list (dynamic selector buttons) |
| `ActiveCategoryTitleLabel` | Current section title |
| `CloseSettingsButton` | `OnCloseClicked` → `RequestClose` |
| `SettingsScroll` / `SettingsContentContainer` | Right pane scroll host |
| `AslmSettingsContainer` | Profile + ports (account/ASLM categories) |
| `UserProfileSection` | `UsernameEntry` |
| `PortsSection` | `ModulePortEntry`, `PortErrorLabel` |
| `ModuleSettingsContainer` | Dynamic module / updates / personalization / Ollama content |
| `EmptyCategoryState` / `EmptyCategoryLabel` | No settings placeholder |
| `DefaultButton` | `OnDefaultClicked` |
| `DiscardButton` | `OnDiscardChangesClicked` |
| `SaveButton` | `OnSaveClicked` |
| `SaveAndRestartButton` | `OnSaveAndRestartClicked` |

Backdrop tap → `OnBackgroundTapped`; dialog border → `OnBorderTapped` (swallows close).

---

### Record `SettingControlMapping`

| Member | Description |
| --- | --- |
| `Module`, `Setting` | Source manifest and setting definition |
| `ReadValue`, `ReadCustomValue` | Live control readers |
| `InitialDisplayValue`, `InitialUseCustomValue` | Baseline for dirty checks |

---

### Event

#### `public event EventHandler? CloseRequested`

**Purpose:** Shell hides overlay and refreshes dependent UI when raised from `RequestClose`.

---

## Nested class `CompactToggle`

Lightweight switch used instead of native `Switch` for consistent sizing.

### Constants (nested)

| Name | Value |
| --- | --- |
| `TrackWidth` × `TrackHeight` | `36` × `20` |
| `ThumbSize` | `16` |
| `ThumbInset` | `2` |

#### `public void Draw(ICanvas canvas, RectF dirtyRect)` (`ThumbDrawable`)

**Purpose:** Draws circular thumb fill.

**Steps:** Enable antialias; fill circle with `ToggleThumbColor`.

#### `public CompactToggle(bool isToggled = false)`

**Purpose:** Builds track, thumb, tap and pan gestures.

**Steps:** Create `Border` track and `GraphicsView` thumb; wire tap toggle and `OnPanUpdated`; `UpdateVisualState`.

#### `public void SetStateWithoutToggleEvent(bool isToggled)`

**Purpose:** Updates state without firing `Toggled`.

**Steps:** Set `_isToggled`; `UpdateVisualState`.

#### `private void UpdateVisualState()`

**Purpose:** Applies on/off track color and thumb offset.

#### `private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)`

**Purpose:** Drag thumb; snap to on/off at end of pan.

#### `private void SetThumbOffset(double offset)`

**Purpose:** Positions thumb via `AbsoluteLayout.SetLayoutBounds`.

#### `private static double ClampOffset(double offset)`

**Purpose:** Clamps thumb X to `[0, MaxThumbOffset]`.

#### `private static double MaxThumbOffset` / `private static double ThumbTopOffset`

**Purpose:** Computed layout metrics for thumb travel and vertical centering.

#### `private double CurrentThumbOffset`

**Purpose:** Reads current thumb X minus inset.

---

## Public methods — `SettingsView`

#### `public SettingsView(AppDataStore, SettingsService, AppLocalizationService, OllamaSettingsStore, UpdateManager, AslmApiServer, NotificationCenter, ThemeService, CustomThemesStore)`

**Purpose:** Constructs overlay; hooks localization, entries, scroll chrome, size/load.

**Steps:**

1. Store services; `InitializeComponent`; `LocalizableAttach.Hook`.
2. `ApplyFlatEntryStyle` on port/username entries; `ApplyScrollViewChrome` on scroll views.
3. Wire `TextChanged` on entries → `QueueActionButtonUpdate`; `SizeChanged`, `Loaded`, `Unloaded`.

---

#### `public async Task RefreshAsync()`

**Purpose:** Reloads settings when shell reopens overlay (after first load).

**Steps:**

1. No-op if not `_hasLoaded`.
2. `LoadSettingsAsync` with exception logged.

---

#### `public void ApplyLocalization()`

**Purpose:** Localizes static labels, rebuilds selectors, re-renders active category.

**Steps:**

1. Set sidebar, profile, ports, footer labels; `UpdateActionButtons`.
2. `BuildCategorySelectors` if categories exist.
3. `ActivateCategory(_activeCategory)` when set.

---

## Private methods — overlay and loading

#### `private void OnBackgroundTapped(object? sender, EventArgs e)`

**Purpose:** Tap outside dialog closes overlay.

**Steps:** `RequestClose()`.

---

#### `private void OnBorderTapped(object? sender, EventArgs e)`

**Purpose:** Swallows taps on dialog surface.

**Steps:** Intentionally empty.

---

#### `private void OnCloseClicked(object? sender, EventArgs e)`

**Purpose:** Close button handler.

**Steps:** `RequestClose()`.

---

#### `private async void RequestClose()`

**Purpose:** Confirms discard, stops Ollama work, raises `CloseRequested`.

**Steps:**

1. `ConfirmDiscardChangesIfNeededAsync` — abort if user stays.
2. Stop polling/metadata refresh; `_ollamaSettings.StopManagedRuntime()`.
3. Invoke `CloseRequested`.

---

#### `private async Task LoadSettingsAsync()`

**Purpose:** Full reload of drafts, modules, categories, active category.

**Steps:**

1. Remember `previousCategoryId`; load ASLM, personalization, Ollama, module drafts.
2. `CreateOrderedCategories`; resolve target or empty state.
3. `BuildCategorySelectors`; `ActivateCategory`.

---

#### `private async void OnLoaded(object? sender, EventArgs e)`

**Purpose:** First load or restart reconcile timer on revisit.

**Steps:**

1. If already loaded → `StartSettingsReconcileTimer` only.
2. Else `_hasLoaded = true`, `UpdateDialogSize`, `LoadSettingsAsync`, start timer.

---

#### `private void OnUnloaded(object? sender, EventArgs e)`

**Purpose:** Stops timers and Ollama background work.

**Steps:** `StopSettingsReconcileTimer`; stop polling/metadata; `StopManagedRuntime`.

---

#### `private void OnViewSizeChanged(object? sender, EventArgs e)`

**Purpose:** Resizes dialog on shell resize.

**Steps:** `UpdateDialogSize()`.

---

#### `private void UpdateDialogSize()`

**Purpose:** Sets `SettingsDialog` width/height from factors and clamps.

**Steps:** Compute `Width * 0.8` and `Height * 0.8` with min/max via `ClampDialogSize`.

---

#### `private static double ClampDialogSize(double value, double min, double max)`

**Purpose:** `Max(min, Min(max, value))`.

---

## Localization helpers

#### `private static string GetLocalizedCategoryTitle(SettingsCategory category)`

**Purpose:** Maps `SettingsCategoryKind` (+ module name) to localized sidebar title.

---

#### `private static string BuildLocalizedSaveMessage(bool hadAnyPersistedSettingsChanges, bool touchedModules, IReadOnlyList<string> deferredSettings)`

**Purpose:** Toast body after save (none / deferred list / saved).

---

#### `private static string GetLanguageDisplayName(string languageId)`

**Purpose:** Delegates to `AppLocalizationService.GetPickerDisplayName`.

---

#### `private static string GetAppearanceDisplayName(string appearance)`

**Purpose:** Localized picker label for Dark/Light/System/Custom.

---

#### `private static string ResolveAppearanceFromDisplayName(string? displayName)`

**Purpose:** Reverse maps display label to canonical appearance id.

**Steps:** Match against `AppearanceOptions` display names; default `Dark`.

---

## Draft loading

#### `private void LoadPersonalizationDraftsFromAppData()`

**Purpose:** Copies `AppData.Personalization` to draft and baseline.

---

#### `private void LoadAslmDraftsFromAppData()`

**Purpose:** Loads account, ports, API, console, update baselines from `SettingsService.BuildAslmDraftSnapshot`.

---

#### `private void LoadOllamaDraftsFromService()`

**Purpose:** Loads persisted Ollama settings into `_ollamaDraft`.

---

#### `private async Task LoadModuleDraftsAsync(bool reloadModules, bool reloadRuntimeValues)`

**Purpose:** Discovers installed modules and optional runtime values. It filters out ineligible modules using `SettingsService.IsModuleEligibleForSettings`.

**Steps:**

1. Optionally reload module list from disk.
2. Build baselines; optionally `ReloadModuleRuntimeValuesAsync` per module.

---

## Category UI

#### `private void BuildCategorySelectors()`

**Purpose:** Rebuilds left sidebar buttons from `_categories`.

**Steps:** Clear `CategoryPanel`; header + selector button per category with `BindingContext`.

---

#### `private static Label CreateSelectorHeader(string text)`

**Purpose:** Section header label in sidebar.

---

#### `private static Border CreateSelectorButton(string text)`

**Purpose:** Tappable category row with localized title binding.

---

#### `private async void OnCategorySelectorClicked(object? sender, EventArgs e)`

**Purpose:** Routes sidebar click to `TrySelectCategoryAsync`.

---

#### `private async Task TrySelectCategoryAsync(SettingsCategory category)`

**Purpose:** Switches category with draft sync and theme restore when leaving personalization.

**Steps:**

1. Guard saving/switching/same category.
2. `SyncDraftValuesFromControls`; resolve category.
3. If leaving personalization → `_themeService.ApplyFromSettings()`.
4. `ActivateCategory`.

---

#### `private void ActivateCategory(SettingsCategory category)`

**Purpose:** Sets active category and renders matching panel.

**Steps:**

1. Update title; `switch` on `Kind` → `Render*` methods.
2. Module kind also starts `RefreshActiveModuleRuntimeValuesAsync`.
3. `UpdateSelectorButtonStates`; `UpdateActionButtons`.

---

#### `private async Task RefreshActiveModuleRuntimeValuesAsync(SettingsCategory category)`

**Purpose:** One-time runtime `getExec` load per module when module category shown.

**Steps:** Skip if not module or already in `_runtimeLoadedModuleIds`; `ReloadModuleRuntimeValuesAsync`; re-render if still active.

---

#### `private async Task ReloadModuleRuntimeValuesAsync(ModuleConfig module)`

**Purpose:** Executes runtime readers and refreshes control values.

---

#### `private SettingsCategory? ResolveCategory(string? categoryId)`

**Purpose:** Finds category by id in `_categories`.

---

#### `private void UpdateSelectorButtonStates()`

**Purpose:** Applies active/inactive chrome to all `_categoryButtons`.

---

#### `private void RefreshCategorySelectorChromeFromResources()`

**Purpose:** Re-applies selector colors after theme preview.

---

#### `private static void ApplySelectorButtonState(Border button, bool isActive)`

**Purpose:** Sets selector background and opacity for active row.

---

## Footer buttons and reconcile timer

#### `private void QueueActionButtonUpdate()`

**Purpose:** Debounces `UpdateActionButtons` by 50 ms on dispatcher.

---

#### `private void StartSettingsReconcileTimer()` / `private void StopSettingsReconcileTimer()`

**Purpose:** Starts/stops 2 s dispatcher timer for WinUI draft reconciliation.

---

#### `private bool OnSettingsReconcileTimerTick()`

**Purpose:** Timer callback; returns false to stop when unload requested.

**Steps:** Call `ReconcileSettingsVisibleControlsWithDrafts` when loaded and not saving.

---

#### `private void ReconcileSettingsVisibleControlsWithDrafts()`

**Purpose:** Syncs visible toggles/entries into drafts and refreshes footer.

**Steps:** On ASLM category refresh API/console drafts from toggles; `SyncDraftValuesFromControls`; `UpdateActionButtons`.

---

#### `private void UpdateActionButtons()`

**Purpose:** Enables/disables Save, Discard, Default, Save+Restart from dirty/restart state.

**Steps:** Compute `HasUnsavedChanges`, `HasPendingRestartChanges`; apply primary/danger styles; Ollama hides Default.

---

#### `private bool HasPendingRestartChanges()`

**Purpose:** Checks whether any pending edit has a restart path, regardless of the visible category.

---

#### `private List<ModuleConfig> GetModulesWithUnsavedChanges()`

**Purpose:** Modules differing from baselines.

---

#### `private static bool CanRestartModule(ModuleConfig module)`

**Purpose:** Module enabled and has commands.

---

#### `private static void ApplyActionButtonState(Button button, bool isPrimary, bool isDanger = false)`

**Purpose:** Applies footer style keys and enabled opacity.

---

## Category rendering

#### `private void RenderAslmCategory()`

**Purpose:** ASLM combined page: API + console toggles (not profile/ports).

**Steps:** Apply drafts; clear mappings; show ports hidden; build API/console cards in `ModuleSettingsContainer`.

---

#### `private void RenderAccountCategory()`

**Purpose:** Profile only — username visible, ports hidden.

---

#### `private void RenderUpdatesCategory()`

**Purpose:** Update preferences + manual check/prepare/restart cards.

**Steps:** `AddUpdateSettings` in module container.

---

#### `private void AddAslmApiSettings(Layout content)`

**Purpose:** API server compact toggle card.

---

#### `private void AddConsoleSettings(Layout content)`

**Purpose:** Sidebar, completed sessions, individual console toggles.

---

#### `private void AddUpdateSettings(Layout content)`

**Purpose:** Check/auto toggles, period entry, channel pickers, manual update card.

---

#### `private void RenderOllamaCategory()`

**Purpose:** Ollama account card; starts metadata refresh.

---

#### `private static Border CreateUpdateCard(string title, string description, View control)`

**Purpose:** Standard settings card layout (title, description, control).

---

#### `private Border CreateManualUpdateCard()`

**Purpose:** Check now / prepare / restart app update UI.

---

#### `private string BuildInitialManualUpdateStatusText()`

**Purpose:** Status when updates category opens (pending restart hint).

---

#### `private string BuildManualUpdateCheckStatusMessage(IReadOnlyList<UpdateCandidate> updates)`

**Purpose:** Summarizes check results for app and modules.

---

#### `private async void OnCheckUpdatesNowClicked(object? sender, EventArgs e)`

**Purpose:** `CheckAllUpdatesAsync`; shows prepare button when app update available.

---

#### `private async void OnPrepareAppUpdateClicked(object? sender, EventArgs e)`

**Purpose:** `PrepareAppUpdateAsync` for `_pendingAppUpdateCandidate`.

---

#### `private async void OnRestartNowClicked(object? sender, EventArgs e)`

**Purpose:** Stops modules, starts launcher for patcher, quits app.

---

#### `private Border CreateOllamaAccountCard()`

**Purpose:** Status label + sign-in/out button.

---

#### `private void RenderModuleCategory(ModuleConfig module)`

**Purpose:** Dynamic setting cards from manifest with visibility rules.

**Steps:** Filter settings; `CreateSettingCard` each; populate `_settingMappings`.

---

#### `private void ShowEmptyCategory(string message)`

**Purpose:** Empty state only — clears dynamic refs.

---

#### `private void RenderPersonalizationCategory()`

**Purpose:** Language, appearance, custom theme list, color editor.

---

## Draft sync and dirty detection

#### `private void ApplyAslmDraftsToControls()`

**Purpose:** Pushes username/port drafts into entries.

---

#### `private void ApplyAslmBuiltInDraftsToToggles()`

**Purpose:** Sets API/console toggle states without extra events.

---

#### `private void RefreshAslmApiAndConsoleDraftsFromToggles()`

**Purpose:** Reads compact toggles into `_apiServerEnabledDraft` and `_consoleDraft`.

---

#### `private void SyncDraftValuesFromControls()`

**Purpose:** Routes to ASLM or skips personalization (handled in handlers).

---

#### `private void SyncAslmDraftValuesFromControls()`

**Purpose:** Username and port entries → drafts when sections visible.

---

#### `private void SyncBuiltInDraftValuesFromControls()`

**Purpose:** `_updateDraft = GetCurrentUpdateDraft()`.

---

#### `private void SyncModuleDraftValuesFromControls()`

**Purpose:** Each mapping `ReadValue` → `setting.Value` with auto-managed rules.

---

#### `private async Task RefreshDynamicVisibilityAsync()`

**Purpose:** Re-renders module category when bool setting affects dependents.

---

#### `private async Task<bool> ConfirmDiscardChangesIfNeededAsync()`

**Purpose:** Alert if any unsaved changes; reloads drafts on discard.

---

#### `private bool HasUnsavedChanges()`

**Purpose:** Dirty check for active category only.

---

#### `private bool HasAnyUnsavedChanges()`

**Purpose:** Account, ASLM, personalization, or any module baseline diff.

---

#### `private bool HasUnsavedPersonalizationChanges()` / `HasUnsavedThemeColorChanges()`

**Purpose:** Appearance/language/theme id or in-editor color diff.

---

#### `private bool HasUnsavedAccountChanges()`

**Purpose:** Display name vs `_aslmBaseline`.

---

#### `private bool HasUnsavedAslmSettingsChanges()`

**Purpose:** Ports, API, consoles, updates vs baselines.

---

#### `private string GetCurrentPortStartDraft()`

**Purpose:** Reads ports from visible entries or cached drafts.

---

#### `private bool HasUnsavedPortChanges()` / `HasUnsavedAslmApiChanges()` / `HasUnsavedConsoleChanges()`

**Purpose:** Delegates to `SettingsService` comparers.

---

#### `private bool HasUnsavedModuleChanges()`

**Purpose:** Compares mapping initial vs current display values.

---

#### `private bool HasUnsavedUpdateChanges()`

**Purpose:** `GetCurrentUpdateDraft()` vs `_updateBaseline`.

---

#### `private UpdateBaseline GetCurrentUpdateDraft()`

**Purpose:** Builds draft from update toggles/entries when controls exist.

---

#### `private void ApplyUpdateDefaultsToControls()`

**Purpose:** Resets update UI to `SettingsService.BuildDefaultUpdateBaseline()`.

---

## Dialogs and save flow

#### `private static Task<bool> ShowAlertAsync(...)` / `private static Task ShowErrorAsync(string message)` / `private Task ShowSuccessAsync(string message)`

**Purpose:** `DisplayAlertAsync` / toast via `NotificationCenter`.

---

#### `private static Style? GetStyleResource(string key)` / `private static Color GetColorResource(string key, Color fallback)`

**Purpose:** Resource lookup with fallback.

---

#### `private async void OnDefaultClicked(object? sender, EventArgs e)`

**Purpose:** Resets active category to defaults (not Ollama).

**Steps:** Per `Kind` reset drafts and re-render; reconcile toggles on ASLM; scroll top.

---

#### `private async void OnDiscardChangesClicked` / `private async Task DiscardUnsavedChangesAsync()`

**Purpose:** Reverts drafts and reactivates same category id.

---

#### `private async void OnSaveClicked` / `OnSaveAndRestartClicked`

**Purpose:** `SaveAsync(false)` / `SaveAsync(true)`.

---

#### `private async Task SaveAsync(bool restartAfterSave)`

**Purpose:** Validates, persists app data, modules, personalization; optional restart.

**Steps:**

1. Validate display name, ports, updates, all modules.
2. `SavePersonalizationAsync` if needed; `ApplyDraftsToAppData`; `SaveAsync`.
3. Toggle API server; update baselines; save changed modules; rebuild categories.
4. Success toast; optionally restarts application via launcher if personalization changes exist, otherwise calls `RestartChangedTargetsAsync`.

---

#### `private async Task<bool> RestartChangedTargetsAsync(bool restartApp, IEnumerable<ModuleConfig> changedModules)`

**Purpose:** Full app restart or per-module `RestartModuleAsync`.

---

#### `private async Task RestartApplicationThroughLauncherAsync()`

**Purpose:** Stops modules, starts the launcher with process wait, and exits so ASLM relaunches cleanly.

---

#### `private async Task RestartApplicationAsync()`

**Purpose:** `StopAllModulesAsync`; replace window page with startup page.

---

## Ollama account

#### `private async void OnOllamaAccountButtonClicked` / `private async Task ExecuteOllamaAccountActionAsync(bool signIn)`

**Purpose:** Sign-in/out via `OllamaSettingsStore`; polling when pending verification.

---

#### `private async Task RefreshOllamaRuntimeMetadataAsync(bool queryLiveStatus, CancellationToken ct)`

**Purpose:** Loads/refreshes CLI metadata into draft.

---

#### `private void ApplyOllamaRuntimeMetadata(OllamaPersistentSettings refreshed)`

**Purpose:** Copies availability, signed-in, user name flags.

---

#### `private void UpdateOllamaAccountActionControls()`

**Purpose:** Status text and button label/enabled/colors.

---

#### `private void StartOllamaMetadataRefresh()` / `StopOllamaMetadataRefresh()` / `private async Task RefreshOllamaMetadataAsync(CancellationTokenSource refreshCts)`

**Purpose:** Background live status when Ollama category visible.

---

#### `private void ResetOllamaControlReferences()` / `ResetUpdateControlReferences()` / `ResetAslmApiControlReferences()` / `ResetRenderedControlReferences()`

**Purpose:** Clears dynamic control refs before rebuild.

---

#### `private void PrepareCategorySurface(bool showAslmContainer, bool showModuleContainer, bool showEmptyState)`

**Purpose:** Toggles container visibility and clears module children.

---

#### `private bool IsOllamaSignedIn()`

**Purpose:** `_ollamaDraft.IsSignedIn`.

---

#### `private void StartOllamaStatusPolling()` / `StopOllamaStatusPolling()` / `private async Task PollOllamaStatusAsync(CancellationTokenSource pollingCts)`

**Purpose:** Polls sign-in completion every 3 s up to 5 min.

---

#### `private string BuildOllamaAccountStatusText()`

**Purpose:** Localized status for CLI missing, waiting, signed in, etc.

---

#### `private void ShowPortError(string message)`

**Purpose:** Shows `PortErrorLabel` with validation message.

---

## Module setting editors

#### `private (View, SettingControlMapping?) CreateSettingCard(...)`

**Purpose:** Builds card for engine badge, bool row, or full editor.

---

#### `private (View, SettingControlMapping?) CreateEditor(...)`

**Purpose:** Dispatches managed, engine picker, allowed values, or type switch.

---

#### `private (View, SettingControlMapping?) CreateBooleanEditor(...)`

**Purpose:** Compact toggle with optional dependent visibility refresh.

---

#### `private (View, SettingControlMapping?) CreateActiveEngineEditor(...)`

**Purpose:** Picker for active LLM engine allowed values.

---

#### `private (View, SettingControlMapping?) CreatePickerEditor(...)`

**Purpose:** Generic allowed-values picker.

---

#### `private (View, SettingControlMapping?) CreateNumericEditor(...)` / `CreateTextEditor(...)` / `CreatePasswordEditor(...)`

**Purpose:** Text field mappings with appropriate keyboard/password UI.

---

#### `private (View, SettingControlMapping?) CreateManagedEditor(...)`

**Purpose:** Auto value + “use custom” toggle + entry.

---

#### `private Border CreateStatusBadge(string text, bool isPositive)`

**Purpose:** Read-only engine installed/not installed badge.

---

## Personalization and themes

#### `private void ResetPersonalizationControlReferences()` / `RebuildCustomThemeSection()` / `OnCustomThemePickerSelectionChanged` / `ApplyCustomThemeSelection`

**Purpose:** Custom theme picker lifecycle and editor visibility.

---

#### `private async void OnDeleteCurrentCustomThemeClicked` / `private async Task OnDeleteThemeClickedAsync(string themeId)`

**Purpose:** Confirmed theme delete and UI refresh.

---

#### `private void BuildThemeColorEditor(VerticalStackLayout container)` / `private View CreateCompactColorEditorRow(string key)`

**Purpose:** Two-column palette key editor with pick/clear.

---

#### `private static void UpdateColorSwatch(Border swatchFrame, string? hex)`

**Purpose:** Swatch fill and contrast stroke from hex or inherit placeholder.

---

#### `private async Task OpenThemeColorPickerForKeyAsync(string key, Border swatchFrame, Label hexLabel)`

**Purpose:** `ThemeColorPickerView.PickAsync` → updates `_editingThemeDraft` and preview.

---

#### `private static Color ResolveThemeEditorColor(string key, string? existingHex)`

**Purpose:** Initial color for picker from hex or resource key.

---

#### `private void OnAppearancePickerChanged` / `OnLanguagePickerChanged`

**Purpose:** Updates drafts; shows custom section; `ApplyPersonalization` preview.

---

#### `private async void OnCreateThemeClicked` / `OnImportThemeClicked` / `OnExportThemeClicked`

**Purpose:** Create (with inherit sheet), import JSON, export JSON (Windows save picker).

---

#### `private static string SanitizeThemeFileName(string name)`

**Purpose:** Safe file name for export.

---

#### `private async Task SavePersonalizationAsync(bool applyImmediately = true)`

**Purpose:** Persists the personalization draft to app data and optionally applies the new theme immediately.

---

#### `private static Task<string?> PromptAsync(...)` / `private static CustomTheme CloneCustomTheme(CustomTheme source)`

**Purpose:** Text prompt and deep copy for editor.

---

## Styling helpers

#### `private static Border CreateModuleSectionBorder()`

**Purpose:** Outer transparent border for a settings section.

**Steps:** Return `Border` with `TransparentBorderStyleKey`.

---

#### `private static Border CreateSettingCardBorder()`

**Purpose:** Card shell for one setting row.

**Steps:** Same as module section border style.

---

#### `private static Label CreateSubGroupHeader(string text)`

**Purpose:** Subsection header (Ports, API, Consoles, etc.).

**Steps:** Label with `SubGroupHeaderLabelStyleKey`.

---

#### `private static Label CreateCardTitle(string text)`

**Purpose:** Primary label on a setting card.

**Steps:** Apply `CardTitleLabelStyleKey`.

---

#### `private static Label CreateCardDescription(string text)`

**Purpose:** Description label under the card title.

**Steps:** Apply `CardDescriptionLabelStyleKey`.

---

#### `private static Label CreateSecondaryLabel(string text)`

**Purpose:** Inline helper label for toggle rows.

---

#### `private static CompactToggle CreateInlineToggle()`

**Purpose:** Off-state compact toggle for custom-value rows.

**Steps:** `CreateCompactToggle(false)`.

---

#### `private static Grid CreateInlineToggleRow(string text, CompactToggle toggle)`

**Purpose:** Label + right-aligned toggle in one row.

---

#### `private static Button CreateInlineActionButton(string text, EventHandler clicked)`

**Purpose:** Small themed button; wires `Clicked`.

---

#### `private static void ApplyOllamaAccountButtonState(Button button, bool isSignedIn)`

**Purpose:** Blue sign-in vs red sign-out background; respects `IsEnabled` opacity.

---

#### `private static Picker CreatePicker(string? title, double fontSize)`

**Purpose:** Picker with shared style and `ApplyCompactPickerStyle`.

---

#### `private static Border CreatePickerContainer(Picker picker, double? maximumWidth = null)`

**Purpose:** Field border wrapper; optional max width for personalization.

---

#### `private static Border CreateUpdatePickerContainer(Picker picker)`

**Purpose:** Fixed 132px-wide picker shell for Updates category.

---

#### `private static Border CreateFieldContainer(View content, Thickness? padding = null)`

**Purpose:** Shared field border around entries or password grid.

---

#### `private static CompactToggle CreateCompactToggle(bool isToggled)`

**Purpose:** Factory for nested `CompactToggle` class.

---

#### `private static void ApplyCompactPickerStyle(Picker picker)`

**Purpose:** Removes WinUI `ComboBox` border/background on handler changed.

---

#### `private static void ApplyScrollViewChrome(ScrollView scrollView, bool isSidebar)`

**Purpose:** Transparent WinUI `ScrollViewer`; hides main pane scrollbars; schedules `StyleScrollBars`.

---

#### `private static void StyleScrollBars(ScrollViewer viewer, bool isSidebar)` (Windows)

**Purpose:** Thin, low-contrast vertical/horizontal scrollbar styling.

---

#### `private static IEnumerable<T> FindDescendants<T>(DependencyObject root)` (Windows)

**Purpose:** BFS visual-tree search for scrollbar restyling.

---

#### `private static (View Control, Entry Entry) CreatePasswordField(string? text)`

**Purpose:** Password entry with reveal toggle overlay.

---

#### `private static void UpdatePasswordToggleIcon(Image toggleIcon, bool isPasswordHidden)`

**Purpose:** Swaps `PasswordIconHidden` / `PasswordIconVisible` source.

---

#### `private static (View Control, Entry Entry) CreateTextField(...)`

**Purpose:** Entry inside `CreateFieldContainer`.

---

#### `private static Entry CreateTextEntry(string? text, bool isPassword, ClearButtonVisibility clearButtonVisibility)`

**Purpose:** Styled entry with `ApplyFlatEntryStyle`.

---

#### `private static void ApplyFlatEntryStyle(Entry entry)`

**Purpose:** WinUI TextBox/PasswordBox zero border and padding.

---

#### `private static void ApplyTextEntryState(Entry entry, bool isReadOnly)`

**Purpose:** Read-only flag and reduced opacity for auto-managed values.

---

## Windows-only

#### `private static extern nint GetForegroundWindow()`

**Purpose:** Fallback HWND for save picker.

#### `private static async Task<string?> PickExportThemeFilePathAsync(string suggestedFileName)`

**Purpose:** `FileSavePicker` for theme JSON export.

---

## Dependencies

`AppDataStore`, `SettingsService`, `AppLocalizationService`, `OllamaSettingsStore`, `UpdateManager`, `AslmApiServer`, `NotificationCenter`, `ThemeService`, `CustomThemesStore`, [ThemeColorPickerView](ThemeColorPickerView/).
