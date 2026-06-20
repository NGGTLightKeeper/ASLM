---
title: "Python Example Module Settings Refactor"
date: "2026-06-11T17:27:02Z"
draft: false
description: "Updated ASLM-Python-Example-Module to refactor setting keys to more generic examples and removed specific settings from the preserve list."
---

## New Features

- N/A

## Bug Fixes

- N/A

## API Changes

- **[Python Example Module]**: Refactored module settings to use standard example names (`example-port`, `example-string`, `example-bool`, `example-int`, `example-number`, `example-password`, `example-select`, `example-theme`, `example-locale`) to better serve as generic references.
- **[Python Example Module]**: Removed `Settings/host_theme.json` and `Settings/host_locale.json` from the module's `preserve` list across updates, retaining only `Settings/settings.json`.
- **[Python Example Module]**: Updated setting descriptions to provide clearer guidance on type conversions and runtime behavior.
- **[Python Example Module]**: Bumped module version from `1.0.0.3` to `1.0.0.5`.

## Known Issues

- N/A
