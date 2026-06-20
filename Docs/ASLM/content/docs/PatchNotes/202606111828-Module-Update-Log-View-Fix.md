---
title: "Module Update Log View Fix"
date: 2026-06-11T18:28:04Z
draft: false
description: "Fixed issues with viewing logs on the Module Update View, improving console layout and scrolling behavior."
---

## New Features

- N/A

## Bug Fixes

- **[ModuleUpdateView]**: Wrapped the update log console in a dedicated UI panel (`UpdateLogPanel`) to improve visibility and layout.
- **[ModuleUpdateView]**: Addressed an issue where the native log console host would not pin to the latest output. A dynamic session key (`UpdateLogSessionKey`) is now generated for each update session to reset the scroll position properly.
- **[ModuleUpdateView]**: Fixed layout measurement issues for the native console host by scheduling layout refresh passes during log visibility or text changes.

## API Changes

- N/A

## Known Issues

- N/A
