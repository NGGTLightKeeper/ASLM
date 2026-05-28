---
title: "UpdateModels"
draft: false
---

## Overview

`ASLM/Models/UpdateModels.cs` — configuration and DTOs for ASLM self-update and module updates (`UpdateManager`, **`GitHubUpdateClient`**, patcher).

---

## Class `AppUpdateSourceConfig`

Shipped JSON describing where ASLM releases are downloaded:

| Property | JSON | Description |
| --- | --- | --- |
| `FileVersion` | `fileVersion` | Schema |
| `Source` | `source` | [ModuleSource](ModuleConfig/)-shaped GitHub source |
| `DefaultChannel` | `defaultChannel` | `release` or `pre-release` |
| `Assets` | `assets` | Channel → asset name map |
| `Preserve` | `preserve` | Paths kept during app patch (no `..`) |

---

## Class `UpdateCandidate`

In-memory available update (not persisted as-is):

| Property | Description |
| --- | --- |
| `TargetKind` | `app` or `module` |
| `TargetId` | Module id or app id |
| `Name`, `DisplayName` | Labels |
| `CurrentVersion`, `RemoteVersion` | Compared versions |
| `Channel`, `Mode` | release / pre-release / branch |
| `DownloadUrl` | Asset URL |
| `ReferenceName`, `ReleaseTag`, `CommitSha` | Git refs |
| `IsVirtualLatest`, `IsPrerelease` | Picker semantics |
| `PublishedAt` | Release time |
| `Module` | Linked [ModuleConfig](ModuleConfig/) when applicable |

---

## Class `PendingAppUpdate`

Written to **`.aslm-update/pending.json`** for the [Patcher](../../Patcher/Program/):

| Property | JSON |
| --- | --- |
| `Kind` | `kind` — must be `app` |
| `Version` | `version` |
| `StagingPath` | `stagingPath` |
| `TargetRoot` | `targetRoot` |
| `BackupPath` | `backupPath` |
| `Preserve` | `preserve` |
| `CreatedUtc` | `createdUtc` |

---

## Record `GitHubBranchInfo`

| Field | Description |
| --- | --- |
| `Name` | Branch name |
| `CommitSha` | Tip commit |

Used when module update **mode** is **`branch`**.
