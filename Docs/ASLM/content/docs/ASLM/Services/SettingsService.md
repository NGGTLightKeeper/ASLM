---
title: "SettingsService"
draft: false
---

## Class `SettingsService`

`ASLM/Services/SettingsService.cs` — **`public sealed`** — settings page orchestration.

**DI:** [EngineInstaller](EngineInstaller/), [ModuleInstaller](ModuleInstaller/), [ModuleRunner](ModuleRunner/).

---

## Related types and nested members

#### `public sealed record SettingsCategory( string Id, string Title, string Description, SettingsCategoryKind Kind, ModuleConfig? Module, bool SupportsAppRestart)`

**Purpose:** Describes one selectable settings category shown in the sidebar.

---

#### `public sealed record LoadedSetting(ModuleSetting Setting, object? Value)`

**Purpose:** Couples one setting with the runtime value loaded for the current refresh pass.

---

#### `public sealed record SettingBaseline(string DisplayValue, bool UseCustomValue)`

**Purpose:** Captures the initial effective value used to detect real user changes across UI rebuilds.

---

#### `public sealed record AslmBaseline(string UserName, string OfficialPort, string ThirdPartyPort, bool ApiServerEnabled)`

**Purpose:** Stores the initial ASLM values loaded for the current page session.

---

#### `public sealed record ConsoleBaseline(bool SidebarVisible, bool ShowCompletedProcesses, bool ShowIndividualModuleConsoles)`

**Purpose:** Stores the initial console preferences loaded for the current page session.

---

#### `public sealed record UpdateBaseline( bool CheckEnabled, bool AutoUpdateEnabled, string AutoCheckPeriodHours, string AppChannel, string ModuleDefaultMode, string ModuleDefaultChannel)`

**Purpose:** Stores the initial update settings loaded for the current page session.

---

#### `public sealed record AslmDraftSnapshot( string UserName, string OfficialPort, string ThirdPartyPort, bool ApiServerEnabled, ConsoleBaseline ConsoleBaseline, UpdateBaseline UpdateBaseline)`

**Purpose:** Snapshot of editable ASLM drafts derived from persisted app data and runtime state.

---

#### `public sealed record ModuleSaveResult(HashSet<ModuleConfig> TouchedModules, List<string> DeferredSettings)`

**Purpose:** Summarizes the modules touched during one save operation.

---

## Public methods

#### `public SettingsService( EngineInstaller engineInstaller, ModuleInstaller moduleInstaller, ModuleRunner moduleRunner)`

**Purpose:** ---

#### `public static SettingsCategoryGroup GetGroupForCategory(SettingsCategory category)`

---

#### `public static string GetModuleRuntimeKey(ModuleConfig module)`

**Purpose:** Stable key used to remember whether live runtime values were already loaded for a module.

---

#### `public List<SettingsCategory> CreateOrderedCategories(IReadOnlyList<ModuleConfig> loadedModules)`

**Purpose:** Builds the ordered category list with ASLM categories first and modules after them.

---

#### `public Task<List<ModuleConfig>> DiscoverModulesAsync()`

**Purpose:** Discovers installed modules and returns their configuration snapshots for the settings page.

---

#### `public static PortParseResult TryParsePorts(string officialDraft, string thirdPartyDraft)`

**Purpose:** Validates the port draft values and returns parsed integers when valid.

---

#### `public static bool TryValidateDisplayName(string? draft, out string normalizedName, out string errorMessage)`

**Purpose:** Validates one display name draft and returns trimmed value.

---

#### `public static bool TryValidateAndBuildUpdateSettings(UpdateBaseline draft, out AppUpdateSettings settings, out string errorMessage)`

**Purpose:** Reads and validates update settings from a draft snapshot.

---

#### `public static string BuildSaveMessage(bool hasAslmChanges, bool hasModuleChanges, List<string> deferredSettings)`

**Purpose:** (appearance, custom themes) were persisted in this save operation.

---

#### `public static AslmDraftSnapshot BuildAslmDraftSnapshot(AppDataStore appData, bool apiServerEnabled)`

**Purpose:** Builds editable ASLM draft values from persisted app data and runtime API-state.

---

#### `public static void ApplyDraftsToAppData( AppDataStore appData, string userName, int officialPort, int thirdPartyPort, ConsoleBaseline consoleDraft, AppUpdateSettings updateSettings)`

**Purpose:** Writes ASLM and update drafts to persisted app data.

---

#### `public static string? BuildAslmInstalledReleaseSummary(AppDataStore appData)`

**Purpose:** Builds optional copy for the Updates manual-check card when the patcher persisted a GitHub release tag.

---

#### `public static UpdateBaseline BuildDefaultUpdateBaseline()`

**Purpose:** Creates the default update baseline used by reset actions in settings UI.

---

#### `public static (string OfficialPort, string ThirdPartyPort, bool ApiServerEnabled, ConsoleBaseline ConsoleDefaults) BuildDefaultAslmDrafts()`

**Purpose:** Builds ASLM defaults for ports, API and console sections.

---

#### `public static bool HasUnsavedAccountChanges(string userName, AslmBaseline baseline)`

**Purpose:** Checks whether account display-name draft differs from baseline.

---

#### `public static bool HasUnsavedPortChanges(string officialPort, string thirdPartyPort, AslmBaseline baseline)`

**Purpose:** Checks whether ports draft differs from baseline.

---

#### `public static bool HasUnsavedApiServerChanges(bool apiServerEnabled, AslmBaseline baseline)`

**Purpose:** Checks whether API-enabled draft differs from baseline.

---

#### `public static bool HasUnsavedConsoleChanges(ConsoleBaseline draft, ConsoleBaseline baseline)`

**Purpose:** Checks whether console draft differs from baseline.

---

#### `public static bool HasUnsavedUpdateChanges(UpdateBaseline draft, UpdateBaseline baseline)`

**Purpose:** Checks whether update draft differs from baseline.

---

#### `public static bool HasUnsavedAslmSettingsChanges( string officialPort, string thirdPartyPort, bool apiServerEnabled, ConsoleBaseline consoleDraft, UpdateBaseline updateDraft, AslmBaseline aslmBaseline, ConsoleBaseline consoleBaseline, UpdateBaseline updateBaseline)`

**Purpose:** Checks whether non-account ASLM settings differ from baseline.

---

#### `public object? GetResolvedSettingValue(ModuleConfig module, ModuleSetting setting)`

**Purpose:** ---

#### `public Task StopAllModulesAsync()`

Stops every running module process before applying settings that require a clean slate.

---

#### `public async Task RestartModuleAsync(ModuleConfig module)`

**Purpose:** Restarts one module using the same flow as the module management page.

---

#### `public static void StartLauncherForSelfUpdate()`

**Purpose:** Starts the launcher so it can detect the prepared update after the current app exits.

---

#### `public static string ResolveRootForSelfUpdate()`

**Purpose:** Resolves the ASLM root folder that contains the pending update manifest.

---

#### `public static void ResetModuleToDefaults(ModuleConfig module)`

**Purpose:** Restores every editable setting in the selected module back to its manifest default.

---

#### `public static bool ShouldDisplaySetting(ModuleSetting setting)`

**Purpose:** Filters out settings that should never be shown in the UI editor.

---

#### `public static bool ShouldRenderSetting( ModuleSetting setting, IReadOnlyList<ModuleSetting> allSettings, IReadOnlyDictionary<string, object?> valuesByKey)`

**Purpose:** Evaluates whether a setting should currently be visible based on its controlling toggle.

---

#### `public static string BuildSettingDescription(ModuleSetting setting)`

**Purpose:** ---

#### `public static bool IsActiveEngineSelector(ModuleSetting setting)`

Determines whether a setting should use the segmented active-engine selector.

---

#### `public static bool HasDependentSettings(ModuleConfig module, ModuleSetting setting)`

**Purpose:** Checks whether the current setting controls the visibility of any other setting.

---

#### `public bool TryValidateModuleSettings(ModuleConfig module, out string errorMessage)`

**Purpose:** Validates the saved draft values for one loaded module.

---

#### `public bool ModuleHasChangesComparedToBaseline(ModuleConfig module, Dictionary<string, SettingBaseline> baselines)`

**Purpose:** Determines whether a loaded module has settings that differ from the saved baseline.

---

#### `public async Task LoadModuleDraftAsync( ModuleConfig module, bool reloadRuntimeValues, Dictionary<string, SettingBaseline> baselines)`

**Purpose:** Loads one module's visible settings, optionally using live runtime getters.

---

#### `public async Task<ModuleSaveResult> SaveActiveModuleAsync( ModuleConfig module, Dictionary<string, SettingBaseline> baselines)`

**Purpose:** Persists the changed settings for a module and applies runtime updates where possible.

---

#### `public async Task<LoadedSetting> LoadSettingValueAsync(ModuleConfig module, ModuleSetting setting)`

**Purpose:** Loads one setting value from runtime get-exec or manifest fallback.

---

#### `public object? GetCurrentSettingValue(ModuleConfig module, ModuleSetting setting)`

**Purpose:** Resolves the current draft value used for rendering and save comparison.

---

#### `public object? GetFallbackValue(ModuleConfig module, ModuleSetting setting)`

**Purpose:** Resolves the best available value when runtime loading is skipped or fails.

---

#### `public object? ResolveEffectiveSettingValue(ModuleConfig module, ModuleSetting setting, object? currentValue)`

**Purpose:** ---

#### `public void UpdateSettingBaselines( ModuleConfig module, IEnumerable<LoadedSetting> loadedSettings, Dictionary<string, SettingBaseline> baselines)`

Refreshes per-setting baselines after a load pass so unsaved-change detection stays accurate.

---

#### `public SettingBaseline GetSettingBaseline( ModuleConfig module, ModuleSetting setting, object? currentValue, Dictionary<string, SettingBaseline> baselines)`

**Purpose:** ---

#### `public static string GetSettingIdentity(ModuleConfig module, ModuleSetting setting)`

Stable identity key for one module setting within baseline dictionaries.

---

#### `public bool IsAutoDetectedAslmEngine(ModuleSetting setting)`

**Purpose:** Detects whether an engine-style setting maps directly to an ASLM engine installation.

---

#### `public bool IsAslmEngineInstalled(string engineId)`

**Purpose:** Checks whether the specified ASLM engine is currently installed on the system.

---

#### `public static bool TryValidateSettingValue(ModuleSetting setting, object? rawValueObj, out string errorMessage)`

**Purpose:** Validates one setting value according to its declared manifest type.

---

## Private methods

#### `private static ModuleSetting? FindControllingSetting( ModuleSetting setting, IReadOnlyList<ModuleSetting> allSettings, IReadOnlyDictionary<string, object?> valuesByKey)`

**Purpose:** Finds the boolean toggle that controls whether  is visible.

---

#### `private static bool IsGroupedUnder(string parentKey, string childKey)`

**Purpose:** ---

#### `private static bool IsVisibilityToggle(ModuleSetting setting, IReadOnlyDictionary<string, object?> valuesByKey)`

---

## Related

- [ModuleRunner](ModuleRunner/)
- [EngineInstaller](EngineInstaller/)
- [AppDataStore](AppDataStore/)
- [AslmApiServer](AslmApiServer/)
