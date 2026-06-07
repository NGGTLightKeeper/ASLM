---
title: "Personalization Full App Restart"
date: 2026-06-07T11:44:00Z
draft: false
description: "Updated settings to perform a full application restart through the launcher when personalization changes are made, replacing the previous module-specific restart behavior."
---

## New Features

- **Personalization App Restart**: Modifying personalization settings (such as theme or locale) now triggers a full application restart via the Launcher, ensuring all components and active modules correctly reflect the updated settings.

## Bug Fixes

- N/A

## API Changes

- Added `SettingsService.StartLauncherForApplicationRestart()` to handle clean restarts of the application process.
- Renamed `SettingsService.ResolveRootForSelfUpdate()` to `SettingsService.ResolveInstallRoot()` to better reflect its generalized purpose.
- Added optional `applyImmediately` parameter to `SettingsView.SavePersonalizationAsync(bool applyImmediately = true)`.
- Removed `SettingsView.RestartModulesForHostPersonalizationAsync` and `SettingsView.ModuleDeclaresHostPersonalizationSync` as personalization restarts are now handled at the application level.

## Known Issues

- N/A
