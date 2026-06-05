---
title: "Installer"
draft: false
---

The **Installer** solution (`ASLM/Installer/`) ships a Windows setup experience for ASLM. It consists of two projects:

| Project | Output | Role |
| --- | --- | --- |
| [Installer-Bootstrapper](Installer-Bootstrapper/) | Single-file `ASLM-Installer.exe` (repo `Build/`) | Extracts embedded UI + payload, starts the MAUI wizard |
| [Installer](Installer/) | `ASLM-Installer.exe` + assets | Wizard UI, legal review, path selection, file extraction |

## End-user flow

1. User runs **`ASLM-Installer.exe`** (bootstrapper).
2. Bootstrapper unpacks `installer-ui.zip` and `aslm-payload.zip` to a temp folder under `%TEMP%/ASLM/Installer/`.
3. Bootstrapper sets **`ASLM_INSTALLER_PAYLOAD_PATH`** and starts **`ASLM-Installer.exe`**.
4. MAUI wizard collects legal acceptance, install path, and options, then **`InstallerService`** extracts the payload into the chosen directory (same layout as a normal ASLM install: `ASLM.exe`, `App/`, `Patcher/`, etc.).
5. Optional desktop/Start menu shortcuts and **Launch ASLM** on finish.

## Build-time inputs

| Input | Source |
| --- | --- |
| `aslm-payload.zip` | Zipped ASLM build output (`Build/{Configuration}/ASLM-{os}-{arch}/`), excluding `Installer` and `.aslm-update` |
| `legal-documents.json` | Generated from `ASLM/Installer/Legal/*.md` at build |
| `installer-ui.zip` | Published MAUI installer output (bootstrapper embed) |

Legal source files live in `ASLM/Installer/Legal/` (EULA, privacy, license). They are not duplicated in this documentation site.

## Related distribution components

After install, the tree matches what [Launcher](../Launcher/) and [Patcher](../Patcher/) expect at the install root (`ASLM.exe`, `App/ASLM.exe`, `Patcher/`, user data under `Data/`, etc.).
