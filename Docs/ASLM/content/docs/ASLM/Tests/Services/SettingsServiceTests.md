---
title: "SettingsServiceTests"
draft: false
---

## Class `SettingsServiceTests`

`ASLM/Tests/Services/SettingsServiceTests.cs` — static validation and draft helpers on [SettingsService](../../Services/SettingsService/) (no UI).

**Helpers:** [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/), [AppDataStore](../../Services/AppDataStore/), [ModuleConfigBuilder](../TestSupport/ModuleConfigBuilder/).

---

## Test methods

#### `public void TryParsePorts_validates_ranges_and_overlap(string official, string thirdParty, bool expectedSuccess)`

**Purpose:** Port draft strings must parse, sit in valid ranges, and not overlap official/third-party bands.

| `official` | `thirdParty` | `expectedSuccess` |
| --- | --- | --- |
| `20000` | `30000` | `true` |
| `abc` | `30000` | `false` |
| `20000` | `99999` | `false` |
| `20050` | `20000` | `false` |

| Step | Action |
| --- | --- |
| 1 | `TryParsePorts(official, thirdParty)` |
| 2 | Assert `Success == expectedSuccess` |
| 3 | On success: parsed ports match integers; on failure: non-empty `ErrorMessage` |

---

#### `public void TryValidateDisplayName_trims_and_rejects_empty(string draft, bool expected, string expectedName)`

**Purpose:** Display name validation trims whitespace and rejects blank input.

| `draft` | `expected` | `expectedName` |
| --- | --- | --- |
| ` Alice ` | `true` | `Alice` |
| `   ` | `false` | `""` |

| Step | Action |
| --- | --- |
| 1 | `TryValidateDisplayName(draft, out name, out error)` |
| 2 | Assert `success`, `name`; on failure assert non-empty `error` |

---

#### `public void TryValidateAndBuildUpdateSettings_rejects_invalid_period()`

**Purpose:** Auto-check period `0` is outside allowed 1–720 hour range.

| Step | Action |
| --- | --- |
| 1 | `UpdateBaseline` with period `"0"` |
| 2 | `TryValidateAndBuildUpdateSettings` → `false` |
| 3 | Assert `error` contains `"720"` |

---

#### `public void BuildSaveMessage_describes_deferred_settings()`

**Purpose:** Deferred settings save message lists settings that could not apply immediately.

| Step | Action |
| --- | --- |
| 1 | `BuildSaveMessage(true, false, ["Setting A", "Setting B"])` |
| 2 | Assert message contains deferred wording and both setting names |

---

#### `public void BuildAslmDraftSnapshot_reads_app_data()`

**Purpose:** Draft snapshot strings reflect in-memory `AppData` and API server flag.

| Step | Action |
| --- | --- |
| 1 | `AppDataStore` with `User.Name = "Tester"`, official/third-party starts |
| 2 | `BuildAslmDraftSnapshot(store, apiServerEnabled: true)` |
| 3 | Assert `UserName`, port strings, `ApiServerEnabled` |

---

#### `public void ApplyDraftsToAppData_persists_values_in_memory()`

**Purpose:** `ApplyDraftsToAppData` writes user, ports, console, and update fields on the store.

| Step | Action |
| --- | --- |
| 1 | `ApplyDraftsToAppData(store, "Bob", 22000, 32000, console, updates)` |
| 2 | Assert user name, port starts, console flags, `AutoCheckPeriodHours` |

---

#### `public void HasUnsaved_changes_detect_differences()`

**Purpose:** Unsaved-change helpers detect drift from baseline for account, ports, and API server.

| Step | Action |
| --- | --- |
| 1 | Baseline `AslmBaseline("Alice", "20000", "30000", true)` |
| 2 | `HasUnsavedAccountChanges("Bob", …)` → `true` |
| 3 | `HasUnsavedPortChanges("20001", "30000", …)` → `true` |
| 4 | `HasUnsavedApiServerChanges(false, …)` → `true` |

---

#### `public void ShouldDisplaySetting_hides_automatic_types()`

**Purpose:** UI hides `port` and `theme` settings; shows ordinary types like `text`.

| Step | Action |
| --- | --- |
| 1 | `ShouldDisplaySetting` for `port`, `theme` → `false` |
| 2 | `ShouldDisplaySetting` for `text` → `true` |

---

#### `public void ResetModuleToDefaults_restores_manifest_defaults()`

**Purpose:** `ResetModuleToDefaults` clears custom bool override back to manifest default.

| Step | Action |
| --- | --- |
| 1 | Module with bool setting `Value = true`, `UseCustomValue = true`, `Default = false` |
| 2 | `ResetModuleToDefaults(module)` |
| 3 | Assert setting value string `"False"` |

---

#### `public void GetGroupForCategory_maps_module_kind()`

**Purpose:** Module-scoped settings categories map to `SettingsCategoryGroup.Modules`.

| Step | Action |
| --- | --- |
| 1 | `SettingsCategory` with `SettingsCategoryKind.Module` |
| 2 | `GetGroupForCategory` → `Modules` |

---

## Related

- [SettingsService](../../Services/SettingsService/)
