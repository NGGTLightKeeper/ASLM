---
title: "UpdateManager"
draft: false
---

## Class `UpdateManager`

`ASLM/Services/UpdateManager.cs` — **`public sealed`** — GitHub app/module updates and staged self-update.

**DI:** [AppDataStore](AppDataStore/), [ModuleInstaller](ModuleInstaller/), [ModuleRunner](ModuleRunner/), [ModuleTrustService](ModuleTrustService/), [GitHubUpdateClient](GitHubUpdateClient/), [NotificationCenter](NotificationCenter/).

---

### Properties

| Member | Description |
| --- | --- |
| `HasPendingAppUpdate` | Whether a prepared ASLM self-update is waiting for restart |
| `CurrentAppVersion` | Installed app release tag from [AppData](../Models/AppData/) |

---

## Public methods

#### `public UpdateManager( AppDataStore appData, ModuleInstaller moduleInstaller, ModuleRunner moduleRunner, ModuleTrustService moduleTrustService, GitHubUpdateClient github, NotificationCenter notifications, ILogger<UpdateManager> logger)`

**Purpose:** Creates the update manager and stores dependencies.

---

#### `public async Task<AppUpdateSourceConfig?> LoadAppUpdateSourceAsync(CancellationToken ct = default)`

**Purpose:** Loads the shipped ASLM update source configuration.

---

#### `public async Task<UpdateCandidate?> CheckAppUpdateAsync( CancellationToken ct = default, bool publishUpdateNotification = true)`

**Purpose:** When false, skips publishing an update-available notification (used when auto-updates will apply immediately).

---

#### `public async Task<UpdateCandidate?> CheckModuleUpdateAsync( ModuleConfig module, CancellationToken ct = default, bool publishUpdateNotification = true)`

**Purpose:** When false, skips publishing an update-available notification (used when auto-updates will apply immediately).

---

#### `public async Task<UpdateCandidate?> ResolveModuleInstallCandidateAsync( ModuleConfig module, CancellationToken ct = default)`

**Purpose:** Resolves the concrete install target that should be used for a module during setup.

---

#### `public async Task<List<UpdateCandidate>> GetModuleReleaseCandidatesAsync( ModuleConfig module, CancellationToken ct = default)`

**Purpose:** ---

#### `public async Task<List<UpdateCandidate>> CheckAllUpdatesAsync( CancellationToken ct = default, bool publishUpdateNotifications = true)`

When false, skips publishing update-available notifications for every discovery (used when auto-updates will apply immediately).

---

#### `public async Task ApplyDiscoveredUpdatesAsync( IReadOnlyList<UpdateCandidate> updates, IProgress<string>? log, CancellationToken ct = default)`

**Purpose:** ASLM self-update prepared for the next launcher start when not already staged.

---

#### `public Task<List<GitHubBranchInfo>> GetModuleBranchesAsync(ModuleConfig module, CancellationToken ct = default)`

**Purpose:** ---

#### `public void SaveModuleUpdatePreferences(ModuleConfig module)`

Saves one module manifest after update preferences changed in UI.

---

#### `public bool TryRestorePendingUpdateCandidate(ModuleConfig module, [NotNullWhen(true)] out UpdateCandidate? candidate)`

**Purpose:** persisted snapshot still reflects an update newer than the local installation.

---

#### `public async Task<bool> PrepareAppUpdateAsync( UpdateCandidate candidate, IProgress<string>? log = null, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)`

**Purpose:** Downloads and stages an ASLM app update for the external patcher.

---

#### `public async Task<bool> ApplyModuleUpdateAsync( UpdateCandidate candidate, IProgress<string>? log = null, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)`

**Purpose:** Downloads and applies one module update.

---

## Private methods

#### `private static bool IsValidAppGitHubSource(AppUpdateSourceConfig? source)`

**Purpose:** ---

#### `private string? GetConfiguredAppAssetName(AppUpdateSourceConfig source)`

---

#### `private async Task<UpdateCandidate?> CheckModuleBranchUpdateAsync(ModuleConfig module, CancellationToken ct)`

**Purpose:** Checks whether a branch-tracked module has moved to a newer commit.

---

#### `private async Task<UpdateCandidate?> ResolveModuleBranchInstallCandidateAsync( ModuleConfig module, CancellationToken ct)`

**Purpose:** Resolves the branch install target selected by the module manifest.

---

#### `private async Task<UpdateCandidate?> CheckModuleReleaseUpdateAsync(ModuleConfig module, CancellationToken ct)`

**Purpose:** Checks whether a release-tracked module has a newer GitHub release.

---

#### `private async Task<UpdateCandidate?> ResolveModuleReleaseInstallCandidateAsync( ModuleConfig module, CancellationToken ct)`

**Purpose:** Resolves the release install target selected by the module manifest.

---

#### `private static string ResolveModuleDownloadUrl(ModuleConfig module, GitHubReleaseInfo release)`

**Purpose:** Resolves the archive URL used to update one module release.

---

#### `private static UpdateCandidate BuildModuleReleaseCandidate( ModuleConfig module, GitHubReleaseInfo release, string downloadUrl)`

**Purpose:** Builds one release-backed module update candidate from a GitHub release payload.

---

#### `private async Task DownloadModuleArchiveAsync( ModuleConfig module, UpdateCandidate candidate, string archivePath, IProgress<string>? log, IProgress<DownloadProgress>? progress, CancellationToken ct)`

**Purpose:** Downloads the archive for one module update candidate.

---

#### `private static string FindUpdatedModuleManifest(string extractDir)`

**Purpose:** Locates the module manifest inside one extracted archive.

---

#### `private async Task<ModuleConfig?> LoadModuleConfigFromPathAsync(string path, CancellationToken ct)`

**Purpose:** Loads one module configuration directly from a specific manifest path.

---

#### `private ModuleConfig? LoadModuleConfigFromPath(string path)`

**Purpose:** Loads one module configuration directly from a specific manifest path synchronously.

---

#### `private async Task<bool> ApplyPreparedModuleUpdateAsync( ModuleConfig module, UpdateCandidate candidate, PreparedModuleUpdate preparedUpdate, string moduleDir, bool wasEnabled, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Applies a prepared module update while keeping the heavy file operations off the UI thread.

---

#### `private static void MergeModuleState(ModuleConfig oldConfig, ModuleConfig newConfig, UpdateCandidate candidate)`

**Purpose:** Carries forward user state and update metadata from the old module into the new manifest.

---

#### `private static bool IsPrereleaseChannel(string? channel)`

**Purpose:** ---

#### `private static bool IsPrereleaseMode(string? mode)`

---

#### `private static string ResolveCurrentAppAssetKey()`

**Purpose:** Resolves the current application asset key from OS and architecture.

---

#### `private static string BuildModuleCurrentVersion(ModuleConfig module)`

**Purpose:** ---

#### `private static string ResolveInstalledModuleReleaseTag(ModuleConfig module)`

---

#### `internal static bool HasRecordedRemoteSourceInstall(ModuleConfig module)`

**Purpose:** Returns whether the module manifest records a successful remote source install.

**Parameters:**

- `module`: The `ModuleConfig` to check.

**Returns:** `true` if a remote source install is recorded; otherwise, `false`.

**Example:**

```csharp
bool hasInstall = UpdateManager.HasRecordedRemoteSourceInstall(module);
```

---

#### `internal static bool ShouldOfferReleaseInstallCandidate(ModuleConfig module, string resolvedReleaseTag)`

**Purpose:** Returns whether a resolved release install candidate should be offered for download.

**Parameters:**

- `module`: The `ModuleConfig` to evaluate.
- `resolvedReleaseTag`: The resolved release tag.

**Returns:** `true` if the candidate should be offered; otherwise, `false`.

**Example:**

```csharp
bool shouldOffer = UpdateManager.ShouldOfferReleaseInstallCandidate(module, "0.7.1.8");
```

---

#### `internal static bool IsModuleAlreadyAtInstallTarget(ModuleConfig module, UpdateCandidate candidate)`

**Purpose:** Returns whether the install candidate already matches the local installation (no file work needed).

#### `private static string ResolveCurrentAppDisplayVersion()`

Resolves the local ASLM version label shown in settings when no GitHub tag is known yet.

---

## Public methods

#### `public string? TryGetPendingPreparedAppVersion()`

**Purpose:** ---

## Private methods

#### `private bool ShouldOfferAppUpdate(string releaseTag)`

**Purpose:** ---

#### `private static string? MergeInstalledAndPendingReleaseBaselines(string? installedReleaseTag, string? pendingVersion)`

---

#### `private string? TryReadPendingAppUpdateVersion()`

**Purpose:** Reads the target tag stored with a prepared ASLM self-update without failing callers when the file is invalid.

---

#### `internal static bool IsLatestReleaseSelection(string? selectedReleaseTag)`

**Purpose:** ---

#### `private static string ShortSha(string sha)`

---

#### `private static string BuildReleaseDisplayName(GitHubReleaseInfo release)`

**Purpose:** Builds the compact picker label shown for one release candidate.

---

#### `private static void ExtractZipSafe(string zipPath, string destination)`

**Purpose:** Extracts one ZIP archive and rejects entries that escape the target directory.

---

#### `private static string ResolveSinglePayloadDirectory(string extractDir)`

**Purpose:** Collapses archives that unpack into a single wrapper folder.

---

#### `private static HashSet<string> BuildPreservePathSet(IEnumerable<string> relativePaths)`

**Purpose:** Builds the normalized set of paths that must stay in place during module replacement.

---

#### `private static void ReportPreservedPaths( string moduleRoot, IReadOnlySet<string> preservePaths, IProgress<string>? log)`

**Purpose:** Logs preserved paths without traversing their contents.

---

#### `private static void CopyDirectory( string sourceDir, string destDir, IReadOnlySet<string> preservePaths, string relativeRoot = "")`

**Purpose:** Recursively copies one directory into another while skipping preserved module paths.

---

#### `private static void ClearDirectory(string directory, IReadOnlySet<string> preservePaths)`

**Purpose:** Removes every non-preserved file and directory inside one root directory.

---

#### `private static void ClearDirectory( string currentDir, string rootDir, IReadOnlySet<string> preservePaths)`

**Purpose:** Removes non-preserved contents from one directory while preserving declared descendants.

---

#### `private static async Task ClearDirectoryForUpdateAsync( string directory, IReadOnlySet<string> preservePaths, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Clears a module directory with retries for Windows file locks from recently stopped processes.

---

#### `private static bool IsTransientFileAccessException(Exception ex)`

**Purpose:** ---

#### `private static async Task<int> StopProcessesRunningFromDirectoryAsync( string directory, IProgress<string>? log, CancellationToken ct)`

Stops untracked or orphaned processes whose executable lives under one module directory.

---

#### `private static string? TryGetProcessExecutablePath(Process process)`

**Purpose:** ---

#### `private static bool IsPathUnderDirectory(string? path, string normalizedRoot)`

---

#### `private static async Task WaitForProcessExitAsync( Process process, TimeSpan timeout, CancellationToken ct)`

**Purpose:** Waits briefly after killing a process tree so Windows can release executable file handles.

---

#### `private string GetPendingUpdatePath()`

**Purpose:** ---

#### `private static string CombineRelativePath(string basePath, string childName)`

Combines relative path segments for module path comparisons.

---

#### `private static bool IsPreservedPath(string relativePath, IReadOnlySet<string> preservePaths)`

**Purpose:** ---

#### `private static bool HasPreservedDescendant(string relativePath, IReadOnlySet<string> preservePaths)`

---

#### `private static string NormalizeRelativePath(string relativePath)`

**Purpose:** Normalizes relative paths for cross-platform module path comparisons.

---

#### `private static string ResolveChildPath(string rootPath, string relativePath)`

**Purpose:** Resolves one relative child path and rejects directory traversal.

---

#### `private static string EnsureTrailingSeparator(string path)`

**Purpose:** Ensures directory paths always end with a separator for secure prefix checks.

---

#### `private static string SanitizeFileName(string value)`

**Purpose:** Sanitizes free-form text so it can be used as a filesystem name.

---

#### `private static void TryDeleteDirectory(string path)`

**Purpose:** Deletes one directory on a best-effort basis.

---

#### `private static string GetRootDirectory()`

**Purpose:** ---

## Public methods

#### `public static NoOpProgress<T> Instance`

**Purpose:** ---

#### `public void Report(T value)`

Ignores one reported value.

---

## Related types and nested members

#### `private sealed record PreparedModuleUpdate( string NewManifestPath, ModuleConfig? NewConfig, string ModuleSourceDir)`

**Purpose:** Carries the validated extracted module payload into the apply phase.

---

## Related

- [GitHubUpdateClient](GitHubUpdateClient/)
- [ModuleInstaller](ModuleInstaller/)
- [ModuleRunner](ModuleRunner/)
- [UpdateScheduler](UpdateScheduler/)
