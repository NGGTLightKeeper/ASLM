---
title: "UpdateManagerInstallStateTests"
draft: false
---

## Class `UpdateManagerInstallStateTests`

`ASLM/Tests/Services/UpdateManagerInstallStateTests.cs` — tests `HasRecordedRemoteSourceInstall`, `ShouldOfferReleaseInstallCandidate`, and `IsModuleAlreadyAtInstallTarget` in [UpdateManager](../../Services/UpdateManager/) to verify update installation state checks.

---

## Test methods

#### `public void HasRecordedRemoteSourceInstall_returns_false_for_release_module_without_installed_tag()`

**Purpose:** Verifies that a release-tracked module without a recorded installed tag correctly returns false.

---

#### `public void HasRecordedRemoteSourceInstall_returns_true_when_installed_release_tag_is_set()`

**Purpose:** Verifies that a release-tracked module with a recorded installed tag correctly returns true.

---

#### `public void HasRecordedRemoteSourceInstall_returns_false_for_branch_module_without_commit_sha()`

**Purpose:** Verifies that a branch-tracked module without a recorded installed commit SHA correctly returns false.

---

#### `public void HasRecordedRemoteSourceInstall_returns_true_when_installed_commit_sha_is_set()`

**Purpose:** Verifies that a branch-tracked module with a recorded installed commit SHA correctly returns true.

---

#### `public void ShouldOfferReleaseInstallCandidate_returns_true_when_version_matches_but_source_not_recorded()`

**Purpose:** Verifies that an installation candidate should be offered if no remote source is recorded, even if the version matches.

---

#### `public void ShouldOfferReleaseInstallCandidate_returns_false_when_installed_release_tag_matches()`

**Purpose:** Verifies that an installation candidate should not be offered when the local installed release tag perfectly matches the candidate.

---

#### `public void ShouldOfferReleaseInstallCandidate_returns_true_when_installed_release_tag_differs()`

**Purpose:** Verifies that an installation candidate should be offered when the local installed release tag differs from the candidate.

---

#### `public void IsModuleAlreadyAtInstallTarget_returns_false_when_release_matches_but_source_not_recorded()`

**Purpose:** Verifies that `IsModuleAlreadyAtInstallTarget` correctly returns false when the release matches but the source install isn't explicitly recorded.

---

#### `public void IsModuleAlreadyAtInstallTarget_returns_true_when_installed_release_tag_matches_candidate()`

**Purpose:** Verifies that `IsModuleAlreadyAtInstallTarget` returns true when the installed release tag matches the update candidate.

---

#### `public void IsModuleAlreadyAtInstallTarget_returns_false_for_branch_when_installed_commit_sha_missing()`

**Purpose:** Verifies that `IsModuleAlreadyAtInstallTarget` correctly returns false for a branch-tracked candidate when the installed commit SHA is missing.

---

## Related

- [UpdateManager](../../Services/UpdateManager/)
