---
title: "DownloadProgress"
draft: false
---

## Overview

`ASLM/Models/DownloadProgress.cs` — immutable progress sample passed to UI and [AppNotification](AppNotification/) during downloads.

---

## Record `DownloadProgress`

| Parameter | Type | Description |
| --- | --- | --- |
| `Fraction` | `double` | Completion 0.0–1.0 |
| `DownloadedBytes` | `long` | Bytes received |
| `TotalBytes` | `long` | Expected total; `0` if unknown |
| `ActiveTransferName` | `string?` | Optional label for current file/stream |

Produced by download/install services; not persisted directly.
