---
title: "LegalDocumentService"
draft: false
---

## Class `LegalDocumentService`

`Installer/Installer/Services/LegalDocumentService.cs` — **`public sealed`** — loads build-generated legal bundle from app assets.

### Constant

| Name | Value |
| --- | --- |
| `LegalDocumentsFileName` | `legal-documents.json` |

---

## Public methods
#### `public async Task<IReadOnlyList<LegalDocument>> LoadAsync(CancellationToken cancellationToken = default)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `FileSystem.OpenAppPackageFileAsync(LegalDocumentsFileName)` |
| 2 | `JsonSerializer.DeserializeAsync<List<LegalDocument>>(stream, JsonOptions.Default, cancellationToken)` |
| 3 | If null or empty → `InvalidOperationException("Installer legal documents are missing or empty.")` |
| 4 | Return list |

JSON is generated at build from `Installer/Legal/*.md` (`GenerateInstallerLegalDocuments` in `Installer.csproj`).

---

## Record `LegalDocument`

`public sealed record LegalDocument(...)`

| Field | Description |
| --- | --- |
| `Id` | Stable identifier |
| `Title` | Wizard heading |
| `FileName` | Source markdown file name |
| `Markdown` | Full text in `LegalDocumentEditor` |
| `Sha256` | Hash stored in install manifest |

---

## Class `JsonOptions`

`internal static class JsonOptions` — shared serializer settings for installer metadata.

### Field

#### `public static readonly JsonSerializerOptions Default`

| Option | Value |
| --- | --- |
| `PropertyNamingPolicy` | `JsonNamingPolicy.CamelCase` |
| `WriteIndented` | `true` |

Used by **`LegalDocumentService.LoadAsync`** and **`InstallerService.WriteManifestAsync`**.

---

## Related

- [InstallerService](InstallerService/)
- [MainPage](../MainPage/)
