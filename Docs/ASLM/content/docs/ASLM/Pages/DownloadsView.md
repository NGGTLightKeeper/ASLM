---
title: "DownloadsView"
draft: false
---

## Class `DownloadsView`

`ASLM/Pages/DownloadsView.xaml.cs` — three-column **shared download catalog** overlay. Merges [ModuleDownloadBridge](../Models/ModuleDownloadBridgeConfig/) responses from installed modules. Implements **`ILocalizable`**, **`INotifyPropertyChanged`**.

Raises **`CloseRequested`** when the user dismisses the overlay.

---

### Constants

| Name | Value |
| --- | --- |
| `DialogWidthFactor` | `0.88` |
| `DialogHeightFactor` | `0.84` |
| `MinDialogWidth` | `1080` |
| `MinDialogHeight` | `620` |
| `MaxDialogWidth` | `1520` |
| `MaxDialogHeight` | `920` |

Theme palette colors resolve via `GetColorResource` from keys: `BackgroundTertiary`, `ActionBlue`, `BackgroundSecondary`, `BackgroundPrimary`, `Separator`, `LabelPrimary`, `LabelSecondary`, `LinkColor`.

---

### Fields

| Name | Description |
| --- | --- |
| `_catalog` | `DownloadCatalog` — load/search/filter snapshots |
| `_installer` | `DownloadInstaller` — install/uninstall variants |
| `_localization` | `AppLocalizationService` — `LocalizableAttach` |
| `_catalogRefreshCts` | Cancels in-flight catalog loads |
| `_detailRefreshCts` | Cancels in-flight item detail loads |
| `_searchDebounceCts` | Debounces remote search refresh |
| `_hasLoaded` | One-time layout init on `Loaded` |
| `_isBusy` | Busy overlay for catalog refresh |
| `_isInstalling` | Install/remove in progress |
| `_statusText` | Footer status line |
| `_itemListEmptyMessage` / `_detailEmptyMessage` | Bindable empty-state copy |
| `_searchText` | Bound search query |
| `_selectedFilterKeys` | Active provider filter keys |
| `_lastCatalogSignature` / `_lastDetailSignature` | Skip redundant silent refreshes |
| `_categoryItems` | Items for active category |
| `_activeCategory` | Selected `DownloadCategoryViewModel` |
| `_selectedItem` / `_selectedItemDetail` | Center/detail selection |
| `_selectedVariant` / `_selectedInfoBlock` | Detail pane selection |
| `_selectedInfoBlockSource` | WebView source for HTML blocks |
| `_selectedInfoBlockWebHeight` | Measured WebView height |
| `_nativeInfoBlockWebView` (Windows) | WinUI `WebView2` for wheel redirect |
| `_nativeDetailScrollViewer` (Windows) | Outer detail scroll host |
| `_detailScrollTargetY` (Windows) | Wheel scroll target offset |

---

### XAML elements (`DownloadsView.xaml`)

| Name | Role |
| --- | --- |
| `DownloadDialog` | Root bordered dialog; sized by `UpdateDialogSize` |
| `DownloadsTitleLabel` | Overlay title |
| `SearchEntry` | Debounced search; `OnSearchTextChanged` |
| Category list | `Categories` — sidebar |
| Filter chips | `Filters` — provider/sort |
| Item list | `CurrentItems` — center column |
| `ItemListEmptyTitleLabel` | Empty list title |
| `RefreshButton` | `RefreshCommand` / `OnRefreshClicked` |
| `DetailScrollView` | Variant + info blocks; `OnDetailScrollViewHandlerChanged` |
| `VariantSectionLabel` | Variant picker header |
| `InstallButton` | `InstallCommand` |
| `OpenButton` | `OpenVariantCommand` |
| `RemoveButton` | `DeleteSelectedVariantAsync` |
| `DetailEmptyTitleLabel` | No selection placeholder |
| Info block `WebView` | `OnInfoBlockWebViewHandlerChanged`, navigating/navigated |

---

### Commands and collections

| Member | Description |
| --- | --- |
| `RefreshCommand` | `ManualRefreshAsync` |
| `InstallCommand` | `InstallSelectedVariantAsync` |
| `OpenVariantCommand` | `OpenSelectedVariantAsync` |
| `Categories`, `Filters`, `CurrentItems` | Sidebar / filters / list |
| `Variants`, `InfoBlocks` | Detail pane |
| `CloseRequested` | Shell hides overlay |

---

## Public methods — `DownloadsView`

#### `public DownloadsView(DownloadCatalog catalog, DownloadInstaller installer, AppLocalizationService localization)`

**Purpose:** Builds the overlay, wires commands, localization, and layout events.

**Steps:**

1. Store services; `InitializeComponent`; `BindingContext = this`.
2. Create `RefreshCommand`, `InstallCommand`, `OpenVariantCommand`.
3. `LocalizableAttach.Hook`; subscribe `DetailScrollView.HandlerChanged`, `Loaded`, `SizeChanged`.
4. Set initial `StatusText` to ready localized string.

---

#### `public void ApplyLocalization()`

**Purpose:** Applies localized strings to named controls and bindable headers.

**Steps:**

1. Set title, search placeholder, empty labels, action button text.
2. `OnPropertyChanged` for category title/count bindables.
3. Call `RefreshLocalizedBindableText`.

---

#### `public async Task OpenAsync()`

**Purpose:** Shell entry — size dialog, show cached catalog, background refresh.

**Steps:**

1. `UpdateDialogSize`.
2. `LoadCatalogAsync` with cache, no force, busy indicator.
3. Fire silent forced refresh in background (`silentRefresh: true`).

---

#### `protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** Raises `PropertyChanged` for bindable overlay state.

**Steps:**

1. Invoke `PropertyChanged` with `PropertyChangedEventArgs`.

---

## Private methods — `DownloadsView`

### Initialization and layout

#### `private void OnLoaded(object? sender, EventArgs e)`

**Purpose:** One-time size and native scroll binding.

**Steps:**

1. Return if `_hasLoaded`; set flag.
2. `UpdateDialogSize`; `OnDetailScrollViewHandlerChanged`.

---

#### `private void UpdateDialogSize()`

**Purpose:** Clamps dialog to factor-based width/height within min/max.

**Steps:**

1. Return if `Width`/`Height` ≤ 0.
2. Set `DownloadDialog.WidthRequest` / `HeightRequest` via `ClampDialogSize`.

---

#### `private static double ClampDialogSize(double value, double min, double max)`

**Purpose:** Clamps one dialog dimension.

**Steps:**

1. Return `Max(min, Min(max, value))`.

---

#### `private static Color GetColorResource(string key, Color fallback)`

**Purpose:** Resolves theme color from `Application.Current.Resources`.

**Steps:**

1. Try resource key; return `Color` or fallback.

---

### Catalog refresh

#### `private async Task ManualRefreshAsync()`

**Purpose:** User-triggered full catalog refresh.

**Steps:**

1. `LoadCatalogAsync` with `forceRefresh: true`, busy indicator, not silent.

---

#### `private async Task LoadCatalogAsync(bool preferCached, bool forceRefresh, bool preserveSelection, bool showBusyIndicator, bool silentRefresh)`

**Purpose:** Loads catalog snapshot; preserves selection; optional silent skip.

**Steps:**

1. Replace `_catalogRefreshCts`; capture preserve keys.
2. Set `IsBusy` / status text when appropriate.
3. `Task.Run` → `_catalog.LoadCatalogAsync` with search + filters.
4. Compare `ComputeCatalogSignature`; skip UI if silent and unchanged.
5. `ApplySnapshot`; set empty messages; load or clear detail.
6. Update status on success; handle cancel/exception; clear busy; `RaiseLayoutProperties`.

---

#### `private void ApplySnapshot(DownloadCatalogSnapshot snapshot, string? selectedCategoryKey, string? selectedItemKey)`

**Purpose:** Rebuilds category sidebar and reselects closest category/item.

**Steps:**

1. Clear/add `DownloadCategoryViewModel` per category.
2. Find target category by key or first.
3. `ActivateCategory` with preserved item key.

---

#### `private void ActivateCategory(DownloadCategoryViewModel? category, string? selectedItemKey)`

**Purpose:** Sets active category and rebuilds filters + item list.

**Steps:**

1. Update `_activeCategory` and `IsSelected` on all categories.
2. Copy category items; `BuildAvailableFilters`; `ApplyCurrentItemFilters`.

---

#### `private void BuildAvailableFilters()`

**Purpose:** Rehydrates filter chips from category payload.

**Steps:**

1. Order filters; prune `_selectedFilterKeys` to available keys.
2. Default-select filters marked `Selected` when none chosen.
3. Rebuild `Filters` collection with `DownloadFilterViewModel`.

---

#### `private void ApplyCurrentItemFilters(string? selectedItemKey)`

**Purpose:** Rebuilds center list for active category.

**Steps:**

1. Sort items; populate `CurrentItems`.
2. `SetSelectedItem` to matching or first item.

---

#### `private async Task ToggleFilter(DownloadFilterViewModel filter)`

**Purpose:** Sort filters single-select; capability filters multi-select.

**Steps:**

1. Update `_selectedFilterKeys` by `filter.Kind`.
2. `UpdateFilterSelectionStates`; `RefreshCurrentQueryAsync`.

---

#### `private void UpdateFilterSelectionStates()`

**Purpose:** Mirrors `_selectedFilterKeys` into filter view models.

**Steps:**

1. Set each `DownloadFilterViewModel.IsSelected` from key set.

---

#### `private IReadOnlyCollection<string> GetSelectedFilterKeys()`

**Purpose:** Stable ordered filter key list for catalog query.

**Steps:**

1. Return sorted copy of `_selectedFilterKeys`.

---

#### `private string BuildEmptyItemListMessage()`

**Purpose:** Contextual empty list message.

**Steps:**

1. Search text → search empty; explicit filters → filtered empty; else category empty.

---

#### `private void RefreshLocalizedBindableText()`

**Purpose:** Reapplies localization to computed bindable strings.

**Steps:**

1. `OnPropertyChanged` for header/category bindables.
2. Refresh `DetailEmptyMessage` / `ItemListEmptyMessage` when visible.

---

#### `private bool HasExplicitProviderFilterSelection()`

**Purpose:** True when user selected a non-default provider filter.

**Steps:**

1. Any selected key other than `sort:popular`.

---

#### `private async Task RefreshCurrentQueryAsync()`

**Purpose:** Silent re-query without busy overlay.

**Steps:**

1. `LoadCatalogAsync` with `preferCached: false`, `forceRefresh: true`, silent.

---

#### `private void ScheduleRemoteSearchRefresh()`

**Purpose:** 350 ms debounced search refresh.

**Steps:**

1. Replace `_searchDebounceCts`.
2. Delay on background; `MainThread` → `RefreshCurrentQueryAsync`.

---

### Selection

#### `private async Task SelectCategoryAsync(DownloadCategoryViewModel category)`

**Purpose:** Switches category and loads detail.

**Steps:**

1. No-op if same category.
2. `ActivateCategory`; load detail cached then background refresh.

---

#### `private async Task SelectItemAsync(DownloadListItemViewModel item)`

**Purpose:** Selects item and loads detail.

**Steps:**

1. Skip if same item with detail loaded.
2. `SetSelectedItem`; cached then forced detail load.

---

#### `private async Task LoadSelectedItemDetailAsync(bool preferCached, bool forceRefresh, string? selectedVariantKey, bool silentRefresh)`

**Purpose:** Loads item detail; ignores stale responses.

**Steps:**

1. Clear detail if no item; replace `_detailRefreshCts`.
2. `Task.Run` → `_catalog.GetItemDetailAsync`.
3. Verify item still selected; signature skip if silent unchanged.
4. `ApplyDetail` or handle errors.

---

#### `private void ApplyDetail(DownloadCatalogItemDetail detail, string? selectedVariantKey)`

**Purpose:** Rebuilds variants and info blocks.

**Steps:**

1. Store detail; rebuild `Variants` and `InfoBlocks`.
2. Select variant by key, default, or first; preserve info block when possible.
3. `RaiseDetailProperties`.

---

#### `private void ClearDetail()`

**Purpose:** Clears detail pane state.

**Steps:**

1. Null detail; clear collections; reset variant/block; `RaiseDetailProperties`.

---

#### `private void SetSelectedItem(DownloadListItemViewModel? item)`

**Purpose:** Updates list selection; clears or preserves detail.

**Steps:**

1. Detect same resource key for detail preserve.
2. Update selection flags; clear or partial clear detail.

---

#### `private void SelectVariant(DownloadVariantViewModel? variant)`

**Purpose:** Routes variant tap to setter.

**Steps:**

1. `SetSelectedVariant(variant)`.

---

#### `private void SetSelectedVariant(DownloadVariantViewModel? variant)`

**Purpose:** Updates variant selection and action visibility.

**Steps:**

1. Set `_selectedVariant`; update `IsSelected`; `RaiseVariantProperties`.

---

#### `private Task SelectInfoBlock(DownloadInfoBlockViewModel? block)`

**Purpose:** Routes info tab tap to setter.

**Steps:**

1. `SetSelectedInfoBlock(block)`; return completed task.

---

#### `private void SetSelectedInfoBlock(DownloadInfoBlockViewModel? block)`

**Purpose:** Rebuilds WebView source and resets height.

**Steps:**

1. Update selection; reset web height to 720; `BuildInfoBlockSource`.
2. Windows: capture scroll Y; `RaiseInfoBlockProperties`.

---

### Install and open

#### `private async Task InstallSelectedVariantAsync()`

**Purpose:** Installs selected variant via `DownloadInstaller`.

**Steps:**

1. Guard item/variant/not installing; `IsInstalling = true`.
2. `Task.Run` install with progress → `StatusText`.
3. On success refresh catalog; handle cancel/error; `IsInstalling = false`.

---

#### `private async Task DeleteSelectedVariantAsync()`

**Purpose:** Uninstalls selected variant.

**Steps:**

1. Same pattern as install with `UninstallAsync`.

---

#### `private async Task OpenSelectedVariantAsync()`

**Purpose:** Opens homepage in system browser.

**Steps:**

1. Resolve URL; validate URI; `Launcher.OpenAsync`; update status.

---

#### `private string GetSelectedVariantHomepageUrl()`

**Purpose:** Variant → item detail → list item homepage fallback chain.

**Steps:**

1. Return first non-empty URL in priority order.

---

#### `private static WebViewSource? BuildInfoBlockSource(DownloadInfoBlockViewModel? block)`

**Purpose:** Resolves absolute/file/combined URL for WebView.

**Steps:**

1. Try absolute `ContentUrl`, rooted file, or combine with `SourceUrl`.

---

#### `private string BuildItemMetadataLine()` / `private string BuildItemTagsLine()`

**Purpose:** Formats detail header metadata and tags.

**Steps:**

1. Join provider, version, detail and tags with ` | `.

---

### UI handlers

#### `private void OnBackgroundTapped` / `OnDialogTapped` / `OnCloseClicked` / `RequestClose`

**Purpose:** Backdrop close, swallow inner taps, cancel work, raise `CloseRequested`.

**Steps:**

1. `RequestClose` cancels CTS tokens; invokes event.

---

#### `private void OnSearchEntryHandlerChanged` (Windows)

**Purpose:** Flat search entry chrome.

**Steps:**

1. Zero border/transparent background on native `TextBox`.

---

#### `private async void OnRefreshClicked` / `OnInstallClicked` / `OnDeleteClicked` / `OnOpenClicked`

**Purpose:** Forward button clicks to async workflows.

---

#### `private void OnSearchTextChanged`

**Purpose:** Updates `SearchText` and schedules debounced refresh.

---

#### `private void OnDetailScrollViewHandlerChanged` (Windows)

**Purpose:** Caches native `ScrollViewer` for wheel forwarding.

---

#### `private void OnInfoBlockWebViewHandlerChanged` / `OnInfoBlockWebViewNavigating` / `OnInfoBlockWebViewNavigated`

**Purpose:** WebView2 wheel redirect, external link cancel + launcher, auto height from JS.

---

#### `private void OnInfoBlockWebViewPointerWheelChanged` / `GetCurrentDetailScrollY` / `ScrollDetailSurfaceTo` (Windows)

**Purpose:** Forwards wheel from embedded WebView to outer scroll.

---

### Signatures and property refresh

#### `private static string ComputeCatalogSignature` / `ComputeDetailSignature` / `AppendSignatureSegment`

**Purpose:** Compact change detection for silent refresh.

---

#### `private static CancellationTokenSource ReplaceCancellationTokenSource(ref CancellationTokenSource? current)`

**Purpose:** Cancels/disposes prior CTS before new operation.

---

#### `private void RaiseLayoutProperties` / `RaiseCategoryProperties` / `RaiseDetailProperties` / `RaiseVariantProperties` / `RaiseInfoBlockProperties`

**Purpose:** Batch `OnPropertyChanged` for bindable UI sections.

---

## Nested type `DownloadCategoryViewModel`

| Member | Description |
| --- | --- |
| `Category`, `Title`, `Description`, `ItemCountLabel` | Catalog category data |
| `IsSelected`, color properties | Selection chrome |
| `SelectCommand` | Invokes `_selectAction` |

#### `public DownloadCategoryViewModel(DownloadCatalogCategory category, Func<DownloadCategoryViewModel, Task> selectAction)`

**Purpose:** Binds one sidebar category.

**Steps:** Store category and action; create `SelectCommand`.

#### `private void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** Raises selection/color property changes.

---

## Nested type `DownloadFilterViewModel`

#### `public DownloadFilterViewModel(string key, string title, string kind, Func<DownloadFilterViewModel, Task> toggleAction)`

**Purpose:** One filter chip with `ToggleCommand`.

#### `private void OnPropertyChanged(...)`

**Purpose:** Filter selection chrome notifications.

---

## Nested type `DownloadListItemViewModel`

#### `public DownloadListItemViewModel(DownloadCatalogItem item, Func<DownloadListItemViewModel, Task> selectAction)`

**Purpose:** One center-list card.

#### `private void OnPropertyChanged(...)`

**Purpose:** Item selection chrome notifications.

---

## Nested type `DownloadVariantViewModel`

#### `public DownloadVariantViewModel(DownloadCatalogVariant variant, Action<DownloadVariantViewModel?> selectAction)`

**Purpose:** One variant row in selector.

#### `private string BuildSelectorDetailLine()`

**Purpose:** Compact secondary line (summary, tags, version).

#### `private string ResolveSizeLabel()`

**Purpose:** Size tag from tags or detail head segment.

#### `private void OnPropertyChanged(...)`

**Purpose:** Variant selection notifications.

---

## Nested type `DownloadInfoBlockViewModel`

#### `public DownloadInfoBlockViewModel(DownloadCatalogInfoBlock block, string? baseUrl, Func<DownloadInfoBlockViewModel, Task> selectAction)`

**Purpose:** One details tab; `RenderedContent` from formatter.

#### `private static string ResolveRenderedContent(DownloadCatalogInfoBlock block, string? baseUrl)`

**Purpose:** Empty text when block is HTML URL/file; else `InfoBlockTextFormatter.Render`.

#### `private void OnPropertyChanged(...)`

**Purpose:** Tab selection notifications.

---

## Nested type `InfoBlockTextFormatter`

Static helpers convert markdown/HTML blocks to plain text for non-WebView display. Uses compiled regexes: `ScriptRegex`, `FencedCodeRegex`, `HeadingRegex`, `UnorderedListRegex`, `OrderedListRegex`, `BlockquoteRegex`, `HorizontalRuleRegex`.

#### `public static string Render(DownloadCatalogInfoBlock block, string? baseUrl)`

**Purpose:** Dispatches on `block.Format` to HTML, markdown, or plain normalization.

**Steps:** Empty content → `""`; try format switch; on exception return `NormalizePlainText` of raw content.

---

#### `private static string NormalizePlainText(string text)`

**Purpose:** CRLF/CR → LF and trim.

---

#### `private static string ConvertMarkdownToText(string markdown, string? baseUrl)`

**Purpose:** Walks lines; handles fences, headings, HR, quotes, lists, paragraphs.

**Steps:** Split lines; toggle code blocks; `FlushParagraph` on blanks; apply block regexes; inline via `ApplyInlineMarkdownToText`.

---

#### `private static string ConvertHtmlToText(string html, string? baseUrl)`

**Purpose:** Strips scripts/tags; converts links, images, lists to readable text.

**Steps:** `ScriptRegex` remove; tag replacements; anchor/img regex; strip tags; decode entities; collapse whitespace.

---

#### `private static void FlushParagraph(StringBuilder builder, List<string> paragraphLines)`

**Purpose:** Joins accumulated paragraph lines and appends with blank line.

---

#### `private static void AppendLine(StringBuilder builder, string value)`

**Purpose:** `builder.AppendLine(value)`.

---

#### `private static void AppendBlankLine(StringBuilder builder)`

**Purpose:** Adds blank line if fewer than two trailing line feeds.

---

#### `private static int CountTrailingLineFeeds(StringBuilder builder)`

**Purpose:** Counts trailing `\n` (and `\r`) at end of builder.

---

#### `private static string ApplyInlineMarkdownToText(string value, string? baseUrl)`

**Purpose:** Images, links, bold/italic/code to plain text.

---

#### `private static string AbsolutizeUrl(string? candidate, string? baseUrl)`

**Purpose:** Absolute URI or combine with `baseUrl`; else return candidate.

---

## Dependencies

`DownloadCatalog`, `DownloadInstaller`, `AppLocalizationService`.
