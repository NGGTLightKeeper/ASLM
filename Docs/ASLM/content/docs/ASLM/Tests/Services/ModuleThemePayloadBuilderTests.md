---
title: "ModuleThemePayloadBuilderTests"
draft: false
---

## Class `ModuleThemePayloadBuilderTests`

`ASLM/Tests/Services/ModuleThemePayloadBuilderTests.cs` — JSON theme payload for module interop from [ModuleThemePayloadBuilder](../../Services/ModuleThemePayloadBuilder/).

**Helpers:** [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/), [AppDataStore](../../Services/AppDataStore/), [CustomThemesStore](../../Services/CustomThemesStore/), [TestLoggerFactory](../TestSupport/TestLoggerFactory/).

---

## Test methods

#### `public void BuildJson_includes_appearance_and_palette_keys()`

**Purpose:** Payload includes user appearance, normalized theme id, and a non-empty `colors` object.

| Step | Action |
| --- | --- |
| 1 | Set `Personalization.Appearance = "Dark"` |
| 2 | `ModuleThemePayloadBuilder(appData, themes, logger)` → parse `BuildJson()` |
| 3 | Assert `appearance == "Dark"`, `theme == "dark"` |
| 4 | Assert `colors` property exists with at least one key |

---

## Related

- [ModuleThemePayloadBuilder](../../Services/ModuleThemePayloadBuilder/)
