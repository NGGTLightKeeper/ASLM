// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text.Json;

namespace ASLM.Installer;

// Legal document loading.

/// <summary>
/// Reads legal documents generated from markdown files during build.
/// </summary>
public sealed class LegalDocumentService
{
    private const string LegalDocumentsFileName = "legal-documents.json";

    // Asset access.

    /// <summary>
    /// Loads the generated legal document bundle from app assets.
    /// </summary>
    public async Task<IReadOnlyList<LegalDocument>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(LegalDocumentsFileName);
        var documents = await JsonSerializer.DeserializeAsync<List<LegalDocument>>(stream, JsonOptions.Default, cancellationToken);

        return documents is { Count: > 0 }
            ? documents
            : throw new InvalidOperationException("Installer legal documents are missing or empty.");
    }
}


// Legal document models.

/// <summary>
/// Describes one legal document shown before installation.
/// </summary>
public sealed record LegalDocument(
    string Id,
    string Title,
    string FileName,
    string Markdown,
    string Sha256);


// JSON configuration.

/// <summary>
/// Provides shared JSON options for installer metadata.
/// </summary>
internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
