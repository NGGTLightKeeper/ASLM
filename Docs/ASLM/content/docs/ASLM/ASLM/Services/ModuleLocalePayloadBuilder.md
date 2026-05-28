---
title: "ModuleLocalePayloadBuilder"
draft: false
---

## Class `ModuleLocalePayloadBuilder`

`ASLM/Services/ModuleLocalePayloadBuilder.cs` — **`public sealed`** — builds a JSON snapshot of the host ASLM UI language for modules that declare a **`locale`** setting.

Delivered through standard **`setExec`** integration (typically via a temp file path).

**DI:** `AddSingleton<ModuleLocalePayloadBuilder>()` — depends on [AppDataStore](AppDataStore/).

---

## Public methods

#### `public ModuleLocalePayloadBuilder(AppDataStore appData, ILogger<ModuleLocalePayloadBuilder> logger)`

**Purpose:** Creates the payload builder and stores dependencies.

---

#### `public string BuildJson()`

**Purpose:** Serializes the active host language to a single-line JSON string (camelCase, no indentation).

| Step | Action |
| --- | --- |
| 1 | `language` ← `AppPersonalizationConfig.NormalizeLanguage(_appData.Data.Personalization.Language)` |
| 2 | Build `ModuleHostLocalePayloadDto` with `DisplayName` from `AppLocalizationService.GetDisplayName(language)` |
| 3 | `JsonSerializer.Serialize(dto, JsonOptions)` |
| On error | Log; return `{"language":"en","displayName":"English"}` |

| JSON field | Source |
| --- | --- |
| `language` | Normalized personalization language |
| `displayName` | `AppLocalizationService.GetDisplayName` |

---

## Private types (same file)

### `ModuleHostLocalePayloadDto` (private sealed class)

| Property | Default |
| --- | --- |
| `Language` | `"en"` |
| `DisplayName` | `"English"` |

---

## Related

- [ModuleRunner](ModuleRunner/) — `ResolveSettingValue` for type **`locale`**
- [AppLocalizationService](AppLocalizationService/)
