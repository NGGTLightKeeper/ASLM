---
title: "UpdateScheduler"
draft: false
---

## Class `UpdateScheduler`

`ASLM/Services/UpdateScheduler.cs` — **`public sealed`** — background loop for periodic update checks and optional auto-apply per [AppDataStore](AppDataStore/) **`Updates`** settings.

**DI:** `AddSingleton<UpdateScheduler>()` — [UpdateManager](UpdateManager/), [GitHubRateLimitStore](GitHubRateLimitStore/).

Implements **`IDisposable`**.

---

### Constants (private static)

| Name | Value |
| --- | --- |
| `StartupDelay` | 15 seconds |
| `FailureRetryDelay` | 15 minutes |
| `IdlePollDelay` | 1 minute |
| `BudgetExhaustedPadding` | 10 seconds |

---

## Public methods

#### `public UpdateScheduler(AppDataStore appData, UpdateManager updateManager, GitHubRateLimitStore rateLimitStore, ILogger<UpdateScheduler> logger)`

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

**Purpose:** After **`StartupDelay`**: loop **`RunSchedulerPassAsync`**, delay using returned time (re-read each pass), on failure log and wait **`FailureRetryDelay`**.

---

#### `private async Task<TimeSpan> RunSchedulerPassAsync(CancellationToken ct)`

**Purpose:** Executes one scheduler pass and returns the delay before the next pass. Populates check queue via `UpdateManager.DiscoverInstalledModulesAsync()`, respects `GitHubRateLimitStore.CanMakeAutoRequest()`, and processes items sequentially via `ProcessNextItemAsync`.

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
