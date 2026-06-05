---
title: "OllamaPersistentSettings"
draft: false
---

## Overview

`ASLM/Models/OllamaPersistentSettings.cs` — UI-facing snapshots for Ollama CLI account state (not persisted as a dedicated JSON file; populated by **`OllamaSettingsStore`**).

---

## Class `OllamaPersistentSettings`

| Property | Description |
| --- | --- |
| `IsCliAvailable` | Ollama executable found |
| `IsSignedIn` | Verified signed-in state |
| `UserName` | Account display name from CLI |

---

## Class `OllamaAccountActionResult`

| Property | Description |
| --- | --- |
| `Success` | Command succeeded |
| `Message` | Text for settings UI |
| `IsPendingVerification` | Browser sign-in started; verify asynchronously |

Returned from sign-in / sign-out operations invoked from settings.
