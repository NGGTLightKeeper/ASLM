---
title: "Fix Modules Installing"
date: 2026-06-11T18:07:00Z
draft: false
description: "Fixed issues with module installation logic where remote source installs were not properly recorded or offered, ensuring accurate update targets."
---

## New Features

- N/A

## Bug Fixes

- **[UpdateManager]**: Fixed an issue where module installations and updates might not properly recognize remote sources. The update manager now accurately determines whether a remote source install has been recorded and properly calculates when a release candidate should be offered for download.

## API Changes

- **[UpdateManager]**: Added `HasRecordedRemoteSourceInstall` to check if a successful remote source install is recorded in the module manifest.
- **[UpdateManager]**: Added `ShouldOfferReleaseInstallCandidate` to determine if a resolved release candidate should be offered for download.

## Known Issues

- N/A
