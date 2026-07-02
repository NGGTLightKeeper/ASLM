---
title: "Switch Python and NodeJS Engines to AMD64"
date: 2026-07-02T01:35:11Z
draft: false
description: "Updated NodeJS and Python engine configurations to download and use the AMD64 (x64) binaries instead of ARM64 versions for Windows."
---

## New Features

- N/A

## Bug Fixes

- **[Engines]**: Switched the NodeJS engine configuration (`ASLM_Engine.json`) to download the `win-x64` binary instead of `win-arm64`.
- **[Engines]**: Switched the Python engine configuration (`ASLM_Engine.json`) to download the `embed-amd64` binary instead of `embed-arm64`.

## API Changes

- N/A

## Known Issues

- N/A
