---
title: "Publish Binaries as Self-Contained"
date: 2026-06-06T12:32:00Z
draft: false
description: "Updated project build logic to publish .NET binaries as self-contained executables for Release builds."
---

## New Features

- **[Build System]**: The ASLM, Launcher, and Patcher projects are now configured to publish as self-contained binaries for Release builds by default. This change allows users to run the application without needing to install the .NET runtime separately.
- **[Launcher]**: The Launcher project now produces a single-file executable when published as a self-contained application, with native libraries included for self-extraction and compression enabled.

## Bug Fixes

- **[Testing]**: Fixed an issue where unit tests would run incorrectly for self-contained builds in the CI environment by skipping unit test execution when `SelfContained` is `true`.
- **[Testing]**: Ensured the test output directory is properly cleaned before building the ASLM app to prevent stale test layouts.

## API Changes

- N/A

## Known Issues

- N/A
