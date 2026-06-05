---
title: "UpdateScheduler"
draft: false
---

## Class `UpdateScheduler`

`ASLM/Services/UpdateScheduler.cs` — **`public sealed`** — background loop for periodic update checks and optional auto-apply per [AppDataStore](AppDataStore/) **`Updates`** settings.

**DI:** `AddSingleton<UpdateScheduler>()` — [UpdateManager](UpdateManager/).

Implements **`IDisposable`**.

---

### Constants (private static)

| Name | Value |
| --- | --- |
| `StartupDelay` | 15 seconds |
| `FailureRetryDelay` | 15 minutes |

---

## Public methods

#### `public UpdateScheduler(AppDataStore appData, UpdateManager updateManager, ILogger<UpdateScheduler> logger)`

**Purpose:** Stores dependencies.

---

#### `public void Start()`

**Purpose:** Starts **`Task.Run(RunAsync)`** once with internal **`CancellationTokenSource`**.

---

#### `public async Task StopAsync()`

**Purpose:** Cancels worker, awaits completion (ignores **`OperationCanceledException`**), disposes CTS.

---

#### `public void Dispose()`

**Purpose:** Cancels and disposes CTS without awaiting worker.

---

## Private methods

#### `private async Task RunAsync(CancellationToken ct)`

**Purpose:** After **`StartupDelay`**: loop **`RunDueCheckAsync`**, delay **`GetSchedulerDelay`** from settings (re-read each pass), on failure log and wait **`FailureRetryDelay`**.

---

#### `private async Task RunDueCheckAsync(CancellationToken ct)`

**Purpose:** When **`CheckEnabled`** and **`IsDue`**: stamp **`LastAutoCheckUtc`**, **`AppDataStore.SaveAsync`**, **`UpdateManager.CheckAllUpdatesAsync`** (notifications when auto-update off). When **`AutoUpdateEnabled`** and updates found: **`ApplyDiscoveredUpdatesAsync`** with logged progress.

---

#### `private static bool IsDue(AppUpdateSettings settings)`

**Purpose:** **`true`** when never checked, unparseable last check, or elapsed ≥ **`GetSchedulerDelay`**.

---

#### `private static TimeSpan GetSchedulerDelay(AppUpdateSettings settings)`

**Purpose:** **`settings.Normalize()`** then **`TimeSpan.FromHours(AutoCheckPeriodHours)`**.

---

## Related

- [UpdateManager](UpdateManager/)
- [NotificationCenter](NotificationCenter/)
- [SettingsView](../Pages/SettingsView/)
