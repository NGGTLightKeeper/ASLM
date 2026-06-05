---
title: "ModuleInteropManifest"
draft: false
---

## Overview

`ASLM/Models/ModuleInteropManifest.cs` — optional block inside **`ASLM_Module.json`** declaring that a module consumes the host **`moduleInterop`** HTTP API and environment injection.

---

## Class `ModuleInteropManifest`

| Property | JSON | Default |
| --- | --- | --- |
| `ProtocolVersion` | `protocolVersion` | `1` |
| `Client` | `client` | See below |

#### `bool IsClientEnabled`

**Purpose:** `true` when **`ProtocolVersion >= 1`** and **`Client.Enabled`**.

**`Normalize()`** — ensures **`Client`** exists and is normalized.

---

## Class `ModuleInteropClientConfig`

| Property | JSON |
| --- | --- |
| `Enabled` | `enabled` |

When enabled, the host starts **`AslmModuleInteropServer`** and injects interop environment variables for that module (see Services).
