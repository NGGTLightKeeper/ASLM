---
title: "AppDataStoreTests"
draft: false
---

## Class `AppDataStoreTests`

`ASLM/Tests/Services/AppDataStoreTests.cs` — tests [AppDataStore](../../Services/AppDataStore/) load/save persistence and corrupt-file recovery.

**Helpers:** [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/), [TestLoggerFactory](../TestSupport/TestLoggerFactory/).

---

## Test methods

#### `public async Task LoadAsync_creates_defaults_when_file_missing()`

**Purpose:** When `ASLM_Data.json` is absent, `LoadAsync` materializes default `AppData`, marks first run, and writes the file.

| Step | Action |
| --- | --- |
| 1 | `AslmFileSystemLayout.ResetDataAppDirectory()` — no app data file |
| 2 | `new AppDataStore(logger)` → `await LoadAsync()` |
| 3 | Assert `IsFirstRun == true` |
| 4 | Assert `File.Exists(layout.AppDataFilePath)` |

---

#### `public async Task SaveAsync_and_LoadAsync_round_trip()`

**Purpose:** Mutations saved by one store instance survive reload via a fresh `AppDataStore`.

| Step | Action |
| --- | --- |
| 1 | Layout + store → `await LoadAsync()` |
| 2 | Set `FirstRunCompleted = true`, `User.Name = "RoundTrip"` → `await SaveAsync()` |
| 3 | New store → `await LoadAsync()` |
| 4 | Assert `IsFirstRun == false`, `User.Name == "RoundTrip"`, file exists |

---

#### `public async Task LoadAsync_recreates_defaults_on_invalid_json()`

**Purpose:** Corrupt JSON on disk is replaced with defaults instead of leaving the store in a broken state.

| Step | Action |
| --- | --- |
| 1 | `layout.WriteAppDataJson("{ not valid json")` |
| 2 | `await LoadAsync()` |
| 3 | Assert `User.Name` empty, `IsFirstRun == true` |

---

## Related

- [AppDataStore](../../Services/AppDataStore/)
