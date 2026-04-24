// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace ASLM.Installer;

// Installation orchestration.

/// <summary>
/// Installs the embedded ASLM payload to the selected directory.
/// </summary>
public sealed class InstallerService
{
    private const string PayloadFileName = "aslm-payload.zip";
    private const string PayloadEnvironmentVariable = "ASLM_INSTALLER_PAYLOAD_PATH";
    private const string LauncherExeName = "ASLM.exe";
    private const string ManifestFileName = "install-manifest.json";

    // Default paths.

    /// <summary>
    /// Returns the default parent directory for an ASLM installation.
    /// </summary>
    public string GetDefaultInstallBasePath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    }

    // Path validation.

    /// <summary>
    /// Validates and normalizes the selected install target.
    /// </summary>
    public InstallPathValidation ValidateInstallPath(string basePath, string folderName)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return InstallPathValidation.Error("Choose an installation directory.");
        }

        if (string.IsNullOrWhiteSpace(folderName))
        {
            return InstallPathValidation.Error("Enter the folder name.");
        }

        var trimmedFolderName = folderName.Trim();
        if (trimmedFolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return InstallPathValidation.Error("The folder name contains characters that Windows cannot use in folder names.");
        }

        string fullBasePath;
        string installPath;
        try
        {
            fullBasePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(basePath.Trim()));
            installPath = Path.GetFullPath(Path.Combine(fullBasePath, trimmedFolderName));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return InstallPathValidation.Error($"The installation path is not valid: {ex.Message}");
        }

        if (!Path.IsPathRooted(installPath))
        {
            return InstallPathValidation.Error("Use an absolute installation path.");
        }

        if (!IsChildPath(fullBasePath, installPath))
        {
            return InstallPathValidation.Error("The folder name must stay inside the selected installation directory.");
        }

        if (Directory.Exists(installPath) && Directory.EnumerateFileSystemEntries(installPath).Any())
        {
            return InstallPathValidation.Error("The target folder already exists and is not empty.");
        }

        return InstallPathValidation.Success(installPath);
    }


    // Installation workflow.

    /// <summary>
    /// Extracts ASLM, writes metadata, creates requested shortcuts, and returns the manifest.
    /// </summary>
    public async Task<InstallManifest> InstallAsync(
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        var validation = ValidateInstallPath(options.BasePath, options.FolderName);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Message);
        }

        var installPath = validation.InstallPath;
        var parentPath = Path.GetDirectoryName(installPath)
            ?? throw new InvalidOperationException("Unable to resolve the installation parent directory.");
        var stagingPath = CreateStagingPath();

        progress.Report(new InstallProgress("Preparing installation directory...", 0));
        Directory.CreateDirectory(parentPath);
        Directory.CreateDirectory(stagingPath);

        try
        {
            // Extract into a temp location first, so a failed payload write does not leave a partial install.
            cancellationToken.ThrowIfCancellationRequested();
            await ExtractPayloadAsync(stagingPath, progress, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new InstallProgress("Finalizing files...", 88));

            if (Directory.Exists(installPath))
            {
                Directory.Delete(installPath, recursive: true);
            }

            MoveStagingDirectory(stagingPath, installPath);

            var manifest = new InstallManifest(
                App: "ASLM",
                Version: options.Version,
                InstalledAtUtc: DateTimeOffset.UtcNow,
                InstallPath: installPath,
                AcceptedDocuments: options.AcceptedDocuments);

            await WriteManifestAsync(installPath, manifest, cancellationToken);

            if (options.CreateDesktopShortcut)
            {
                progress.Report(new InstallProgress("Creating desktop shortcut...", 94));
                TryCreateShortcut(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "ASLM.lnk",
                    installPath);
            }

            if (options.CreateStartMenuShortcut)
            {
                progress.Report(new InstallProgress("Creating Start menu shortcut...", 97));
                TryCreateShortcut(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                    "ASLM.lnk",
                    installPath);
            }

            progress.Report(new InstallProgress("ASLM has been installed.", 100));
            return manifest;
        }
        catch
        {
            TryDeleteDirectory(stagingPath);
            throw;
        }
    }

    /// <summary>
    /// Starts ASLM from an installed directory.
    /// </summary>
    public void Launch(string installPath)
    {
        var launcherPath = Path.Combine(installPath, LauncherExeName);
        if (!File.Exists(launcherPath))
        {
            throw new FileNotFoundException("ASLM launcher was not found.", launcherPath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = installPath,
            UseShellExecute = true
        });
    }


    // Payload extraction.

    /// <summary>
    /// Extracts the ASLM payload archive into the target directory.
    /// </summary>
    private static async Task ExtractPayloadAsync(
        string targetPath,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        progress.Report(new InstallProgress("Opening embedded ASLM package...", 4));

        await using var payloadStream = await OpenPayloadStreamAsync();
        using var archive = new ZipArchive(payloadStream, ZipArchiveMode.Read, leaveOpen: false);
        var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("The embedded ASLM package is empty.");
        }

        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = entries[index];
            var destinationPath = Path.GetFullPath(Path.Combine(targetPath, entry.FullName));
            if (!IsChildPath(targetPath, destinationPath))
            {
                throw new InvalidOperationException($"Package entry tries to write outside the installation directory: {entry.FullName}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await using var source = entry.Open();
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken);

            var percent = 6 + (int)Math.Round((index + 1d) / entries.Count * 80d);
            progress.Report(new InstallProgress($"Extracting {entry.FullName}", percent));
        }
    }

    /// <summary>
    /// Opens the payload from the bootstrapper environment, local files, or packaged assets.
    /// </summary>
    private static async Task<Stream> OpenPayloadStreamAsync()
    {
        var candidates = new List<string>();
        var environmentPath = Environment.GetEnvironmentVariable(PayloadEnvironmentVariable);

        AddCandidate(candidates, environmentPath);
        AddCandidate(candidates, Path.Combine(AppContext.BaseDirectory, PayloadFileName));
        AddCandidate(candidates, Path.Combine(Environment.CurrentDirectory, PayloadFileName));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return File.OpenRead(candidate);
            }
        }

        try
        {
            return await FileSystem.OpenAppPackageFileAsync(PayloadFileName);
        }
        catch (FileNotFoundException)
        {
            throw CreatePayloadMissingException(candidates);
        }
        catch (DirectoryNotFoundException)
        {
            throw CreatePayloadMissingException(candidates);
        }
    }

    /// <summary>
    /// Adds one normalized payload candidate path if it is available.
    /// </summary>
    private static void AddCandidate(List<string> candidates, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        if (!candidates.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(fullPath);
        }
    }

    /// <summary>
    /// Builds an error that includes all filesystem locations checked for the payload.
    /// </summary>
    private static FileNotFoundException CreatePayloadMissingException(IReadOnlyList<string> candidates)
    {
        var searchedPaths = candidates.Count == 0
            ? "No filesystem candidates were available."
            : string.Join(Environment.NewLine, candidates.Select(path => $" - {path}"));

        return new FileNotFoundException(
            $"ASLM payload archive was not found. Build Installer/Bootstrapper in Visual Studio so {PayloadFileName} is generated and included.{Environment.NewLine}{searchedPaths}",
            PayloadFileName);
    }


    // Staging directory operations.

    /// <summary>
    /// Creates a unique temporary directory path for payload extraction.
    /// </summary>
    private static string CreateStagingPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "ASLM",
            "InstallStaging",
            Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Moves extracted files to the final install path, copying when direct move is unavailable.
    /// </summary>
    private static void MoveStagingDirectory(string stagingPath, string installPath)
    {
        try
        {
            Directory.Move(stagingPath, installPath);
        }
        catch (IOException)
        {
            CopyDirectory(stagingPath, installPath);
            Directory.Delete(stagingPath, recursive: true);
        }
    }

    /// <summary>
    /// Copies a complete directory tree into the destination path.
    /// </summary>
    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var directoryPath in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, directoryPath);
            Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
        }

        foreach (var filePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, filePath);
            var destinationFilePath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
            File.Copy(filePath, destinationFilePath, overwrite: true);
        }
    }


    // Metadata persistence.

    /// <summary>
    /// Writes the install manifest into the installed ASLM directory.
    /// </summary>
    private static async Task WriteManifestAsync(
        string installPath,
        InstallManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(installPath, ManifestFileName);

        await using var stream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions.Default, cancellationToken);
    }


    // Shortcut creation.

    /// <summary>
    /// Creates a Windows shortcut without failing an otherwise successful installation.
    /// </summary>
    private static void TryCreateShortcut(string folderPath, string shortcutFileName, string installPath)
    {
        try
        {
            CreateShortcut(folderPath, shortcutFileName, installPath);
        }
        catch
        {
            // Shortcut creation is optional after files are installed.
        }
    }

    /// <summary>
    /// Creates a Windows shell shortcut for the installed ASLM launcher.
    /// </summary>
    private static void CreateShortcut(string folderPath, string shortcutFileName, string installPath)
    {
        Directory.CreateDirectory(folderPath);

        var launcherPath = Path.Combine(installPath, LauncherExeName);
        var shortcutPath = Path.Combine(folderPath, shortcutFileName);
        if (!File.Exists(launcherPath))
        {
            return;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return;
        }

        var shell = Activator.CreateInstance(shellType);
        var shortcut = shellType.InvokeMember(
            "CreateShortcut",
            BindingFlags.InvokeMethod,
            binder: null,
            target: shell,
            args: [shortcutPath]);

        var shortcutType = shortcut!.GetType();
        shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [launcherPath]);
        shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [installPath]);
        shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, ["ASLM"]);
        shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [$"{launcherPath},0"]);
        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, []);
    }


    // Path safety and cleanup.

    /// <summary>
    /// Checks whether a path is equal to or located under a parent path.
    /// </summary>
    private static bool IsChildPath(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                normalizedParent.TrimEnd(Path.DirectorySeparatorChar),
                normalizedChild.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deletes a directory without masking the original installation error.
    /// </summary>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Cleanup is best-effort.
        }
    }
}


// Installation option models.

/// <summary>
/// Contains user-selected installation options.
/// </summary>
public sealed record InstallOptions(
    string BasePath,
    string FolderName,
    string Version,
    IReadOnlyList<AcceptedLegalDocument> AcceptedDocuments,
    bool CreateDesktopShortcut,
    bool CreateStartMenuShortcut);

/// <summary>
/// Records one accepted legal document.
/// </summary>
public sealed record AcceptedLegalDocument(
    string Id,
    string Title,
    string FileName,
    string Sha256,
    DateTimeOffset AcceptedAtUtc);


// Installation result models.

/// <summary>
/// Describes the completed ASLM installation.
/// </summary>
public sealed record InstallManifest(
    string App,
    string Version,
    DateTimeOffset InstalledAtUtc,
    string InstallPath,
    IReadOnlyList<AcceptedLegalDocument> AcceptedDocuments);

/// <summary>
/// Reports installation progress to the wizard UI.
/// </summary>
public sealed record InstallProgress(string Message, int Percent);


// Validation models.

/// <summary>
/// Describes the result of installation path validation.
/// </summary>
public sealed record InstallPathValidation(bool IsValid, string InstallPath, string Message)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static InstallPathValidation Success(string installPath) => new(true, installPath, string.Empty);

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static InstallPathValidation Error(string message) => new(false, string.Empty, message);
}
