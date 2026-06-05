---
title: "Models"
draft: false
---

JSON-serializable types and UI binding models for the host. Most types expose **`Normalize()`** after deserialization to restore defaults and strip invalid entries.

## Manifest files (on disk)

| File | Type | Page |
| --- | --- | --- |
| `ASLM_Module.json` | [ModuleConfig](ModuleConfig/) | Per installed module |
| `ASLM_Engine.json` | [EngineConfig](EngineConfig/) | Engine runtime package |
| `ASLM_Model.json` | [ModelConfig](ModelConfig/) | Downloadable model package |
| `Data/App/ASLM_Data.json` | [AppData](AppData/) | Host preferences |
| `Data/App/ASLM_CustomThemes.json` | [CustomThemesModels](CustomThemesModels/) | User themes |
| `Data/App/download-catalog-state.json` | [DownloadCatalogModels](DownloadCatalogModels/) | Shared download install state |
| Shipped / remote trust & update JSON | [ModuleTrustModels](ModuleTrustModels/), [UpdateModels](UpdateModels/) | Trust lists and updater |

## Runtime / UI models

| Type | Page |
| --- | --- |
| In-app notifications | [AppNotification](AppNotification/) |
| Download progress sample | [DownloadProgress](DownloadProgress/) |
| Running module summary | [RunningModuleSnapshot](RunningModuleSnapshot/) |
| Ollama account UI state | [OllamaPersistentSettings](OllamaPersistentSettings/) |

## Bridge and interop (declared in module manifest)

| Type | Page |
| --- | --- |
| Downloads bridge manifest | [ModuleDownloadBridgeConfig](ModuleDownloadBridgeConfig/) |
| Bridge wire DTOs + merged catalog | [DownloadCatalogModels](DownloadCatalogModels/) |
| Module interop opt-in | [ModuleInteropManifest](ModuleInteropManifest/) |
