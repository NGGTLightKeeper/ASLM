---
title: "AppDataStore"
draft: false
---

## Class `AppDataStore`

`ASLM/Services/AppDataStore.cs` — **`public`** — loads and saves persisted application data in **`Data/App/ASLM_Data.json`** under the layout root (parent of the deployed `App` folder). In-memory model: [AppData](../Models/AppData/).

Registered in [MauiProgram](../MauiProgram/) as **`AddSingleton<AppDataStore>()`**. Initialized at startup via **`InitializeAsync()`**.

---

### Fields

| Name | Type | Description |
| --- | --- | --- |
| `_filePath` | `string` | Absolute path to `ASLM_Data.json` |
| `_logger` | `ILogger<AppDataStore>` | Logs load failures |
| `_jsonOptions` | `JsonSerializerOptions` | Indented JSON; `WhenWritingNull` ignore |

---

### Properties

| Name | Type | Description |
| --- | --- | --- |
| `Data` | `AppData` | Current persisted state (private setter) |
| `IsFirstRun` | `bool` | `!Data.FirstRunCompleted` |

---

## Public methods

#### `public AppDataStore(ILogger<AppDataStore> logger)`

**Purpose:** Creates the store and resolves the persisted data file path.

**Steps:**

1. Throw if `logger` is null.
2. Call **`GetRootDirectory()`** for the layout root.
3. Set `_filePath` to `{root}/Data/App/ASLM_Data.json`.

---

#### `public async Task InitializeAsync()`

**Purpose:** Initializes the store by loading persisted data once at startup.

**Steps:**

1. Await **`LoadAsync()`**.

---

#### `public async Task LoadAsync()`

**Purpose:** Loads persisted application data or recreates defaults when the file is missing or invalid.

**Steps:**

1. If `_filePath` exists and content is non-whitespace → deserialize to `Data`, call **`Data.Normalize()`**, return.
2. On exception → log error with `_filePath`.
3. Assign **`new AppData()`**, **`Normalize()`**, **`SaveAsync()`** (creates a valid file for the next run).

---

#### `public void Save()`

**Purpose:** Saves the current application data synchronously.

**Steps:**

1. **`EnsureDirectoryExists()`**.
2. Serialize `Data` with `_jsonOptions`.
3. **`File.WriteAllText`** to `_filePath`.

---

#### `public async Task SaveAsync()`

**Purpose:** Saves the current application data asynchronously.

**Steps:**

1. **`EnsureDirectoryExists()`**.
2. Serialize `Data` with `_jsonOptions`.
3. **`File.WriteAllTextAsync`** to `_filePath`.

---

## Private methods

#### `private void EnsureDirectoryExists()`

**Purpose:** Ensures the parent directory for the data file exists.

**Steps:**

1. Get `Path.GetDirectoryName(_filePath)`.
2. If non-empty → **`Directory.CreateDirectory`**.

---

#### `private static string GetRootDirectory()`

**Purpose:** Returns the application root directory above the deployed app folder.

**Steps:**

1. Trim trailing separator from `AppDomain.CurrentDomain.BaseDirectory`.
2. Return parent full name, or base directory if no parent.

---

## Related

- [AppData](../Models/AppData/)
- [LoadingPage](../Pages/LoadingPage/)
