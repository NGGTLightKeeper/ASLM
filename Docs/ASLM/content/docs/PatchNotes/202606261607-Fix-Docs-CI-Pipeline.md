---
title: "Fix Docs CI Pipeline Limits"
date: 2026-06-26T16:07:00Z
draft: false
description: "Fixed GitHub Actions workflows for Jules to resolve issues with oversized API payloads and shell limits."
---

## New Features

- N/A

## Bug Fixes

- **[CI/CD]**: Fixed an issue in the `jules-update-docs.yml` and `jules-update-patchnotes.yml` GitHub Actions workflows where very large commit diffs could exceed API payload and shell limits. Oversized commit diffs and lists of changed files are now appropriately capped. Additionally, prompts are now securely written to temporary files rather than outputting to variables, preventing shell "Argument list too long" errors.

## API Changes

- N/A

## Known Issues

- N/A
