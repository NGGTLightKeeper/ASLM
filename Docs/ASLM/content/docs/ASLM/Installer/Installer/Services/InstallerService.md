---
title: "InstallerService"
draft: false
---

## Class `InstallerService`

`Installer/Installer/Services/InstallerService.cs` — **`public sealed`** — extracts **`aslm-payload.zip`**, writes **`install-manifest.json`**, optional shortcuts.

Records at file bottom: `InstallOptions`, `AcceptedLegalDocument`, `InstallManifest`, `InstallProgress`, `InstallPathValidation`.

---

### Constants

| Name | Value |
| --- | --- |
| `PayloadFileName` | `aslm-payload.zip` |
| `PayloadEnvironmentVariable` | `ASLM_INSTALLER_PAYLOAD_PATH` |
| `LauncherExeName` | `ASLM.exe` |
| `ManifestFileName` | `install-manifest.json` |

---

## Public methods

#### `public string GetDefaultInstallBasePath()`

**Purpose:** Returns `Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)`.

---

#### `public InstallPathValidation ValidateInstallPath(string basePath, string folderName)`

**Purpose:** Validates parent directory + folder name; returns **`InstallPathValidation.Success(installPath)`** or **`.Error(message)`**.

Checks: non-empty inputs, invalid file name chars, `GetFullPath` + `ExpandEnvironmentVariables`, rooted path, `IsChildPath(base, install)`, target not existing non-empty directory.

---

#### `public async Task<InstallManifest> InstallAsync(InstallOptions options, IProgress<InstallProgress> progress, CancellationToken cancellationToken)

**Purpose:** Full install workflow (see flow table).

| Phase | Progress / action |
| --- | --- |
| Prepare | Validate path; `CreateStagingPath()`; mkdir parent + staging (0%) |
| Extract | `ExtractPayloadAsync` → staging |
| Finalize | Delete existing install dir if any; `MoveStagingDirectory` (88%) |
| Metadata | `WriteManifestAsync` |
| Shortcuts | Optional desktop (94%) / Start menu (97%) |
| Done | 100% + return `InstallManifest` |

On failure: `TryDeleteDirectory(staging)` then rethrow.

---

#### `public void Launch(string installPath)`

**Purpose:** Starts `{installPath}/ASLM.exe`, `UseShellExecute = true`. Throws **`FileNotFoundException`** if launcher missing.

---

## Private methods

#### `private static async Task ExtractPayloadAsync(string targetPath, IProgress<InstallProgress> progress, CancellationToken cancellationToken)`

**Purpose:** Opens payload via **`OpenPayloadStreamAsync`**, `ZipArchive` read, foreach entry (skip directory-only):

- Zip-slip guard: `IsChildPath(targetPath, destinationPath)`
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

#### `private static string CreateStagingPath()`

**Purpose:** `%TEMP%/ASLM/InstallStaging/{guid}/`.

---

#### `private static void MoveStagingDirectory(string stagingPath, string installPath)`

**Purpose:** `Directory.Move` or on `IOException`: `CopyDirectory` + delete staging.

---

#### `private static void CopyDirectory(string sourcePath, string destinationPath)`

**Purpose:** Recursive directory + file copy with overwrite.

---

#### `private static async Task WriteManifestAsync(string installPath, InstallManifest manifest, CancellationToken cancellationToken)`

**Purpose:** Writes `{installPath}/install-manifest.json` via **`JsonOptions.Default`** (camelCase, indented).

---

#### `private static void TryCreateShortcut(string folderPath, string shortcutFileName, string installPath)`

**Purpose:** Swallows exceptions from **`CreateShortcut`**.

---

#### `private static void CreateShortcut(string folderPath, string shortcutFileName, string installPath)`

**Purpose:** COM **`WScript.Shell`**: `CreateShortcut` → set `TargetPath`, `WorkingDirectory`, `Description`, `IconLocation`, `Save`. No-op if launcher exe missing or COM unavailable.

---

#### `private static bool IsChildPath(string parentPath, string childPath)`

**Purpose:** Normalized parent/child with trailing separator; child must be under parent or equal.

---

#### `private static void TryDeleteDirectory(string path)`

**Purpose:** Best-effort recursive delete.

---

## Related types (same file)

### `InstallOptions` (record)

`BasePath`, `FolderName`, `Version`, `AcceptedDocuments`, `CreateDesktopShortcut`, `CreateStartMenuShortcut`.

### `AcceptedLegalDocument` (record)

`Id`, `Title`, `FileName`, `Sha256`, `AcceptedAtUtc`.

### `InstallManifest` (record)

`App`, `Version`, `InstalledAtUtc`, `InstallPath`, `AcceptedDocuments`.

### `InstallProgress` (record)

`Message`, `Percent` (0–100).

### `InstallPathValidation` (record)

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
