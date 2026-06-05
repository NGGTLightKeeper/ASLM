---
title: "DownloadCatalog"
draft: false
---

## Class `DownloadCatalog`

`ASLM/Services/DownloadCatalog.cs` — **`public`** — builds the shared download catalog by querying every installed module bridge and merging equivalent items.

**DI:** [ModuleInstaller](ModuleInstaller/), [ModuleDownloadBridge](ModuleDownloadBridge/), [DownloadStateStore](DownloadStateStore/).

**Concurrency:** up to **`MaxConcurrentBridgeRequests`** (4) parallel module loads.

---

## Public methods

#### `public DownloadCatalog(ModuleInstaller moduleInstaller, ModuleDownloadBridge bridge, DownloadStateStore stateStore, ILogger<DownloadCatalog> logger)`

**Purpose:** Creates the catalog service.

---

#### `public async Task<DownloadCatalogSnapshot> LoadCatalogAsync(string? queryText = null, IReadOnlyCollection<string>? filters = null, bool preferCached = false, bool forceRefresh = false, CancellationToken ct = default)`

**Purpose:** Discovers modules with configured **`DownloadsBridge`**, loads categories/items per module (throttled), merges into **`CategoryBuilder`** map, returns sorted categories with warnings.

---

#### `public async Task<DownloadCatalogItemDetail> GetItemDetailAsync(DownloadCatalogItem item, bool preferCached = false, bool forceRefresh = false, CancellationToken ct = default)`

**Purpose:** Merges **`GetItemDetailAsync`** from every source via **`ItemDetailBuilder`**; falls back to **`BuildFallbackDetail`** when no bridge detail.

---

## Private methods — catalog loading

#### `private async Task LoadModuleCatalogAsync(ModuleConfig module, string? queryText, IReadOnlyCollection<string>? filters, bool preferCached, bool forceRefresh, List<string> warnings, Dictionary<string, CategoryBuilder> categoryBuilders, object mergeLock, CancellationToken ct)`

**Purpose:** Loads categories and items for one module; merges filters and items under **`groupKey`** / **`resourceGroupKey`**.

---

#### `private static void AddWarning(List<string> warnings, object mergeLock, string warning)`

**Purpose:** Thread-safe warning append.

---

## Private methods — merge helpers

#### `private static CategoryBuilder GetOrCreateCategoryBuilder(IDictionary<string, CategoryBuilder> categoryBuilders, string groupKey, ModuleDownloadCategoryPayload category)`

**Purpose:** Gets or creates category builder; **`Merge`** on existing.

---

#### `private static void MergeItem(CategoryBuilder categoryBuilder, ModuleConfig module, ModuleDownloadCategoryPayload category, ModuleDownloadItemPayload item)`

**Purpose:** Merges item payload and adds **`DownloadCatalogItemSource`**.

---

#### `private DownloadCatalogItemDetail BuildFallbackDetail(DownloadCatalogItem item)`

**Purpose:** Single synthetic variant when **`describe_item`** unavailable; uses persisted install state.

---

#### `private static string GetPrimaryStateKey(ItemBuilder itemBuilder)`

**Purpose:** Variant key or resource key for install state lookup.

---

## Nested class `CategoryBuilder`

#### `public CategoryBuilder(string groupKey, ModuleDownloadCategoryPayload category)`

**Purpose:** Seeds title, description, sort order from first payload.

---

#### `public void Merge(ModuleDownloadCategoryPayload category)`

**Purpose:** Fills empty title/description; adopts non-zero sort order.

---

#### `public void MergeFilter(ModuleDownloadFilterPayload filter)`

**Purpose:** Creates or merges filter builders.

---

#### `public DownloadCatalogCategory ToCategory(DownloadStateStore stateStore)`

**Purpose:** Materializes filters and items sorted for UI.

---

## Nested class `FilterBuilder`

#### `public FilterBuilder(ModuleDownloadFilterPayload filter)`

**Purpose:** Seeds key, title, kind, selected, sort order.

---

#### `public void Merge(ModuleDownloadFilterPayload filter)`

**Purpose:** Merges metadata; **`Selected |=`** across sources.

---

#### `public DownloadCatalogFilter ToFilter()`

**Purpose:** Final filter DTO.

---

## Nested class `ItemBuilder`

#### `public ItemBuilder(string resourceKey, string categoryId, string groupKey, ModuleDownloadItemPayload item)`

**Purpose:** Seeds aggregate list item fields and tag set.

---

#### `public void Merge(ModuleDownloadItemPayload item)`

**Purpose:** Fills empty scalar fields and unions tags.

---

#### `public DownloadCatalogItem ToItem(DownloadStateStore stateStore)`

**Purpose:** Builds **`DownloadCatalogItem`** with install flags and deduped sources.

---

## Nested class `ItemDetailBuilder`

#### `public ItemDetailBuilder(DownloadCatalogItem item)`

**Purpose:** Seeds detail view from list item.

---

#### `public void Merge(ModuleDownloadItemDetailPayload detail)`

**Purpose:** Merges scalars, variants (**`VariantBuilder`**), and info blocks (**`BlockBuilder`**).

---

#### `public DownloadCatalogItemDetail ToDetail(DownloadStateStore stateStore)`

**Purpose:** Sorted variants/blocks; synthetic variant when none returned.

---

## Nested class `VariantBuilder`

#### `public VariantBuilder(ModuleDownloadVariantPayload variant)`

**Purpose:** Seeds one installable variant.

---

#### `public void Merge(ModuleDownloadVariantPayload variant)`

**Purpose:** Merges non-empty variant fields and tags.

---

#### `public DownloadCatalogVariant ToVariant(DownloadStateStore stateStore)`

**Purpose:** Variant with persisted install state.

---

## Nested class `BlockBuilder`

#### `public BlockBuilder(string key, ModuleDownloadInfoBlockPayload block)`

**Purpose:** Seeds info block by id or title key.

---

#### `public void Merge(ModuleDownloadInfoBlockPayload block)`

**Purpose:** Fills empty block metadata.

---

#### `public DownloadCatalogInfoBlock ToBlock()`

**Purpose:** Final info block DTO.

---

## Related

- [DownloadInstaller](DownloadInstaller/)
- [ModuleDownloadBridge](ModuleDownloadBridge/)
- [DownloadStateStore](DownloadStateStore/)
