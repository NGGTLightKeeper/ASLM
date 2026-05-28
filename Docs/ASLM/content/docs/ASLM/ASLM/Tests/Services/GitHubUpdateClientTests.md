---
title: "GitHubUpdateClientTests"
draft: false
---

## Class `GitHubUpdateClientTests`

`ASLM/Tests/Services/GitHubUpdateClientTests.cs` — guard clauses on [GitHubUpdateClient](../../Services/GitHubUpdateClient/) when repo slug is blank (no live HTTP).

---

## Test methods

#### `public async Task GetReleasesAsync_returns_empty_for_blank_repo()`

**Purpose:** Whitespace-only repo string yields an empty release list without calling GitHub.

| Step | Action |
| --- | --- |
| 1 | `GetReleasesAsync("  ", includePrerelease: true)` |
| 2 | Assert `releases` empty |

---

#### `public async Task GetBranchesAsync_returns_empty_for_blank_repo()`

**Purpose:** Empty repo string yields no branches.

| Step | Action |
| --- | --- |
| 1 | `GetBranchesAsync(string.Empty)` |
| 2 | Assert `branches` empty |

---

#### `public async Task GetLatestReleaseAsync_returns_null_for_blank_repo()`

**Purpose:** Empty repo string yields no latest release.

| Step | Action |
| --- | --- |
| 1 | `GetLatestReleaseAsync(string.Empty, includePrerelease: false)` |
| 2 | Assert `release` is `null` |

---

## Related

- [GitHubUpdateClient](../../Services/GitHubUpdateClient/)
