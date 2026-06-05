---
title: "CustomThemesStore"
draft: false
---

## Class `CustomThemesStore`

`ASLM/Services/CustomThemesStore.cs` — **`public`** — loads and saves user-defined themes from **`Data/App/ASLM_CustomThemes.json`**. Model: [CustomThemesRoot](../Models/CustomThemesRoot/).

---

### Fields

| Name | Type | Description |
| --- | --- | --- |
| `_filePath` | `string` | Path to `ASLM_CustomThemes.json` |
| `_logger` | `ILogger<CustomThemesStore>` | Load/import warnings |
| `_jsonOptions` | `JsonSerializerOptions` | Indented; ignore nulls on write |
| `TrailingNumberSuffix` | `Regex` | Strips trailing ` #N` from display names |

---

### Properties

| Name | Type | Description |
| --- | --- | --- |
| `Root` | `CustomThemesRoot` | Loaded custom themes (private setter) |

---

## Public methods

#### `public CustomThemesStore(ILogger<CustomThemesStore> logger)`

**Purpose:** Creates the custom themes store.

**Steps:**

1. Throw if `logger` is null.
2. Resolve `_filePath` via **`GetRootDirectory()`** → `{root}/Data/App/ASLM_CustomThemes.json`.

---

#### `public async Task LoadAsync()`

**Purpose:** Loads persisted custom themes or initializes an empty collection when absent.

**Steps:**

1. If file exists and JSON non-empty → deserialize to `Root`, **`Root.Normalize()`**, return.
2. On exception → log error, fall through.
3. Assign **`new CustomThemesRoot()`**.

---

#### `public async Task SaveAsync()`

**Purpose:** Persists the current custom themes collection to disk.

**Steps:**

1. **`EnsureDirectoryExists()`**.
2. **`Root.Normalize()`**, serialize, **`WriteAllTextAsync`**.

---

#### `public CustomTheme? FindById(string? id)`

**Purpose:** Returns the theme with the requested identifier, or null when not found.

**Steps:**

1. Return null if `id` is null/whitespace.
2. First theme in `Root.Themes` with case-insensitive matching `Id`.

---

#### `public CustomTheme CreateTheme(string name, string baseAppearance)`

**Purpose:** Adds a new theme with a generated identifier and returns it.

**Steps:**

1. **`AllocateUniqueDisplayName(name)`** for display name.
2. Create `CustomTheme` with new `Guid` id (`"N"`), normalized base appearance, empty case-insensitive color dictionary.
3. Add to `Root.Themes`, return theme.

---

#### `public string AllocateUniqueDisplayName(string desiredName, string? exceptThemeId = null)`

**Purpose:** Returns a display name that does not collide with existing themes (` #2`, ` #3`, … when needed).

**Steps:**

1. Trim `desiredName` or use `"Theme"` if empty.
2. If not taken (optionally excluding `exceptThemeId`) → return candidate.
3. Strip trailing number suffix via regex; default stem `"Theme"` if blank.
4. Increment from `2` until `"{stem} #{index}"` is free.

---

#### `public CustomTheme ImportThemeCopy(CustomTheme source)`

**Purpose:** Adds a copy of an imported theme with a new id and non-colliding name.

**Steps:**

1. **`source.Normalize()`**.
2. Clone with new `Guid` id, **`AllocateUniqueDisplayName(source.Name)`**, copied colors.
3. Add to `Root.Themes`, return copy.

---

#### `public string SerializeThemeForExport(CustomTheme theme)`

**Purpose:** Serializes one theme for sharing or backup.

**Steps:**

1. **`theme.Normalize()`**.
2. Return JSON via `_jsonOptions`.

---

#### `public CustomTheme? DeserializeImportedTheme(string json)`

**Purpose:** Parses JSON from a single-theme export or root document (first theme used).

**Steps:**

1. Return null if `json` is null/whitespace.
2. Try parse as object with `themes` array → deserialize **`CustomThemesRoot`**, normalize, return first theme.
3. On failure → log warning.
4. Try deserialize single **`CustomTheme`**; normalize and return, or null on failure.

---

#### `public bool DeleteTheme(string id)`

**Purpose:** Removes the theme with the requested identifier.

**Steps:**

1. **`RemoveAll`** themes with case-insensitive matching `Id`; return whether count > 0.

---

## Private methods

#### `private bool IsDisplayNameTaken(string name, string? exceptThemeId)`

**Purpose:** Returns whether another theme already uses the requested display name.

**Steps:**

1. Any theme where id differs from `exceptThemeId` (if set) and `Name` equals `name` (ignore case).

---

#### `private void EnsureDirectoryExists()`

**Purpose:** Ensures the parent directory for the data file exists.

**Steps:**

1. Create directory for `Path.GetDirectoryName(_filePath)` when non-empty.

---

#### `private static string GetRootDirectory()`

**Purpose:** Returns the application root directory above the deployed app folder.

**Steps:**

1. Parent of `BaseDirectory`, or base dir if no parent.

---

## Related

- [CustomTheme](../Models/CustomTheme/)
- [ThemeService](ThemeService/)
