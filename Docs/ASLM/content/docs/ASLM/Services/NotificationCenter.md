---
title: "NotificationCenter"
draft: false
---

## Class `NotificationCenter`

`ASLM/Services/NotificationCenter.cs` — **`public sealed`** — in-app notification list, persistence (**`Data/App/ASLM_Notifications.json`**), update/download toasts, debounced saves.

Uses [DownloadTransferSpeedEstimator](DownloadTransferSpeedEstimator/) for download speed labels. **`DownloadProgress`** from [Models/DownloadProgress](../Models/DownloadProgress/).

Related type in same file: **`UpdateNotificationActionEventArgs`**. Private persistence: **`NotificationStore`**, **`NotificationStoreItem`**.

---

### Constants

| Name | Value |
| --- | --- |
| `NotificationsFileName` | `ASLM_Notifications.json` |
| `MaxPersistedNotifications` | 200 |

---

### Events

| Event | When |
| --- | --- |
| `NotificationsChanged` | List/count/filter projections may have changed |
| `NotificationPublished` | New toast should display (also on download complete) |
| `UpdateNotificationActionRequested` | User chose Update now / Update later |

---

## Public methods — `NotificationCenter`

#### `public NotificationCenter()`

**Purpose:** Resolves file path; wraps **`ObservableCollection<AppNotification>`** as read-only.

---

#### `public async Task InitializeAsync()`

**Purpose:** Loads disk snapshot once.

---

#### `public ReadOnlyObservableCollection<AppNotification> Notifications`

**Purpose:** Newest-first live collection (property getter).

---

#### `public void PublishUpdateCandidate(UpdateCandidate candidate)`

**Purpose:** Upserts update notification with localized title/message, **`OffersUpdateActions`**, suppress auto-dismiss toast.

---

#### `public void RequestUpdateNotificationAction(AppNotification notification, bool updateNow)`

**Purpose:** Raises **`UpdateNotificationActionRequested`** on main thread.

---

#### `public void ClearUpdateNotificationDeferredActions(AppNotification notification)`

**Purpose:** Clears inline update actions after defer from list.

---

#### `public void PublishSystemToast(string title, string message, string statusText, string sourceId)`

**Purpose:** Ephemeral toast only (not added to persisted list).

---

#### `public Task StartDownloadAsync(string operationKey, string title, string message, string sourceKind, string sourceId)`

**Purpose:** Main-thread upsert with progress row; awaits insert before returning.

---

#### `public void ReportDownloadProgress(string operationKey, DownloadProgress progress, string? statusText = null)`

**Purpose:** Updates fraction, byte detail, speed label via estimator.

---

#### `public void ReportDownloadStatus(string operationKey, string statusText)`

**Purpose:** Status line without byte progress; resets transfer row.

---

#### `public void CompleteDownload(string operationKey, string statusText)`

**Purpose:** **`FinishDownload`** with success severity, progress **1.0**.

---

#### `public void FailDownload(string operationKey, string statusText)`

**Purpose:** **`FinishDownload`** with error severity.

---

#### `public IProgress<DownloadProgress> CreateDownloadProgressBridge(string operationKey, IProgress<DownloadProgress>? innerProgress = null)`

**Purpose:** Forwards to notification + optional inner progress.

---

#### `public void Dismiss(AppNotification notification)`

**Purpose:** Removes one row.

---

#### `public void DismissAll()`

**Purpose:** Clears collection.

---

#### `public static string BuildOperationKey(string sourceKind, string sourceId)`

**Purpose:** **`{kind}:{id}`** lowercased trimmed.

---

## Private methods — `NotificationCenter`

#### `private void FinishDownload(string operationKey, AppNotificationSeverity severity, string statusText, double finalProgress)`

**Purpose:** Completes download card, clears detail, publishes toast.

---

#### `private void UpsertNotification(...)`

**Purpose:** Marshals to main thread → **`UpsertNotificationImpl`**.

---

#### `private void UpsertNotificationImpl(...)`

**Purpose:** Insert/update, **`Resort`**, **`TrimOldNotifications`**, debounced save, optional **`NotificationPublished`**.

---

#### `private AppNotification? FindNotification(string id)`

**Purpose:** Case-insensitive id lookup.

---

#### `private void Resort(AppNotification notification)`

**Purpose:** **`ObservableCollection.Move`** to preserve newest **`UpdatedAt`** without remove+insert flicker.

---

#### `private void TrimOldNotifications()`

**Purpose:** Caps at **`MaxPersistedNotifications`** from tail.

---

#### `private async Task LoadAsync()`

**Purpose:** Deserializes **`NotificationStore`** into **`AppNotification`** instances.

---

#### `private void QueueSave(int delayMs)`

**Purpose:** Debounced background **`SaveSnapshotAsync`** (skipped until initialized).

---

#### `private async Task SaveSnapshotAsync(CancellationToken ct)`

**Purpose:** Main-thread snapshot → JSON file.

---

#### `private static NotificationStoreItem CreateStoreItem(AppNotification notification)`

**Purpose:** DTO mapping for persistence.

---

#### `private static void RunOnMainThread(Action action)`

**Purpose:** Inline or **`MainThread.BeginInvokeOnMainThread`**.

---

#### `private void RaiseNotificationsChanged()`

**Purpose:** Fires **`NotificationsChanged`**.

---

#### `private static string BuildUpdateNotificationId(UpdateCandidate candidate)`

**Purpose:** **`update:{kind}:{id}:{remoteVersion}`** lowercased.

---

#### `private static string BuildDownloadNotificationId(string operationKey)`

**Purpose:** **`download:{operationKey}`** lowercased.

---

#### `private static string FormatBytes(long bytes)`

**Purpose:** B / KB / MB / GB labels.

---

#### `private static string GetRootDirectory()`

**Purpose:** App root above **`App`** folder.

---

## Related types (same file)

### `UpdateNotificationActionEventArgs` (class)

#### `public UpdateNotificationActionEventArgs(AppNotification notification, bool updateNow)`

#### `public AppNotification Notification { get; }`

#### `public bool UpdateNow { get; }`

**Purpose:** True = Update now; false = Update later from list.

---

### `NotificationStore` (private class)

`FileVersion` (default **1**), `Notifications` list.

---

### `NotificationStoreItem` (private class)

Persisted fields: `Id`, `Category`, `Severity`, `Title`, `Message`, `StatusText`, `DetailText`, `SourceKind`, `SourceId`, `CreatedAt`, `UpdatedAt`, `IsInProgress`, `ProgressFraction`, `HasProgress`, `OffersUpdateActions`, `SuppressToastAutoDismiss`.

---

## Related

- [UpdateManager](UpdateManager/)
- [ModuleInstaller](ModuleInstaller/) — download progress bridge
- [NotificationsView](../Pages/NotificationsView/)
