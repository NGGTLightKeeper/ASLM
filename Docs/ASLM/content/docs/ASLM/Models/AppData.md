---
title: "AppData"
draft: false
---

## Overview

`ASLM/Models/AppData.cs` — root object persisted as **`Data/App/ASLM_Data.json`**. Loaded and saved by **`AppDataStore`** (Services).

---

## Class `AppData`

| JSON property | Type | Description |
| --- | --- | --- |
| `firstRunCompleted` | `bool` | Setup wizard finished |
| `user` | `AppUserData` | Display name |
| `ports` | `AppPortConfig` | Module start port |
| `api` | `AppApiConfig` | Local API mirror server toggle |
| `consoles` | `AppConsoleConfig` | Consoles page preferences |
| `updates` | `AppUpdateSettings` | ASLM and module update policy |
| `personalization` | `AppPersonalizationConfig` | Theme, language, custom theme id |

#### `void Normalize()`

**Purpose:** Recreates missing nested objects and calls each child **`Normalize()`**.

---

## `AppUserData`

| Property | JSON | Default |
| --- | --- | --- |
| `Name` | `name` | `""` |

---

## `AppApiConfig`

| Property | JSON | Default |
| --- | --- | --- |
| `ServerEnabled` | `serverEnabled` | `true` |

Port assignment is handled at runtime by **`PortRegistry`**, not stored here.

---

## `AppConsoleConfig`

| Property | JSON | Default |
| --- | --- | --- |
| `SidebarVisible` | `sidebarVisible` | `true` |
| `ShowCompletedProcesses` | `showCompletedProcesses` | `false` |
| `ShowIndividualModuleConsoles` | `showIndividualModuleConsoles` | `true` |

---

## `AppPortConfig`

| Property | JSON | Default |
| --- | --- | --- |
| `ModulesStart` | `modulesStart` | `20000` |

#### `void Normalize()`

**Purpose:** Migrates legacy official/third-party port settings and clamps the start port.

---

## `AppUpdateSettings`

| Property | JSON | Notes |
| --- | --- | --- |
| `CheckEnabled` | `checkEnabled` | Default `true` |
| `AutoUpdateEnabled` | `autoUpdateEnabled` | Default `true` |
| `AutoCheckPeriodHours` | `autoCheckPeriodHours` | Clamped 1–720; default 12 |
| `LastAutoCheckUtc` | `lastAutoCheckUtc` | ISO UTC or null |
| `AppChannel` | `appChannel` | `release` or `pre-release` |
| `InstalledReleaseTag` | `installedReleaseTag` | Written by patcher on success |
| `ModuleDefaultMode` | `moduleDefaultMode` | `release` or `branch` |
| `ModuleDefaultChannel` | `moduleDefaultChannel` | `release` or `pre-release` |

**`Normalize()`** — normalizes channel/mode strings and clamps period hours.

---

## `AppPersonalizationConfig`

| Property | JSON | Notes |
| --- | --- | --- |
| `Appearance` | `appearance` | `Dark`, `Light`, `System`, or `Custom` |
| `Language` | `language` | BCP-47-style code; see supported set below |
| `CustomThemeId` | `customThemeId` | Set only when appearance is `Custom` |

#### `NormalizeLanguage(string? value)`

**Purpose:** Falls back to **`en`** if the code is not in the supported set:

`en`, `zh-Hans`, `es`, `ar`, `hi`, `pt-BR`, `ru`, `ja`, `de`, `fr`, `ko`, `it`, `zh-Hant`, `pt`, `tr`, `pl`, `uk`, `id`, `vi`, `nl`.

#### `NormalizeAppearance(string? value)`

Unknown values → **`Dark`**.
