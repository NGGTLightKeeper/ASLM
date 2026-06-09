---
title: "ModuleConfig"
draft: false
---

## Overview

`ASLM/Models/ModuleConfig.cs` — deserializes **`ASLM_Module.json`** for each module under **`Modules/{id}/`**. Central manifest for install state, GitHub source, dependencies, commands, settings, updates, and optional bridges.

**`Normalize()`** on the root restores all nested objects, trims strings, and normalizes child collections.

---

## Class `ModuleConfig`

| Property | JSON | Description |
| --- | --- | --- |
| `FileVersion` | `fileVersion` | Schema (default 1) |
| `Id` | `id` | Stable module id |
| `Name` | `name` | Display name |
| `Description` | `description` | |
| `Version` | `version` | Package version |
| `Author` | `author` | |
| `Type` | `type` | Module kind |
| `Category` | `category` | Tag list (metadata) |
| `Source` | `source` | [ModuleSource](#modulesource) |
| `Dependencies` | `dependencies` | [ModuleDependencies](#moduledependencies) |
| `Commands` | `commands` | [ModuleCommands](#modulecommands) |
| `HasPage` | `hasPage` | Contributes shell page |
| `Icon` | `icon` | Relative icon path |
| `SidebarIcon` | `sidebarIcon` | Relative sidebar icon |
| `Settings` | `settings` | [ModuleSetting](#modulesetting) list |
| `DownloadsBridge` | `downloadsBridge` | [ModuleDownloadsBridge](ModuleDownloadBridgeConfig/) |
| `ModuleInterop` | `moduleInterop` | [ModuleInteropManifest](ModuleInteropManifest/) |
| `Update` | `update` | [ModuleUpdateConfig](#moduleupdateconfig) |
| `Status` | `status` | [ModuleStatus](#modulestatus) |
| `SourcePath` | *(ignored)* | Absolute manifest path |
| `HasDeclaredUpdateConfig` | *(ignored)* | Whether JSON contained `update` |
| `IconFullPath` / `SidebarIconFullPath` | *(ignored)* | Resolved asset paths |

---

## `ModuleUpdateConfig`

| Property | JSON | Notes |
| --- | --- | --- |
| `LatestReleaseTag` | *(const)* | `"latest"` virtual tag |
| `Mode` | `mode` | `release`, `pre-release`, or `branch` |
| `Channel` | `channel` | Derived from mode |
| `Branch` | `branch` | Default `main` when mode is branch |
| `AssetName` | `assetName` | Optional GitHub asset |
| `Preserve` | `preserve` | Paths kept across update |
| `RunFirstRunAfterUpdate` | `runFirstRunAfterUpdate` | Default `true` |
| `InstalledCommitSha` | `installedCommitSha` | Branch mode |
| `InstalledReleaseTag` | `installedReleaseTag` | Release mode |
| `SelectedReleaseTag` | `selectedReleaseTag` | User picker / rollback |
| `PendingUpdate` | `pendingUpdate` | [ModulePendingUpdate](#modulependingupdate) badge cache |

---

## `ModulePendingUpdate`

Persisted “update available” snapshot for module cards.

| Property | JSON |
| --- | --- |
| `UpdateMode` | `updateMode` |
| `Branch` | `branch` |
| `ReleaseSelectionKey` | `releaseSelectionKey` |
| `RemoteVersion` | `remoteVersion` |
| `DisplayName` | `displayName` |
| `ReleaseTag` | `releaseTag` |
| `CommitSha` | `commitSha` |
| `ReferenceName` | `referenceName` |
| `DownloadUrl` | `downloadUrl` |
| `IsVirtualLatest` | `isVirtualLatest` |
| `IsPrerelease` | `isPrerelease` |
| `Channel` | `channel` |
| `CheckedUtc` | `checkedUtc` |

**`FromCandidate(ModuleConfig, UpdateCandidate)`** — builds snapshot from a live check.

---

## `ModuleSource`

| Property | JSON | Default |
| --- | --- | --- |
| `Type` | `type` | `github` |
| `Repo` | `repo` | `owner/repo` |

---

## `ModuleDependencies`

| Property | JSON |
| --- | --- |
| `Engines` | `engines` — [ModuleEngineDependency](#moduleenginedependency) |
| `Modules` | `modules` — [ModuleModuleDependency](#modulemoduledependency) |
| `Models` | `models` — required model category strings |

---

## `ModuleModuleDependency`

| Property | JSON |
| --- | --- |
| `Id` | `id` — stable identifier of the required module |

---

## `ModuleEngineDependency`

| Property | JSON |
| --- | --- |
| `Id` | `id` — engine id |
| `Libraries` | `libraries` — extra packages |

---

## `ModuleCommands`

| Property | JSON |
| --- | --- |
| `FirstRun` | `firstRun` — one-time setup commands |
| `Run` | `run` — normal launch commands |

---

## `ModuleCommand`

| Property | JSON |
| --- | --- |
| `Name` | `name` |
| `Description` | `description` |
| `Engine` | `engine` — engine id |
| `Exec` | `exec` — command relative to module dir |

---

## `ModuleSetting`

User-configurable module setting (rendered in module UI).

| Property | JSON | Notes |
| --- | --- | --- |
| `Key` | `key` | Persistence key |
| `Name` | `name` | Label |
| `Description` | `description` | Help |
| `Type` | `type` | `string`, `bool`, `port`, `theme`, `locale`, `json`, … |
| `Default` | `default` | Initial value |
| `Value` | `value` | Current value |
| `UseCustomValue` | `useCustomValue` | Override auto-managed |
| `AllowedValues` | `allowedValues` | Choice list |
| `Engine` | `engine` | For get/set exec |
| `GetExec` / `SetExec` | `getExec`, `setExec` | Module commands |

### Derived

| Member | Rule |
| --- | --- |
| `NormalizedType` | Lowercase trimmed `Type` |
| `IsAutomaticallyManaged` | `path`, `data`, or `models` |

### Parsing helpers

| Method | Role |
| --- | --- |
| `ParseUserInput` | Text → persisted scalar |
| `NormalizeUserValue` | Control value → scalar |
| `ParseSerializedValue` | Type-aware parse |
| `FormatValueForDisplay` | Scalar → UI string |

**`theme`** and **`locale`** types are host-managed: ASLM resolves JSON and applies via **`setExec`** without env injection in the module UI.

---

## `ModuleStatus`

| Property | JSON |
| --- | --- |
| `Installed` | `installed` |
| `Enabled` | `enabled` |
| `FirstRunCompleted` | `firstRunCompleted` |
| `InstalledVersion` | `installedVersion` |
| `LastChecked` | `lastChecked` |
| `LastUpdated` | `lastUpdated` |
