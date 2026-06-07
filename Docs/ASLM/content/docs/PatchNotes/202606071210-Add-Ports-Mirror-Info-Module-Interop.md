---
title: "Add Ports and Mirror Info to ASLM Modules Interop"
date: 2026-06-07T12:10:00Z
draft: false
description: "Introduced a new /v1/ports endpoint and enhanced /v1/registry with ASLM API state and per-module port/host information."
---

## New Features

- **[Module Interop]**: Added a new `GET /v1/ports` endpoint to the `AslmModuleInteropServer`, which provides a lightweight response containing the ASLM API state and per-module port/host information for running modules, without the overhead of querying the installed-modules list.
- **[Module Interop]**: Enhanced the `GET /v1/registry` endpoint to include the ASLM API mirror server state (`aslmApi`) and detailed port mapping information (`pageUrl`, `hosts`) for each running module instance.
- **[Module Interop]**: Introduced `ModuleInteropPortsBuilder`, a pure utility class to assemble port and host payload data for interop API responses without side-effects.

## Bug Fixes

- N/A

## API Changes

- Added `GET /v1/ports` endpoint to the local HTTP interop API.
- Modified `GET /v1/registry` interop API response format:
  - Added the `aslmApi` object at the root level.
  - Added `pageUrl` and `hosts` lists to each object in `runningModules`.
- Added new read-only port access methods to `PortRegistry`: `TryGetAssignedPorts()`, `TryGetModulePageUrl()`, `BuildLoopbackUrl()`, and `BuildHostRouteKey()`.
- Added `GetRunningModuleConfigs()` to `ModuleRunner` for retrieving configurations of currently tracked live processes.

## Known Issues

- N/A
