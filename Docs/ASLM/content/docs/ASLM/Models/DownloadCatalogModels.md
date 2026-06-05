---
title: "DownloadCatalogModels"
draft: false
---

## Overview

`ASLM/Models/DownloadCatalogModels.cs` — JSON DTOs for the **module downloads bridge** (stdio protocol) and the **merged shared download catalog** shown in the host UI. Static bridge declaration: [ModuleDownloadBridgeConfig](ModuleDownloadBridgeConfig/).

---

## Bridge request / response

### `ModuleDownloadBridgeRequest`

| Property | JSON | Description |
| --- | --- | --- |
| `ProtocolVersion` | `protocolVersion` | Default 1 |
| `Operation` | `operation` | e.g. `list_categories`, `list_items`, `describe_item`, `resolve_install` |
| `CategoryId` | `categoryId` | |
| `ResourceKey` | `resourceKey` | |
| `QueryText` | `queryText` | Search |
| `Filters` | `filters` | Selected filter keys |
| `PreferCached` | `preferCached` | Allow module cache |
| `ForceRefresh` | `forceRefresh` | Refresh upstream first |

### `ModuleDownloadBridgeResponse`

| Property | JSON |
| --- | --- |
| `ProtocolVersion`, `Success`, `Error`, `Warnings` | |
| `Categories` | `list<ModuleDownloadCategoryPayload>` |
| `Items` | `list<ModuleDownloadItemPayload>` |
| `Filters` | `list<ModuleDownloadFilterPayload>` |
| `ItemDetail` | `ModuleDownloadItemDetailPayload` |
| `InstallManifest` / `UninstallManifest` | `ModuleDownloadInstallManifest` |

---

## Bridge payloads (module → ASLM)

| Type | Role |
| --- | --- |
| `ModuleDownloadCategoryPayload` | Category row (`id`, `title`, `groupKey`, `sortOrder`, …) |
| `ModuleDownloadItemPayload` | Grouped resource family in a list |
| `ModuleDownloadFilterPayload` | Filter chip (`key`, `title`, `kind`, `selected`) |
| `ModuleDownloadItemDetailPayload` | Detail pane with `variants` and `blocks` |
| `ModuleDownloadVariantPayload` | Selectable variant |
| `ModuleDownloadInfoBlockPayload` | Extra content (`format`: text, etc.) |
| `ModuleDownloadInstallManifest` | Install plan: `resourceKey`, `actions` |
| `ModuleDownloadInstallAction` | Whitelisted step: `type`, `url`, `model`, `packages`, `engineId`, … |

---

## Merged catalog (UI)

Built by **`DownloadCatalog`** from all configured bridges:

| Type | Role |
| --- | --- |
| `DownloadCatalogSnapshot` | `Categories` + `Warnings` |
| `DownloadCatalogCategory` | Merged category with `Items` and `Filters` |
| `DownloadCatalogFilter` | UI filter row |
| `DownloadCatalogItem` | Merged item + `Installed`, `Sources` |
| `DownloadCatalogItemDetail` | Merged detail + variants/blocks |
| `DownloadCatalogVariant` | Variant with install state |
| `DownloadCatalogInfoBlock` | Rendered info block |
| `DownloadCatalogItemSource` | Contributing module (`ModuleId`, `CategoryId`, …) |

---

## Persistent state

### `DownloadCatalogStateFile`

| Property | JSON |
| --- | --- |
| `Resources` | `resources` — map resourceKey → state |

### `DownloadCatalogResourceState`

| Property | JSON |
| --- | --- |
| `Installed` | `installed` |
| `InstalledVersion` | `installedVersion` |
| `LastInstalledUtc` | `lastInstalledUtc` |
| `ProviderModuleId` | `providerModuleId` |

---

## `DownloadInstallResult`

Record **`(bool Success, string Message)`** — outcome of one install request.
