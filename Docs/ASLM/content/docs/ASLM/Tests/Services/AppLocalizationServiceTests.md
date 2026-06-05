---
title: "AppLocalizationServiceTests"
draft: false
---

## Class `AppLocalizationServiceTests`

`ASLM/Tests/Services/AppLocalizationServiceTests.cs` — static language display helpers on [AppLocalizationService](../../Services/AppLocalizationService/).

---

## Test methods

#### `public void GetDisplayName_returns_native_culture_name(string code, string expectedFragment)`

**Purpose:** `GetDisplayName` returns the culture’s native name containing the expected substring.

| `code` | `expectedFragment` |
| --- | --- |
| `en` | `English` |
| `ru` | `русский` |

| Step | Action |
| --- | --- |
| 1 | `AppLocalizationService.GetDisplayName(code)` |
| 2 | Assert result contains `expectedFragment` |

---

#### `public void SupportedLanguages_contains_english_entry()`

**Purpose:** Built-in supported language list includes English (`en`).

| Step | Action |
| --- | --- |
| 1 | Read `AppLocalizationService.SupportedLanguages` |
| 2 | Assert any option has `Id` equal to `en` (ordinal ignore case) |

---

#### `public void GetPickerDisplayName_includes_native_name_for_english()`

**Purpose:** Picker label for English includes the native name “English”.

| Step | Action |
| --- | --- |
| 1 | `GetPickerDisplayName("en")` |
| 2 | Assert result contains `"English"` |

---

## Related

- [AppLocalizationService](../../Services/AppLocalizationService/)
