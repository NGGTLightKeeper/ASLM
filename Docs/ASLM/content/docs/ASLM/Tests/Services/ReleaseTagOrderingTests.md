---
title: "ReleaseTagOrderingTests"
draft: false
---

## Class `ReleaseTagOrderingTests`

`ASLM/Tests/Services/ReleaseTagOrderingTests.cs` — semver equivalence, precedence, and GitHub release sort order in [ReleaseTagOrdering](../../Services/ReleaseTagOrdering/).

---

## Test methods

#### `public void AreEquivalentVersionReferences_matches_expected_pairs(string left, string right, bool expected)`

**Purpose:** Version reference equality ignores `v` prefix, build metadata, and treats prerelease tags as distinct from release.

| `left` | `right` | `expected` |
| --- | --- | --- |
| `1.0.0` | `1.0.0` | `true` |
| `v1.0.0` | `1.0.0` | `true` |
| `1.0.0+build` | `1.0.0` | `true` |
| `1.0.0` | `2.0.0` | `false` |
| `1.0.0-alpha` | `1.0.0` | `false` |
| `1.0.0-alpha` | `1.0.0-beta` | `false` |

| Step | Action |
| --- | --- |
| 1 | `AreEquivalentVersionReferences(left, right)` |
| 2 | Assert equals `expected` |

---

#### `public void ComparePrecedence_orders_tags(string left, string right, int expectedSign)`

**Purpose:** `ComparePrecedence` sign matches semver ordering (including prerelease vs release).

| `left` | `right` | `expectedSign` |
| --- | --- | --- |
| `2.0.0` | `1.0.0` | `1` |
| `1.0.0` | `2.0.0` | `-1` |
| `1.0.0` | `1.0.0` | `0` |
| `1.1.0` | `1.0.9` | `1` |
| `1.0.0-rc.1` | `1.0.0` | `1` |

| Step | Action |
| --- | --- |
| 1 | `Math.Sign(ComparePrecedence(left, right))` |
| 2 | Assert equals `expectedSign` |

---

#### `public void CompareGitHubReleasesNewestFirst_uses_publish_date_as_tie_breaker()`

**Purpose:** Same tag name sorts by `PublishedAt` when semver compares equal.

| Step | Action |
| --- | --- |
| 1 | Build `older` / `newer` both `TagName = "1.0.0"`, different `PublishedAt` |
| 2 | `CompareGitHubReleasesNewestFirst(older, newer)` positive |
| 3 | `CompareGitHubReleasesNewestFirst(newer, older)` negative |

---

#### `public void CompareGitHubReleasesNewestFirst_orders_by_semver_first()`

**Purpose:** Higher semver tag sorts before lower regardless of publish date.

| Step | Action |
| --- | --- |
| 1 | `left` tag `2.0.0`, `right` tag `1.0.0` |
| 2 | `CompareGitHubReleasesNewestFirst(left, right)` negative (newer `left` sorts first in descending comparator) |

---

## Related

- [ReleaseTagOrdering](../../Services/ReleaseTagOrdering/)
