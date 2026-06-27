---
title: "Fix Bootstrapper Build Restore Issues"
date: 2026-06-27T21:48:19Z
draft: false
description: "Resolves build issues with nested MSBuild restore tasks by injecting unique MSBuildRestoreSessionIds."
---

## New Features

- N/A

## Bug Fixes

- **[Build System]**: Fixed build failures during the Bootstrapper compilation process. Nested `Restore` tasks for `ASLM`, `Patcher`, `Launcher`, and the `Installer` projects now correctly use unique `MSBuildRestoreSessionId`s, preventing cross-project restore conflicts.

## API Changes

- N/A

## Known Issues

- N/A
