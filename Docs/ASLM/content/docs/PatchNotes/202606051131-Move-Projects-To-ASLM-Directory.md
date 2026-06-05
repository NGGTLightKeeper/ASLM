---
title: "Move Projects To ASLM Directory"
date: 2026-06-05T11:31:00Z
draft: false
description: "Installer, Launcher, and Patcher projects have been moved to the ASLM directory, and documentation paths updated to reflect the new structure."
---

## New Features

- **[Project Structure]**: Moved the `Installer`, `Launcher`, and `Patcher` projects into the main `ASLM/` directory for better organization and consistency.
- **[Documentation]**: Updated documentation mappings and MSBuild paths to reflect the new nested directory structure (`ASLM/Installer/`, `ASLM/Launcher/`, `ASLM/Patcher/`).
- **[Build System]**: The root `ASLM.slnx` solution file and `ASLM.csproj` references have been updated to target the new project locations.

## Bug Fixes

- N/A

## API Changes

- N/A

## Known Issues

- N/A
