---
title: "ModuleDependencyResolver"
draft: false
---

## Overview

`ASLM/Services/ModuleDependencyResolver.cs` — **`public static`** — resolves declared module-to-module dependencies from manifests. Provides utility methods for analyzing and ordering module dependencies before installation.

---

## Public methods

#### `public static IReadOnlyList<ModuleConfig> ExpandInstallOrder(IEnumerable<ModuleConfig> selectedModules, IReadOnlyList<ModuleConfig> catalog)`

**Purpose:** Returns the selected modules plus transitive module dependencies in install order (dependencies before dependents).

**Steps:**

1. Indexes the provided `catalog` of modules by `Id`.
2. Uses a depth-first search approach (`Visit`) to traverse module dependencies.
3. Adds dependent modules to the result list only after their dependencies have been added, ensuring correct installation order.
4. Returns the deduplicated, ordered list of `ModuleConfig` objects.

---

#### `public static IReadOnlyList<string> GetDirectModuleDependencyIds(ModuleConfig module)`

**Purpose:** Returns direct module dependency ids declared by a single manifest.

**Steps:**

1. Iterates over `Dependencies.Modules` in the given `module`.
2. Trims and filters out empty identifiers.
3. Returns a distinct, case-insensitive list of dependency string identifiers.

---

## Related

- [ModuleConfig](../Models/ModuleConfig/)
- [SetupWizardPage](../Pages/SetupWizardPage/)
