// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// External patcher entry point.

/// <summary>
/// Applies a pending ASLM self-update while the main launcher is not running.
/// </summary>
internal static class Program
{
    private const string PendingRelativePath = ".aslm-update/pending.json";
    private const string LogRelativePath = ".aslm-update/logs/Patcher.log";
    private const string LauncherExeName = "ASLM.exe";

    private static string _logPath = string.Empty;


    // Process startup

    /// <summary>
    /// Loads the pending update, applies it, and restarts the launcher.
    /// </summary>
    private static int Main(string[] args)
    {
        var root = ResolveRoot(args);
        _logPath = Path.Combine(root, LogRelativePath.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            Log("Patcher started.");

            var pendingPath = Path.Combine(root, PendingRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(pendingPath))
            {
                Log("No pending update found.");
                StartLauncher(root);
                return 0;
            }

            var pending = LoadPendingUpdate(pendingPath);
            ValidatePendingUpdate(pending);

            var targetRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(pending.TargetRoot) ? root : pending.TargetRoot);
            var stagingPath = Path.GetFullPath(pending.StagingPath);
            var backupPath = Path.GetFullPath(pending.BackupPath);
            if (!Directory.Exists(stagingPath))
            {
                throw new DirectoryNotFoundException($"Staging path does not exist: {stagingPath}");
            }

            var externalStaging = Path.Combine(Path.GetTempPath(), "Patcher_Staging_" + Guid.NewGuid().ToString("N"));
            var preservePath = Path.Combine(Path.GetTempPath(), "Patcher_Preserve_" + Guid.NewGuid().ToString("N"));

            try
            {
                Log($"Target root: {targetRoot}");
                Log($"Staging path: {stagingPath}");

                // Copy staging out of the managed update folder first so the final cleanup can remove that folder safely.
                CopyDirectory(stagingPath, externalStaging);
                Directory.CreateDirectory(backupPath);
                Directory.CreateDirectory(preservePath);

                Log("Creating backup...");
                CopyDirectory(targetRoot, backupPath, relativePath => !IsInternalUpdatePath(relativePath));

                Log("Saving preserved files...");
                CopyPreservedEntries(targetRoot, preservePath, pending.Preserve);

                Log("Replacing application files...");
                ClearDirectory(targetRoot, relativePath => !IsInternalUpdatePath(relativePath));
                CopyDirectory(externalStaging, targetRoot);

                Log("Restoring preserved files...");
                CopyDirectory(preservePath, targetRoot);

                File.Delete(pendingPath);
                TryDeleteDirectory(Path.GetDirectoryName(stagingPath) ?? stagingPath);
                Log($"Update {pending.Version} applied successfully.");
                StartLauncher(targetRoot);
                return 0;
            }
            catch
            {
                Log("Patch failed. Attempting rollback...");

                if (Directory.Exists(backupPath))
                {
                    ClearDirectory(targetRoot, relativePath => !IsInternalUpdatePath(relativePath));
                    CopyDirectory(backupPath, targetRoot);
                }

                throw;
            }
            finally
            {
                TryDeleteDirectory(externalStaging);
                TryDeleteDirectory(preservePath);
            }
        }
        catch (Exception ex)
        {
            Log("Fatal patcher error: " + ex);
            MarkPendingUpdateFailed(root);
            StartLauncher(root);
            return 1;
        }
    }


    // Pending update loading

    /// <summary>
    /// Loads the pending update manifest from disk.
    /// </summary>
    private static PendingAppUpdate LoadPendingUpdate(string pendingPath)
    {
        var pending = JsonSerializer.Deserialize<PendingAppUpdate>(File.ReadAllText(pendingPath))
            ?? new PendingAppUpdate();
        pending.Normalize();
        return pending;
    }

    /// <summary>
    /// Validates the pending update payload before any filesystem changes begin.
    /// </summary>
    private static void ValidatePendingUpdate(PendingAppUpdate pending)
    {
        if (!string.Equals(pending.Kind, "app", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported pending update kind: {pending.Kind}");
        }
    }


    // Root resolution

    /// <summary>
    /// Resolves the ASLM root path either from command-line arguments or from the current executable directory.
    /// </summary>
    private static string ResolveRoot(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--root", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }

        return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }


    // Internal path filtering

    /// <summary>
    /// Returns whether the relative path points into the updater's own working directory.
    /// </summary>
    private static bool IsInternalUpdatePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        return normalized.Equals(".aslm-update", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith(".aslm-update/", StringComparison.OrdinalIgnoreCase);
    }


    // Preserve handling

    /// <summary>
    /// Copies preserved files and directories into a temporary location before replacement.
    /// </summary>
    private static void CopyPreservedEntries(
        string sourceRoot,
        string preserveRoot,
        IEnumerable<string> preservePatterns)
    {
        var patterns = preservePatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => NormalizeRelativePattern(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (patterns.Count == 0)
        {
            return;
        }

        var matchedEntries = Directory
            .EnumerateFileSystemEntries(sourceRoot, "*", SearchOption.AllDirectories)
            .Select(entry => new
            {
                FullPath = entry,
                RelativePath = Path.GetRelativePath(sourceRoot, entry).Replace('\\', '/')
            })
            .Where(entry => patterns.Any(pattern => IsPatternMatch(pattern, entry.RelativePath)))
            .OrderBy(entry => entry.RelativePath.Length)
            .Where(entry => !HasPreservedAncestor(entry.RelativePath, patterns))
            .ToList();

        // Copy only the top-most matching entries so preserved directories are not redundantly recopied file-by-file.
        foreach (var entry in matchedEntries)
        {
            var destination = ResolveChildPath(preserveRoot, entry.RelativePath);
            if (File.Exists(entry.FullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(entry.FullPath, destination, overwrite: true);
                continue;
            }

            if (Directory.Exists(entry.FullPath))
            {
                CopyDirectory(entry.FullPath, destination);
            }
        }
    }

    /// <summary>
    /// Normalizes one preserved pattern into the slash-only relative format used by the patcher.
    /// </summary>
    private static string NormalizeRelativePattern(string pattern)
    {
        return pattern.Trim().Replace('\\', '/').Trim('/');
    }

    /// <summary>
    /// Returns whether the current entry is already covered by a preserved parent pattern.
    /// </summary>
    private static bool HasPreservedAncestor(string relativePath, IEnumerable<string> patterns)
    {
        var parent = GetParentRelativePath(relativePath);
        while (!string.IsNullOrWhiteSpace(parent))
        {
            if (patterns.Any(pattern => IsPatternMatch(pattern, parent)))
            {
                return true;
            }

            parent = GetParentRelativePath(parent);
        }

        return false;
    }

    /// <summary>
    /// Returns the parent portion of one relative path, or an empty string when none exists.
    /// </summary>
    private static string GetParentRelativePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        var slashIndex = normalized.LastIndexOf('/');
        return slashIndex > 0 ? normalized[..slashIndex] : string.Empty;
    }

    /// <summary>
    /// Returns whether a preserved pattern matches the requested relative path.
    /// </summary>
    private static bool IsPatternMatch(string pattern, string relativePath)
    {
        if (!pattern.Contains('*'))
        {
            return relativePath.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith(pattern.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
        }

        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "[^/]*", StringComparison.Ordinal) + "($|/.*)";
        return Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }


    // Copy helpers

    /// <summary>
    /// Recursively copies one directory into another, optionally filtering entries by relative path.
    /// </summary>
    private static void CopyDirectory(
        string sourceDir,
        string destDir,
        Func<string, bool>? includeRelative = null)
    {
        Directory.CreateDirectory(destDir);
        var sourceRoot = EnsureTrailingSeparator(Path.GetFullPath(sourceDir));

        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, directory);
            if (includeRelative?.Invoke(relative) == false)
            {
                continue;
            }

            Directory.CreateDirectory(ResolveChildPath(destDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            if (includeRelative?.Invoke(relative) == false)
            {
                continue;
            }

            var destination = ResolveChildPath(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }


    // Delete helpers

    /// <summary>
    /// Deletes every file inside one directory and removes empty subdirectories, optionally filtering by relative path.
    /// </summary>
    private static void ClearDirectory(string directory, Func<string, bool>? deleteRelative = null)
    {
        Directory.CreateDirectory(directory);
        var root = EnsureTrailingSeparator(Path.GetFullPath(directory));

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            var relative = Path.GetRelativePath(root, file);
            if (deleteRelative?.Invoke(relative) == false)
            {
                continue;
            }

            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var subdir in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            var relative = Path.GetRelativePath(root, subdir);
            if (deleteRelative?.Invoke(relative) == false || !Directory.Exists(subdir))
            {
                continue;
            }

            if (!Directory.EnumerateFileSystemEntries(subdir).Any())
            {
                Directory.Delete(subdir);
            }
        }
    }


    // Path helpers

    /// <summary>
    /// Resolves one child path and rejects directory traversal outside the requested root.
    /// </summary>
    private static string ResolveChildPath(string rootPath, string relativePath)
    {
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
        var combined = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!EnsureTrailingSeparator(combined).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{relativePath}' escapes root '{rootPath}'.");
        }

        return combined;
    }

    /// <summary>
    /// Ensures directory paths always end with a separator for secure prefix checks.
    /// </summary>
    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }


    // Launcher restart

    /// <summary>
    /// Restarts the main launcher after patching or after a fatal error.
    /// </summary>
    private static void StartLauncher(string root)
    {
        try
        {
            var launcherPath = Path.Combine(root, LauncherExeName);
            if (!File.Exists(launcherPath))
            {
                Log("Launcher not found after patch: " + launcherPath);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = launcherPath,
                WorkingDirectory = root,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Log("Failed to restart launcher: " + ex);
        }
    }


    // Cleanup

    /// <summary>
    /// Deletes one directory on a best-effort basis.
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
            // Best effort cleanup only.
        }
    }

    /// <summary>
    /// Renames the pending update file after a fatal error so the launcher does not re-enter the same failing patch loop.
    /// </summary>
    private static void MarkPendingUpdateFailed(string root)
    {
        try
        {
            var pendingPath = Path.Combine(root, PendingRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(pendingPath))
            {
                return;
            }

            var failedPath = pendingPath + ".failed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            File.Move(pendingPath, failedPath, overwrite: true);
            Log("Moved failed pending update to: " + failedPath);
        }
        catch (Exception ex)
        {
            Log("Failed to mark pending update as failed: " + ex.Message);
        }
    }


    // Logging

    /// <summary>
    /// Appends one timestamped message to the patcher log.
    /// </summary>
    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging errors to avoid masking the primary failure.
        }
    }


    // Pending update payload

    /// <summary>
    /// Represents the pending ASLM self-update payload written by the main application.
    /// </summary>
    private sealed class PendingAppUpdate
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "app";

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("stagingPath")]
        public string StagingPath { get; set; } = string.Empty;

        [JsonPropertyName("targetRoot")]
        public string TargetRoot { get; set; } = string.Empty;

        [JsonPropertyName("backupPath")]
        public string BackupPath { get; set; } = string.Empty;

        [JsonPropertyName("preserve")]
        public List<string> Preserve { get; set; } = [];

        /// <summary>
        /// Normalizes persisted string and collection values after deserialization.
        /// </summary>
        public void Normalize()
        {
            Kind = string.IsNullOrWhiteSpace(Kind) ? "app" : Kind.Trim();
            Version ??= string.Empty;
            StagingPath ??= string.Empty;
            TargetRoot ??= string.Empty;
            BackupPath ??= string.Empty;
            Preserve ??= [];
        }
    }
}
