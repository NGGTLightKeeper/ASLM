---
title: "Settings Display For Non-Installed Modules Fix"
date: 2026-06-11T19:44:00Z
draft: false
description: "Fixes an issue where settings for non-installed modules were incorrectly displayed in the UI."
---

## New Features

- N/A

## Bug Fixes

- **[UI]**: Fixed an issue where the settings sidebar would display options for modules that are not yet fully installed or haven't completed their first-run setup.
- **[Module Lifecycle]**: Updated the module installation and launch lifecycle (`ModuleDependencyService`, `ModuleLaunchCoordinator`, `ModuleRunner`) to explicitly set and track `Installed` and `FirstRunCompleted` statuses, ensuring more reliable module state management.
- **[Documentation]**: Updated API documentation to reflect the new module state management and settings eligibility rules.

## API Changes

- **[SettingsService]**: Added `IsModuleEligibleForSettings(ModuleConfig module)` to centralize the logic for determining if a module's settings should be displayed.

## Known Issues

- N/A
