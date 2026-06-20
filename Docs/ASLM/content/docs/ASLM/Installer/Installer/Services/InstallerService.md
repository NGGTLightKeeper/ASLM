---
title: "InstallerService"
draft: false
---

## Class `InstallerService`

`ASLM/Installer/Installer/Services/InstallerService.cs` — **`public sealed`** — installs the embedded ASLM payload using the dual-location strategy: the Launcher and the payload archive go into the shared installation directory chosen by the user, while the application itself is installed into the current user's per-user application directory.

Records at file bottom: `InstallOptions`, `AcceptedLegalDocument`, `InstallManifest`, `InstallProgress`, `InstallPathValidation`.

---

### Constants

| Name | Value |
| --- | --- |
| `PayloadFileName` | `aslm-payload.zip` |
| `BaseArchiveFileName` | `aslm-base.zip` |
| `PayloadEnvironmentVariable` | `ASLM_INSTALLER_PAYLOAD_PATH` |
| `LauncherExeName` | `ASLM.exe` |
| `ManifestFileName` | `install-manifest.json` |
| `AppName` | `ASLM` |

---

## Public methods

#### `public string GetDefaultInstallBasePath()`

**Purpose:** Returns `Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)` as the default parent directory for the shared ASLM installation.

---

#### `public string GetUserAppDir()`

**Purpose:** Returns the per-user application directory where the ASLM application will be installed (`%LOCALAPPDATA%\ASLM`). This path is fixed and not configurable by the user.

---

#### `public InstallPathValidation ValidateInstallPath(string basePath, string folderName)`

**Purpose:** Validates and normalizes the selected shared installation directory; returns **`InstallPathValidation.Success(installPath)`** or **`.Error(message)`**.

Checks: non-empty inputs, invalid file name chars, `GetFullPath` + `ExpandEnvironmentVariables`, rooted path, `IsChildPath(base, install)`, target not existing non-empty directory.

---

#### `public async Task<InstallManifest> InstallAsync(InstallOptions options, IProgress<InstallProgress> progress, CancellationToken cancellationToken)

**Purpose:** Full install workflow using the dual-location strategy (see flow table).

| Phase | Progress / action |
| --- | --- |
| Prepare | Validate shared path and resolve user app dir; `CreateStagingPath()`; mkdir parent + staging (0%) |
| Extract | `ExtractPayloadToStagingAsync` → staging |
| Finalize | Create shared install dir, copy launcher executable from staging (88%) |
| Copy Archive | `CopyPayloadArchive` to shared install directory (90%) |
| Install App Files | `InstallUserAppDir` from staging to user app dir (92%) |
| Metadata | `WriteManifestAsync` into the shared directory |
| Shortcuts | Optional desktop (94%) / Start menu (97%) pointing to the shared launcher |
| Done | 100% + return `InstallManifest` |

On failure / Finally: `TryDeleteDirectory(staging)`.

---

#### `public void Launch(string sharedInstallPath)`

**Purpose:** Starts `{sharedInstallPath}/ASLM.exe`, `UseShellExecute = true`. Throws **`FileNotFoundException`** if launcher missing.

---

## Private methods

#### `private static async Task ExtractPayloadToStagingAsync(string stagingPath, IProgress<InstallProgress> progress, CancellationToken cancellationToken)`

**Purpose:** Opens payload via **`OpenPayloadStreamAsync`**, `ZipArchive` read, foreach entry (skip directory-only):

- Zip-slip guard: `IsChildPath(stagingPath, destinationPath)`
- Copy entry stream to file
- Progress: `6 + (index+1)/count * 80` percent

Throws if archive has no file entries.

---

#### `private static async Task<Stream> OpenPayloadStreamAsync()`

**Purpose:** Builds candidate list: env var, `BaseDirectory`, `CurrentDirectory` payloads; first existing file wins; else `FileSystem.OpenAppPackageFileAsync`; on failure **`CreatePayloadMissingException`**.

---

#### `private static void AddCandidate(List<string> candidates, string? path)`

**Purpose:** Adds normalized full path if non-empty and not duplicate (ignore case).

---

#### `private static FileNotFoundException CreatePayloadMissingException(IReadOnlyList<string> candidates)`

**Purpose:** Message lists all searched paths + hint to build bootstrapper.

---

#### `private static void CopyPayloadArchive(string sharedInstallDir)`

**Purpose:** Copies the original payload archive to the shared installation directory as `aslm-base.zip`, enabling the Launcher to bootstrap new users without re-running the installer.

---

#### `private static void InstallUserAppDir(string stagingPath, string userAppDir)`

**Purpose:** Copies all staging content except the root-level Launcher executable into the per-user application directory.

---

#### `private static bool IsRootLauncherFile(string relativePath)`

**Purpose:** Returns true when the relative path represents the root-level Launcher executable (`ASLM.exe`).

---

#### `private static string CreateStagingPath()`

**Purpose:** `%TEMP%/ASLM/InstallStaging/{guid}/`.

---

#### `private static async Task WriteManifestAsync(string sharedInstallDir, InstallManifest manifest, CancellationToken cancellationToken)`

**Purpose:** Writes `{sharedInstallDir}/install-manifest.json` via **`JsonOptions.Default`** (camelCase, indented).

---

#### `private static void TryCreateShortcut(string folderPath, string shortcutFileName, string sharedInstallDir)`

**Purpose:** Swallows exceptions from **`CreateShortcut`**.

---

#### `private static void CreateShortcut(string folderPath, string shortcutFileName, string sharedInstallDir)`

**Purpose:** COM **`WScript.Shell`**: `CreateShortcut` → set `TargetPath`, `WorkingDirectory`, `Description`, `IconLocation`, `Save`. No-op if launcher exe missing or COM unavailable.

---

#### `private static bool IsChildPath(string parentPath, string childPath)`

**Purpose:** Normalized parent/child with trailing separator; child must be under parent or equal.

---

#### `private static void TryDeleteDirectory(string path)`

**Purpose:** Best-effort recursive delete.

---

## Related types (same file)

#### `InstallOptions` (record)

`BasePath`, `FolderName`, `Version`, `AcceptedDocuments`, `CreateDesktopShortcut`, `CreateStartMenuShortcut`.

#### `AcceptedLegalDocument` (record)

`Id`, `Title`, `FileName`, `Sha256`, `AcceptedAtUtc`.

#### `InstallManifest` (record)

`App`, `Version`, `InstalledAtUtc`, `SharedInstallPath`, `UserAppPath`, `AcceptedDocuments`.

#### `InstallProgress` (record)

`Message`, `Percent` (0–100).

#### `InstallPathValidation` (record)

| Member | Description |
| --- | --- |
| `IsValid` | Success flag |
| `InstallPath` | Full install path when valid |
| `Message` | Error text when invalid |

#### `public static InstallPathValidation Success(string installPath)`

#### `public static InstallPathValidation Error(string message)`

**Purpose:** ---

## Related

- [LegalDocumentService](LegalDocumentService/)
- [Installer-Bootstrapper Program](../../Installer-Bootstrapper/Program/)
- [MainPage](../MainPage/)
