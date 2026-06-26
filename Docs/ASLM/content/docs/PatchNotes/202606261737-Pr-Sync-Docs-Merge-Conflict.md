---
title: "PR Auto-Merging Docs Workflow Bug Fix"
date: 2026-06-26T17:37:34Z
draft: false
description: "Fixed an issue in the auto-merging docs workflow where squash merge conflicts would fail the entire pipeline."
---

## New Features

- N/A

## Bug Fixes

- **[CI/CD]**: Updated the PR sync docs GitHub Actions workflow (`.github/workflows/pr-sync-docs.yml`) to properly handle squash merge conflicts. Instead of failing the entire pipeline, the workflow now prints a warning, aborts the merge, drops the conflicting branch, and continues processing the remaining branches.

## API Changes

- N/A

## Known Issues

- N/A
