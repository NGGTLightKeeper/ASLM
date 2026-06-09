---
title: "ModuleDependencyResolverTests"
draft: false
---

## Overview

`ASLM/Tests/Services/ModuleDependencyResolverTests.cs` — unit tests for the `ModuleDependencyResolver` service, validating the dependency resolution and sorting logic.

---

## Test methods

#### `ExpandInstallOrder_places_dependencies_before_dependents`

**Purpose:** Validates that `ExpandInstallOrder` correctly sorts dependencies such that required modules are placed before the modules that depend on them.

**Steps:**

1. Creates a dependency module (`aslm-chat`).
2. Creates a dependent module (`aslm-code`) which requires `aslm-chat`.
3. Calls `ModuleDependencyResolver.ExpandInstallOrder`.
4. Asserts that the resulting order places `aslm-chat` before `aslm-code`.

---

#### `ExpandInstallOrder_deduplicates_shared_dependencies`

**Purpose:** Validates that `ExpandInstallOrder` correctly deduplicates dependencies when multiple selected modules depend on the same underlying module.

**Steps:**

1. Creates a shared dependency module (`aslm-chat`).
2. Creates a dependent module (`aslm-code`) which requires `aslm-chat`.
3. Creates another dependent module (`other-module`) which also requires `aslm-chat`.
4. Calls `ModuleDependencyResolver.ExpandInstallOrder`.
5. Asserts that the resulting order contains exactly one instance of `aslm-chat` placed before both dependent modules.

---

#### `GetDirectModuleDependencyIds_returns_trimmed_unique_ids`

**Purpose:** Validates that `GetDirectModuleDependencyIds` returns a unique list of trimmed identifiers, properly resolving duplicate and untrimmed declarations within a single manifest.

**Steps:**

1. Creates a module with duplicated and differently spaced dependencies on `aslm-chat`.
2. Calls `ModuleDependencyResolver.GetDirectModuleDependencyIds`.
3. Asserts that the resulting list contains exactly one properly trimmed identifier `aslm-chat`.

---

## Related

- [ModuleDependencyResolver](../../Services/ModuleDependencyResolver/)
- [ModuleConfigBuilder](../../TestSupport/ModuleConfigBuilder/)