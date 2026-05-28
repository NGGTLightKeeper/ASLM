---
title: "Installer-Bootstrapper"
draft: false
---

Console-free **WinExe** bootstrapper (`Installer/Installer-Bootstrapper/`) that produces the distributable **`ASLM-Installer.exe`**.

At runtime it does not install files itself; it only unpacks embedded resources and starts the MAUI installer UI.

## Embedded resources

| Resource name | Content |
| --- | --- |
| `installer-ui.zip` | Published `ASLM-Installer.exe` and dependencies |
| `aslm-payload.zip` | Full ASLM install tree to extract in the wizard |

Both are embedded via `EmbeddedResource` in `Installer-Bootstrapper.csproj` and produced by MSBuild targets in `Installer/Build/Installer.Build.targets`.

## Environment variable

| Variable | Purpose |
| --- | --- |
| `ASLM_INSTALLER_PAYLOAD_PATH` | Absolute path to extracted `aslm-payload.zip` for `InstallerService` |

Command-line arguments are forwarded to the UI process (quoted when needed).

## Implementation

- [Program](Program/) — `Installer/Installer-Bootstrapper/Program.cs`
