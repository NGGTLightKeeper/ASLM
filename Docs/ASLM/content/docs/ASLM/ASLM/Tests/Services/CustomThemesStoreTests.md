---
title: "CustomThemesStoreTests"
draft: false
---

## Class `CustomThemesStoreTests`

`ASLM/Tests/Services/CustomThemesStoreTests.cs` — tests [CustomThemesStore](../../Services/CustomThemesStore/) naming, persistence, import/export.

**Helpers:** [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/), [TestLoggerFactory](../TestSupport/TestLoggerFactory/).

---

## Test methods

#### `public void AllocateUniqueDisplayName_appends_suffix_on_collision()`

**Purpose:** Duplicate display names get a `#2` suffix when a theme with the same name already exists.

| Step | Action |
| --- | --- |
| 1 | Layout + store; add theme `Name = "Ocean"` |
| 2 | `AllocateUniqueDisplayName("Ocean")` |
| 3 | Assert returns `"Ocean #2"` |

---

#### `public async Task SaveAsync_and_LoadAsync_round_trip_theme()`

**Purpose:** Custom theme colors persist across save and reload.

| Step | Action |
| --- | --- |
| 1 | `CreateTheme("Sunset", "light")`; set `Colors["SystemBlue"] = "#112233"` |
| 2 | `await SaveAsync()` |
| 3 | New store → `await LoadAsync()` |
| 4 | `FindById(theme.Id)` not null; color `#112233` preserved |

---

#### `public void ImportThemeCopy_assigns_new_id_and_unique_name()`

**Purpose:** `ImportThemeCopy` clones a theme with a new id and disambiguated name.

| Step | Action |
| --- | --- |
| 1 | `CreateTheme("Forest", "dark")` → `ImportThemeCopy(source)` |
| 2 | Assert `copy.Id != source.Id`, `copy.Name == "Forest #2"` |

---

#### `public void DeserializeImportedTheme_parses_single_theme_json()`

**Purpose:** JSON from `SerializeThemeForExport` round-trips through `DeserializeImportedTheme`.

| Step | Action |
| --- | --- |
| 1 | Export theme from `CreateTheme("Export", "dark")` |
| 2 | `DeserializeImportedTheme(exported)` |
| 3 | Assert not null, `Name == "Export"` |

---

## Related

- [CustomThemesStore](../../Services/CustomThemesStore/)
