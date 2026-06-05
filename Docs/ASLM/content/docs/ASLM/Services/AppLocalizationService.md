---
title: "AppLocalizationService"
draft: false
---

## Class `AppLocalizationService`

`ASLM/Services/AppLocalizationService.cs` — **`public sealed`** — active UI language for ASLM: applies culture to RESX resources, RTL/LTR flow direction, and notifies [ILocalizable](../Localization/ILocalizable/) views on change.

**DI:** `AddSingleton<AppLocalizationService>()`.

Record at file bottom: **`LanguageOption`**.

---

### Static data

| Name | Description |
| --- | --- |
| `RtlLanguageCodes` | Hash set (`"ar"`, …) for explicit RTL |
| `SupportedLanguageCodes` | 20 locale codes |
| `SupportedLanguages` | `LanguageOption` list sorted by English name |
| `MachineTranslationPickerTag` | `"[AI]"` suffix constant (private) |

---

### Instance fields

| Name | Description |
| --- | --- |
| `_appData` | [AppDataStore](AppDataStore/) for persisted language |
| `_localizableViews` | Weak references to `ILocalizable` |
| `_localizableLock` | Sync for registration list |
| `_appliedLanguage` | Last applied code (default `"en"`) |

---

### Events

| Name | Description |
| --- | --- |
| `CultureChanged` | Raised after **`ApplyCulture`** updates thread culture |

---

## Public methods

#### `public AppLocalizationService(AppDataStore appData)`

**Purpose:** Creates the localization service.

**Steps:**

1. Store `appData` in `_appData`.

---

#### `public string GetCurrentLanguage()`

**Purpose:** Returns the normalized language code from persisted personalization.

**Steps:**

1. Return **`AppPersonalizationConfig.NormalizeLanguage(_appData.Data.Personalization.Language)`**.

---

#### `public void SyncFlowDirection()`

**Purpose:** Reapplies RTL/LTR on the active window page after `Window.Page` is replaced.

**Steps:**

1. Call **`ApplyFlowDirection(CultureInfo.CurrentUICulture)`**.

---

#### `public bool ApplyCulture()`

**Purpose:** Applies the persisted language to UI culture and RESX resources.

**Returns:** `true` when the culture actually changed.

**Steps:**

1. Get language via **`GetCurrentLanguage()`**.
2. If `_appliedLanguage` and `CurrentUICulture.Name` already match → set `AppResources.Culture`, **`ApplyFlowDirection`**, return `false`.
3. Build culture with **`CreateCulture(language)`**; set `CurrentUICulture`, `CurrentCulture`, `AppResources.Culture`; update `_appliedLanguage`.
4. **`ApplyFlowDirection`**, **`NotifyLocalizableViews()`**, raise **`CultureChanged`**, return `true`.

---

#### `public string GetString(string key)`

**Purpose:** Returns a localized string for the given resource key.

**Steps:**

1. Return empty string if `key` is null/whitespace.
2. Return `AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key`.

---

#### `public string GetString(string key, params object[] args)`

**Purpose:** Returns a formatted localized string.

**Steps:**

1. Get format via **`GetString(key)`**.
2. Return format unchanged if `args` is empty; else **`string.Format`**.

---

#### `public void Register(ILocalizable view)`

**Purpose:** Registers a view that should refresh when the active culture changes.

**Steps:**

1. Return if `view` is null.
2. Under lock: **`PruneLocalizableViews()`**; add weak reference if not already registered for this instance.

---

#### `public void Unregister(ILocalizable view)`

**Purpose:** Removes a previously registered view.

**Steps:**

1. Return if `view` is null.
2. Under lock: remove entries whose target is dead or equals `view`.

---

#### `public static string GetDisplayName(string languageCode)`

**Purpose:** Returns the native display name for a language code, or the code when unknown.

**Steps:**

1. Normalize with **`AppPersonalizationConfig.NormalizeLanguage`**.
2. Return **`GetCultureNativeName(normalized)`**.

---

#### `public static string GetPickerDisplayName(string languageCode)`

**Purpose:** Bilingual language-picker label: English — native, with `[AI]` tag for non-English locales.

**Steps:**

1. Normalize language code.
2. Get English and native names via **`GetCultureEnglishName`** / **`GetCultureNativeName`**.
3. If equal (case-insensitive) → English only; else `"{english} - {native}"`.
4. Append **`MachineTranslationPickerTag`** when not English.
5. Return label.

---

## Private methods

#### `private static string GetCultureEnglishName(string languageCode)`

**Purpose:** Returns the English culture name for one language code.

**Steps:**

1. Try **`CreateCulture(languageCode).EnglishName`**.
2. On **`CultureNotFoundException`** → return `languageCode`.

---

#### `private static string GetCultureNativeName(string languageCode)`

**Purpose:** Returns the native culture name for one language code.

**Steps:**

1. Try **`CreateCulture(languageCode).NativeName`**.
2. On **`CultureNotFoundException`** → return `languageCode`.

---

#### `private static CultureInfo CreateCulture(string language)`

**Purpose:** Creates `CultureInfo` for a supported code, falling back to English.

**Steps:**

1. Switch: `zh-Hans`, `zh-Hant`, `pt-BR`, or default **`GetCultureInfo(language)`**.
2. On **`CultureNotFoundException`** → English.

---

#### `private static void ApplyFlowDirection(CultureInfo culture)`

**Purpose:** Applies RTL or LTR to the active application page; embedded WebViews stay LTR.

**Steps:**

1. Detect RTL from `RtlLanguageCodes`, two-letter code, or `culture.TextInfo.IsRightToLeft`.
2. Resolve active `Application.Current` window page; return if null.
3. Set `page.FlowDirection`; **`ResetEmbeddedWebViewsOnPage(page)`**.
4. For RTL → schedule **`ResetEmbeddedWebViewsOnPage`** again on main thread (Shell timing).

---

#### `private static void ResetEmbeddedWebViewsOnPage(Page page)`

**Purpose:** Walks the active page and pins every embedded WebView to LTR.

**Steps:**

1. If `ContentPage` with `Content` as `Element` → **`ResetEmbeddedWebViewsToLeftToRight`** on root.
2. Else if `page` is `Element` → recurse from page.

---

#### `private static void ResetEmbeddedWebViewsToLeftToRight(Element root)`

**Purpose:** Recursively sets `FlowDirection.LeftToRight` on WebViews in the visual tree.

**Steps:**

1. If `WebView` → set LTR.
2. `Layout` → foreach child `Element` or `WebView`.
3. `ContentView`, `ScrollView`, `Border` → recurse into content.

---

#### `private void NotifyLocalizableViews()`

**Purpose:** Refreshes every registered localizable view after culture changes.

**Steps:**

1. Under lock: prune; copy live `ILocalizable` targets to a list.
2. For each target → **`ApplyLocalization()`** (swallow per-view exceptions).

---

#### `private void PruneLocalizableViews()`

**Purpose:** Removes dead weak references from the registered view list.

**Steps:**

1. **`RemoveAll`** references that fail **`TryGetTarget`**.

---

## Related types (same file)

### `LanguageOption` (record)

`Id`, `EnglishName` — one selectable UI language in personalization settings.

---

## Related

- [AppDataStore](AppDataStore/)
- [AppPersonalizationConfig](../Models/AppPersonalizationConfig/)
