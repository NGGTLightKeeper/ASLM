---
title: "Fix Workflows Targets"
date: 2026-06-05T10:38:00Z
draft: false
description: "Updated GitHub Actions workflow triggers and conditions to prevent unnecessary runs."
---

## New Features

- N/A

## Bug Fixes

- **[CI/CD Workflows]**: Modified `.github/workflows/jules-remove-pr.yml` to only run on pull request `opened` events, removing `edited` and `reopened` triggers.
- **[CI/CD Workflows]**: Updated `.github/workflows/jules-update-docs.yml` to ignore commits from `github-actions[bot]`, instead of just ignoring commits that start with 'Merge'.
- **[CI/CD Workflows]**: Removed the redundant event check (`if: ${{ !startsWith(github.event.head_commit.message, 'Merge') }}`) from `.github/workflows/jules-update-patchnotes.yml`.
- **[CI/CD Workflows]**: Removed the `issues` event trigger (including `opened`, `edited`, and `reopened`) from `.github/workflows/tests-docs.yml`.

## API Changes

- N/A

## Known Issues

- N/A
