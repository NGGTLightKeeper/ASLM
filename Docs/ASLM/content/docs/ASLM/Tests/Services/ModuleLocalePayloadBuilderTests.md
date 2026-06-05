---
title: "ModuleLocalePayloadBuilderTests"
draft: false
---

## Class `ModuleLocalePayloadBuilderTests`

`ASLM/Tests/Services/ModuleLocalePayloadBuilderTests.cs` — JSON locale payload for module interop from [ModuleLocalePayloadBuilder](../../Services/ModuleLocalePayloadBuilder/).

**Helpers:** [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/), [AppDataStore](../../Services/AppDataStore/), [TestLoggerFactory](../TestSupport/TestLoggerFactory/).

---

## Test methods

#### `public void BuildJson_serializes_active_language()`

**Purpose:** `BuildJson` emits `language` and non-empty `displayName` from app personalization.

| Step | Action |
| --- | --- |
| 1 | Layout + `AppDataStore`; set `Personalization.Language = "de"` |
| 2 | `new ModuleLocalePayloadBuilder(appData, logger)` → parse `BuildJson()` |
| 3 | Assert `language == "de"`, `displayName` not whitespace |

---

## Related

- [ModuleLocalePayloadBuilder](../../Services/ModuleLocalePayloadBuilder/)
