---
title: "GitHub Rate Limit & Updater Improvements"
date: 2026-06-12T21:40:08Z
draft: false
description: "Introduces GitHub API rate limit tracking and enhances the update scheduler to budget requests safely."
---

## New Features

- **GitHub API Rate Limit Budgeting**: Implemented a new `GitHubRateLimitStore` to track and persist GitHub API usage. This ensures ASLM stays within the GitHub API limits across application restarts by budgeting automatic requests and pausing checks if the limit is approaching.
- **Smart Update Scheduler**: The background `UpdateScheduler` has been heavily refactored. It now queues checks sequentially and respects rate limits, calculating intelligent inter-check delays to avoid exhausting the API budget. It also differentiates between automatic background checks and manual user-initiated update requests.
- **Bootstrapper Improvements**: Removed the redundant startup update check in `AppShellPage` to prevent unnecessary API hits. Rate limit tracking is now gracefully initialized during the `LoadingPage` sequence.

## Bug Fixes

- Fixed potential rate-limiting bans from GitHub by tracking API window resets and pausing automatic update checks when the budget is nearly exhausted.

## API Changes

- `GitHubUpdateClient` now requires `GitHubRateLimitStore` via constructor injection and includes a new `RefreshRateLimitAsync` method to explicitly pull current limits from GitHub.
- Methods in `UpdateManager` such as `CheckAppUpdateAsync`, `CheckModuleUpdateAsync`, `GetModuleReleaseCandidatesAsync`, and others now accept an `isManualRequest` boolean parameter to distinguish manual UI interactions from automatic background checks.
- New models `GitHubRateLimitData` and `GitHubRequestRecord` have been added to persist historical request records.

## Known Issues

- None.
