---
title: "DockerServiceTests"
draft: false
---

## Class `DockerServiceTests`

`ASLM/Tests/Services/DockerServiceTests.cs` — platform gating for [DockerService](../../Services/DockerService/) availability checks.

---

## Test methods

#### `public void IsCheckRequiredOnThisPlatform_matches_windows_runtime()`

**Purpose:** Docker prerequisite checks run only on Windows in this build.

| Step | Action |
| --- | --- |
| 1 | `new DockerService()` |
| 2 | `IsCheckRequiredOnThisPlatform()` |
| 3 | Assert equals `OperatingSystem.IsWindows()` |

---

## Related

- [DockerService](../../Services/DockerService/)
