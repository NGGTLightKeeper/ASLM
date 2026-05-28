---
title: "ModuleTrustModels"
draft: false
---

## Overview

`ASLM/Models/ModuleTrustModels.cs` — trust levels and signed list payloads used by **`ModuleTrustService`** when loading modules.

---

## Enum `ModuleTrustLevel`

| Value | Meaning |
| --- | --- |
| `Official` | Id/repo matches built-in official catalog |
| `CommunityReviewed` | On signed remote reviewed list |
| `Unreviewed` | No verification |

---

## `OfficialModuleTrustEntry`

Immutable **`(Id, Repo)`** — ids/repos normalized via **`ModuleTrustIdentity`**.

---

## `ReviewedModuleTrustEntry`

JSON DTO: **`id`**, **`repo`** — normalized on **`Normalize()`**.

---

## `ModuleTrustSourceConfig`

Shipped config for fetching the reviewed list:

| Property | JSON | Default |
| --- | --- | --- |
| `FileVersion` | `fileVersion` | `1` |
| `ReviewedListUrl` | `reviewedListUrl` | |
| `PublicKeyBase64` | `publicKeyBase64` | Ed25519 verify key |
| `RefreshIntervalHours` | `refreshIntervalHours` | `24` |

---

## `SignedReviewedModulesPayload`

Remote signed document: **`fileVersion`**, **`issuedAt`**, **`modules`**, **`signature`**.

**`ToUnsignedBody()`** — canonical body for signature verification.

---

## `ReviewedModulesCacheDocument`

Local cache: **`fetchedAt`**, **`payload`**, **`signature`**.

---

## Static `ModuleTrustIdentity`

| Method | Role |
| --- | --- |
| `NormalizeId` / `NormalizeRepo` | Lowercase trim for comparison |
| `Matches(ModuleConfig, id, repo)` | Official entry check |
