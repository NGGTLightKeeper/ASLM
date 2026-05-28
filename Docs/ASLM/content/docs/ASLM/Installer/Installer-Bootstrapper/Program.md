---
title: "Program"
draft: false
---

## Class `Program`

`Installer/Installer-Bootstrapper/Program.cs` — namespace **`ASLM.Installer.Bootstrapper`** — **`internal static`** — single-file bootstrapper entry.

---

### Constants

| Name | Value |
| --- | --- |
| `InstallerZipResourceName` | `installer-ui.zip` |
| `InstallerExecutableName` | `ASLM-Installer.exe` |
| `PayloadFileName` | `aslm-payload.zip` |
| `PayloadEnvironmentVariable` | `ASLM_INSTALLER_PAYLOAD_PATH` |

---

## Private methods
#### `private static int Main(string[] args)`

**Purpose:** `[STAThread]` entry.

| Step | Action |
| --- | --- |
| 1 | `extractionPath = %TEMP%/ASLM/Installer/{utc}-{guid}/` |
| 2 | `Directory.CreateDirectory` |
| 3 | `ExtractInstallerUi(extractionPath)` |
| 4 | `payloadPath = ExtractEmbeddedPayload(extractionPath)` |
| 5 | Verify `ASLM-Installer.exe` and payload exist |
| 6 | `Process.Start` UI with quoted forwarded `args`, `WorkingDirectory = extractionPath`, env **`ASLM_INSTALLER_PAYLOAD_PATH`** = payload path |
| 7 | `WaitForExit()`; return exit code |
| 8 | On exception → `ShowError(ex)`; return `1` |
| 9 | `finally` → `TryDeleteDirectory(extractionPath)` |

Payload path is not passed on the command line; the MAUI installer reads the environment variable.

---

#### `private static void ExtractInstallerUi(string extractionPath)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `OpenInstallerZipResource()` → `ZipArchive` |
| 2 | For each entry with non-empty `Name` |
| 3 | Resolve destination; **`IsChildPath`** zip-slip guard |
| 4 | `ExtractToFile(..., overwrite: true)` |

---

#### `private static string ExtractEmbeddedPayload(string extractionPath)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `payloadPath = {extractionPath}/aslm-payload.zip` |
| 2 | `OpenRequiredResource(PayloadFileName)` → copy to file |
| 3 | Return `payloadPath` |

---

#### `private static Stream OpenInstallerZipResource()`

**Purpose:** Returns **`OpenRequiredResource(InstallerZipResourceName)`**.

---

#### `private static Stream OpenRequiredResource(string resourceName)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)` |
| 2 | If found → return stream |
| 3 | Else throw `InvalidOperationException` listing all embedded resource names (hint: build bootstrapper in Visual Studio) |

---

#### `private static string QuoteArgument(string argument)`

**Purpose:** Wraps in `"..."` with escaped `"` if argument contains space or `"`; otherwise returns as-is.

---

#### `private static bool IsChildPath(string parentPath, string childPath)`

**Purpose:** Normalizes parent/child with trailing directory separator; child must start with parent (ordinal ignore case).

---

#### `private static void ShowError(Exception ex)`

**Purpose:** `System.Windows.Forms.MessageBox.Show` with title **ASLM Installer**. Swallows secondary failures (WinExe has no console).

---

#### `private static void TryDeleteDirectory(string path)`

**Purpose:** Best-effort recursive delete if directory exists; ignores errors (UI may briefly lock files).

---

## Related

- [InstallerService](../Installer/Services/InstallerService/) — reads `ASLM_INSTALLER_PAYLOAD_PATH`
- [Installer _index](../_index/)
