---
title: "Module Dependencies System"
date: 2026-06-09T20:11:00Z
draft: false
description: "Added a module-to-module dependency system to ASLM, ensuring required modules are installed and running before dependent modules start."
---

## New Features

- **[Module Dependencies System]**: Introduced the ability for ASLM modules to declare dependencies on other ASLM modules in their `ModuleConfig`. The system guarantees that all required module dependencies are installed and running prior to launching a dependent module.
- **[ModuleDependencyResolver]**: Added a new service to resolve declared module-to-module dependencies. It supports topological sorting via `ExpandInstallOrder`, ensuring dependencies are placed before dependents in the installation queue, and provides dependency deduplication.
- **[ModuleDependencyService]**: Added a new service that runs the first-time setup for any module dependencies that are not yet ready, including circular dependency detection to prevent infinite loops.
- **[Setup Wizard Enhancements]**: Updated the setup wizard to expand the user's selected modules using the new dependency resolver, guaranteeing all transitive dependencies are discovered and installed during the initial setup flow.
- **[Module Launch Coordinator]**: The coordinator now correctly starts any declared dependency modules before starting the dependent module.

## Bug Fixes

- N/A

## API Changes

- **[ModuleConfig]**: Added the `Modules` list under `ModuleDependencies` to allow declaring module-to-module dependencies. Added `ModuleModuleDependency` to define the stable identifier (`Id`) of the required module.
- **[ModuleRunner]**: The constructor now requires an `IServiceProvider` to lazily resolve the `ModuleDependencyService`. The `ExecuteFirstRunAsync` method has been updated to accept an optional `skipModuleDependencies` parameter.
- **[MauiProgram]**: Registered `ModuleDependencyService` as a singleton in the DI container.

## Known Issues

- N/A
