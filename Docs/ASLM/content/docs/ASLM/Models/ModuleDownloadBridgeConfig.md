---
title: "ModuleDownloadBridgeConfig"
draft: false
---

## Overview

`ASLM/Models/ModuleDownloadBridgeConfig.cs` — static declaration in **`ASLM_Module.json`** (`downloadsBridge`) for modules that expose dynamic download catalogs over JSON stdio.

Runtime request/response DTOs live in [DownloadCatalogModels](DownloadCatalogModels/).

---

## Class `ModuleDownloadsBridge`

| Property | JSON | Description |
| --- | --- | --- |
| `ProtocolVersion` | `protocolVersion` | Wire protocol (default 1) |
| `Engine` | `engine` | Engine id to launch bridge |
| `EntryPoint` | `entryPoint` | Script/command under module dir |
| `Operations` | `operations` | e.g. `list_items`, `resolve_install` |
| `Categories` | `categories` | Declared category metadata |
| `Targets` | `targets` | Named install roots inside ASLM |

#### `bool IsConfigured`

**Purpose:** `true` when **`EntryPoint`** is non-empty.

---

## Class `ModuleDownloadBridgeCategory`

| Property | JSON |
| --- | --- |
| `Id` | `id` |
| `Title` | `title` |
| `Description` | `description` |
| `GroupKey` | `groupKey` — merge key across modules |
| `TargetRef` | `targetRef` |
| `SortOrder` | `sortOrder` |

---

## Class `ModuleDownloadBridgeTarget`

Maps **`targetRef`** to a safe directory under the install layout:

| Property | JSON | Example |
| --- | --- | --- |
| `Root` | `root` | `Models`, `Data` |
| `Relative` | `relative` | subfolder |
| `Description` | `description` | Help text |
