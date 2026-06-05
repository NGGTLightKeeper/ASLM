---
title: "AppNotification"
draft: false
---

## Overview

`ASLM/Models/AppNotification.cs` — bindable in-app notification row used by **`NotificationCenter`** and the notifications UI. Implements **`INotifyPropertyChanged`**.

---

## Enums

### `AppNotificationCategory`

| Value | Use |
| --- | --- |
| `Updates` | Available updates |
| `Downloads` | Install / download progress |
| `System` | General host messages |

### `AppNotificationSeverity`

| Value | Accent (approx.) |
| --- | --- |
| `Info` | Blue `#0A84FF` |
| `Success` | Green `#32D74B` |
| `Warning` | Yellow `#FFD60A` |
| `Error` | Red `#FF453A` |

---

## Class `AppNotification`

### Identity (immutable)

| Member | Description |
| --- | --- |
| `Id` | Stable id for deduplication |
| `Category` | Filter group |
| `SourceKind` / `SourceId` | Producer reference |
| `CreatedAt` | First seen time |

### Display properties

| Member | Description |
| --- | --- |
| `Title`, `Message` | Primary text |
| `StatusText` | Short status; hidden during active download |
| `DetailText` | Secondary line |
| `Severity` | Drives **`AccentColor`** and **`SeverityLabel`** |
| `IsInProgress`, `HasProgress`, `ProgressFraction` | Progress UI |
| `TimestampLabel` | Local `yyyy-MM-dd HH:mm` from **`UpdatedAt`** |
| `DetailLine` | Compact line for toast / popover |

### Download-specific

| Member | Description |
| --- | --- |
| `ActiveTransferLabel` | Current file or stream name |
| `TransferSpeedDisplay`, `TransferPercentDisplay` | Metrics row |
| `ShowDownloadMetricsRow` | Visible for in-progress download category |
| `ShowActiveTransferInCard` | Suppresses duplicate labels vs **`Message`** |

### Update actions

| Member | Description |
| --- | --- |
| `OffersUpdateActions` | Show Update now / later |
| `UpdateNowText`, `UpdateLaterText` | Via **`L.Get(LocalizationKeys.*)`** |
| `SuppressToastAutoDismiss` | Toast stays until user acts |

---

## Internal API (host services)

| Method | Role |
| --- | --- |
| `UpdateContent(...)` | Refresh text and severity |
| `UpdateProgress(...)` | Progress bar state |
| `ApplyDownloadTransferSample(DownloadProgress, speedLabel)` | Live download metrics |
| `ResetDownloadTransferRow()` | Clear transfer row |
| `SetUpdateAvailabilityPresentation` / `ClearUpdateAvailabilityPresentation` | Update card actions |
| `RefreshLocalizedPresentation()` | Refresh action button labels after culture change |

Persistence constructor restores state from storage for notification history.
