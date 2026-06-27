---
title: "Update GitHub Actions Versions and Timeouts"
date: 2026-06-26T17:59:20Z
draft: false
description: "Updated GitHub Actions versions across multiple workflows and increased the pipeline timeout for Hugo builds."
---

## New Features

- N/A

## Bug Fixes

- **[CI/CD]**: Updated versions of `actions/github-script` (v7 to v9), `actions/checkout` (v6 to v7), and `actions/cache` (v5 to v6) in multiple GitHub Actions workflows.
- **[CI/CD]**: Increased the pipeline timeout from 5 minutes to 10 minutes for the `hugo-build` job in `tests-docs.yml` to prevent build timeout failures.

## API Changes

- N/A

## Known Issues

- N/A
