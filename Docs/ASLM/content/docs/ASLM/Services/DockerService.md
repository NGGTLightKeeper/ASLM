---
title: "DockerService"
draft: false
---

## Class `DockerService`

`ASLM/Services/DockerService.cs` — **`public sealed`** — detects whether the Docker CLI is installed and opens installation documentation.

Does **not** require a running Docker daemon.

**DI:** `AddSingleton<DockerService>()`.

---

### Constants

| Name | Value |
| --- | --- |
| `WindowsInstallUrl` | `https://www.docker.com/` |
| `CliTimeoutSeconds` | `5` (private) |

---

## Public methods

#### `public bool IsCheckRequiredOnThisPlatform()`

**Purpose:** Returns whether Docker CLI checks apply on the current operating system.

Returns **`OperatingSystem.IsWindows()`**.

---

#### `public Task<bool> IsCliInstalledAsync(CancellationToken ct = default)

**Purpose:** Returns whether **`docker`** is available on PATH.

| Step | Action |
| --- | --- |
| 1 | If `!IsCheckRequiredOnThisPlatform()` → **`Task.FromResult(true)`** |
| 2 | `Task.Run(() => IsCliInstalledCore(ct), ct)` |

---

#### `public async Task OpenInstallGuideAsync()`

**Purpose:** Opens the Docker installation documentation in the system browser.

`await Launcher.Default.OpenAsync(new Uri(WindowsInstallUrl))`.

---

## Private methods

#### `private static bool IsCliInstalledCore(CancellationToken ct)

**Purpose:** Runs **`docker --version`** and returns whether the command succeeds.

| Step | Action |
| --- | --- |
| 1 | `RunDocker(["--version"], ct)` |
| 2 | Return **`ExitCode == 0`** |
| On cancel | Rethrow `OperationCanceledException` |
| On other error | **`false`** |

---

#### `private static (int ExitCode, string Stdout, string Stderr) RunDocker(IReadOnlyList<string> args, CancellationToken ct)

**Purpose:** Starts one Docker CLI process and captures exit code and output streams.

| Step | Action |
| --- | --- |
| 1 | `ProcessStartInfo`: `FileName = "docker"`, quoted args, redirect stdout/stderr, no window |
| 2 | `process.Start()` — on failure or `Win32Exception` → **`(-1, "", "")`** |
| 3 | Read stdout/stderr async |
| 4 | `WaitForExit(CliTimeoutSeconds)` — on timeout: kill tree (best effort) → **`(-1, "", "")`** |
| 5 | Return `(ExitCode, stdout, stderr)` |

---

## Related

- [SetupWizardPage](../Pages/SetupWizardPage/) — optional Docker step
