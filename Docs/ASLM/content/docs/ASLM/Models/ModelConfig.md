---
title: "ModelConfig"
draft: false
---

## Overview

`ASLM/Models/ModelConfig.cs` — deserializes **`ASLM_Model.json`** for a downloadable model package.

---

## Class `ModelConfig`

| Property | JSON | Default / notes |
| --- | --- | --- |
| `FileVersion` | `fileVersion` | `1` |
| `Id` | `id` | Stable id |
| `Name` | `name` | UI name |
| `Description` | `description` | |
| `Version` | `version` | |
| `Type` | `type` | `model` |
| `Category` | `category` | Dependency matching |
| `Source` | `source` | Remote repository |
| `Files` | `files` | Optional explicit file list |
| `Status` | `status` | Install state |
| `SourcePath` | *(ignored)* | Runtime path |

**`Normalize()`** — trims strings, normalizes **`Source`** and **`Status`**, drops empty **`Files`** entries.

---

## `ModelSource`

| Property | JSON | Default |
| --- | --- | --- |
| `Type` | `type` | `huggingface` |
| `RepoId` | `repoId` | e.g. `org/model` |

---

## `ModelStatus`

| Property | JSON |
| --- | --- |
| `Installed` | `installed` |
| `InstalledVersion` | `installedVersion` |
| `LastChecked` | `lastChecked` |

Installation is performed by host services using this manifest shape; see Services (when documented).
