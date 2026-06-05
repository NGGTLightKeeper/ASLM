---
title: "RunningModuleSnapshot"
draft: false
---

## Overview

`ASLM/Models/RunningModuleSnapshot.cs` — lightweight summary of one module that currently has tracked processes in **`ModuleRunner`**.

---

## Record `RunningModuleSnapshot`

| Parameter | Description |
| --- | --- |
| `Id` | Module id from manifest |
| `Name` | Display name |
| `SourcePath` | Path to module directory / manifest |

Used for consoles and module status UI; not persisted.
