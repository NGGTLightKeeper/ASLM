---
title: "ReleaseTagOrdering"
draft: false
---

## Class `ReleaseTagOrdering`

`ASLM/Services/ReleaseTagOrdering.cs` — **`public static`** — normalizes and orders GitHub-style release tags consistently for ASLM and module updates.

---

## Public methods

#### `public static bool AreEquivalentVersionReferences(string left, string right)`

**Purpose:** Returns whether two local or remote version references point at the same GitHub tag identity.

| Step | Action |
| --- | --- |
| 1 | `NormalizeVersionReference` both sides |
| 2 | If neither contains `-` and both parse as `Version` → semantic equality (`1.0` == `1.0.0`) |
| 3 | Else → ordinal-ignore-case string equality |

---

#### `public static int ComparePrecedence(string? leftTag, string? rightTag)`

**Purpose:** Compares two release tags; **positive** when `leftTag` is strictly newer than `rightTag`.

| Step | Action |
| --- | --- |
| 1 | If `AreEquivalentVersionReferences` → **`0`** |
| 2 | Normalize both tags |
| 3 | Compare numeric cores via `Version.TryParse` on `ExtractSemverNumericCore` |
| 4 | If cores tie → `string.Compare` full normalized strings (pre-release suffix) |

---

#### `public static int CompareGitHubReleasesNewestFirst(GitHubReleaseInfo left, GitHubReleaseInfo right)

**Purpose:** Orders two GitHub release records so the semantically newest tag appears first.

| Step | Action |
| --- | --- |
| 1 | `-ComparePrecedence(left.TagName, right.TagName)` — newer tag first |
| 2 | Tie-break: `PublishedAt ?? CreatedAt ?? MinValue`; newer timestamp first |

---

## Private methods

#### `private static string NormalizeVersionReference(string value)`

**Purpose:** Normalizes a version or release tag while preserving pre-release identifiers and removing build metadata.

| Step | Action |
| --- | --- |
| 1 | Trim; strip leading **`v`** / **`V`** |
| 2 | Remove **`+build`** metadata (substring before first `+`) |

---

#### `private static string ExtractSemverNumericCore(string normalizedReference)`

**Purpose:** Returns the dotted numeric portion used for `Version` comparisons before any pre-release suffix.

Returns substring before first **`-`**, or full string if none.

---

## Related

- [UpdateManager](UpdateManager/)
- [GitHubUpdateClient](GitHubUpdateClient/)
