---
title: "Update NodeJS Example Module"
date: 2026-06-11T17:27:24Z
draft: false
description: "Updated the ASLM NodeJS Example Module configuration, renaming setting keys to standardize example names and updating descriptions for clarity."
---

## New Features

- **[ASLM_Module.json]**: Updated setting keys to use standardized `example-*` prefixes (e.g., `example-port`, `example-string`, `example-bool`, `example-int`, `example-number`, `example-password`, `example-select`, `example-theme`, `example-locale`) to clarify their role as generic examples.
- **[ASLM_Module.json]**: Refined and improved the descriptions for all example settings and engine flags to provide clearer context on how they are used and handled by ASLM.
- **[ASLM_Module.json]**: Bumped the module version to `1.0.0.5`.

## Bug Fixes

- **[ASLM_Module.json]**: Removed `Settings/host_theme.json` and `Settings/host_locale.json` from the `preserve` list during module updates, allowing them to be overwritten by new defaults or generated dynamically.

## API Changes

- N/A

## Known Issues

- N/A
