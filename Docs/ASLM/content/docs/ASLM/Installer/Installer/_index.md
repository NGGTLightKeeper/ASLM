---
title: "Installer (MAUI)"
draft: false
---

**.NET MAUI** Windows wizard (`Installer/Installer/`, assembly **`ASLM-Installer`**). Presents legal documents, install location, confirmation, and progress, then delegates file work to **`InstallerService`**.

## Wizard steps (dynamic)

| Step index | View | Content |
| --- | --- | --- |
| `0` | Welcome | Introduction |
| `1 … N` | Legal | One step per entry in `legal-documents.json` |
| `PathStep` | Path | Base directory, folder name, shortcuts |
| `ConfirmStep` | Confirm | Summary before install |
| `InstallStep` | Install | Progress bar and status |

`PathStep`, `ConfirmStep`, and `InstallStep` are computed from `_legalDocuments.Count` so adding legal files does not require code changes.

## Services

| Type | Page |
| --- | --- |
| `InstallerService` | [InstallerService](Services/InstallerService/) |
| `LegalDocumentService` | [LegalDocumentService](Services/LegalDocumentService/) |

Registered in [MauiProgram](MauiProgram/) as singletons; **`MainPage`** currently constructs its own service instances for the wizard lifecycle.

## UI entry points

| Component | Page |
| --- | --- |
| MAUI bootstrap | [MauiProgram](MauiProgram/) |
| Application window | [App](App/) |
| Wizard UI | [MainPage](MainPage/) |
| WinUI host | [App (Windows)](Platforms/Windows/App/) |
| Folder picker | [WindowsFolderPicker](Platforms/Windows/WindowsFolderPicker/) |

## Output artifact

Writes **`install-manifest.json`** at the install root (accepted legal documents, version, paths, timestamps).
