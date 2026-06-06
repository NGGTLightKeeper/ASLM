---
title: "Update Installation Paths"
date: 2026-06-06T13:56:00Z
draft: false
description: "Introduced a dual-location installation strategy to support per-user setups, updated the installer to handle shared and local application data, and refactored the launcher and patcher."
---

## New Features

- **[Installation Layout]**: Introduced a dual-location strategy. The Launcher and payload archive (`aslm-base.zip`) now reside in a shared installation directory. The application (`App\`, `Patcher\`, `Data\`, etc.) is now bootstrapped into the user's per-user application directory (`%LOCALAPPDATA%\ASLM` on Windows).
- **[Launcher Bootstrapper]**: Added `UserBootstrapper.cs` to the Launcher. It extracts the `aslm-base.zip` archive into the per-user application directory for new users transparently on launch.
- **[AppPaths Utility]**: Added `AppPaths.cs` to resolve paths consistently across both the legacy Monolithic (Debug) layout and the new Dual-location (Release) layout across platforms.
- **[Installer Flow]**: Updated the installer flow to create temporary staging directories, place the Launcher and payload archive in the shared directory, copy application files per-user, and correctly configure Windows shortcuts.

## Bug Fixes

- N/A

## API Changes

- **[InstallerService]**: `GetDefaultInstallBasePath()` now specifically refers to the shared installation directory. Added `GetUserAppDir()` to determine the per-user AppData directory.
- **[InstallerService]**: `InstallAsync` now implements the dual-location placement logic, moving away from a monolithic directory structure.
- **[Installer Manifest]**: Modified `InstallManifest` to store `SharedInstallPath` and `UserAppPath` instead of a single `InstallPath`.
- **[Launcher Execution]**: Restructured argument forwarding in `Program.cs` and added a `--launcher` CLI argument to explicitly tell the Patcher where the shared launcher executable is located.
- **[Patcher Resolution]**: Replaced hardcoded launcher paths in the Patcher with `ResolveLauncherPath()`, leveraging the new `--launcher` argument or `launcher-ref.json` created by the Launcher.

## Known Issues

- N/A
