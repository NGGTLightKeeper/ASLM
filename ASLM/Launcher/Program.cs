// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Diagnostics;

// Launcher entry point.

/// <summary>
/// Launches the main ASLM application and logs startup errors.
/// </summary>
internal static class Program
{
    private const string FolderName = "App";
    private const string ExeName = "ASLM.exe";
    private const string LogFileName = "Launcher.log";
    private const string PatcherFolderName = "Patcher";
    private static readonly IReadOnlyList<string> PatcherExeNames =
    [
        "ASLM Patcher.exe",
        "Patcher.exe"
    ];
    private const string PendingUpdatePath = ".aslm-update\\pending.json";
    private const string WaitProcessArgument = "--wait-process";

    // Process startup.

    /// <summary>
    /// Resolves the target path, forwards arguments and starts the main process.
    /// </summary>
    private static void Main(string[] args)
    {
        // Resolve the launcher paths.
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var logPath = Path.Combine(currentDir, LogFileName);
        var targetPath = Path.Combine(currentDir, FolderName, ExeName);

        try
        {
            WaitForRequestedProcessExit(args, logPath);

            if (TryStartPendingPatcher(currentDir, logPath))
            {
                return;
            }

            // Ensure the target executable exists.
            if (!File.Exists(targetPath))
            {
                Log("Error: Target executable not found at " + targetPath, logPath);
                Environment.Exit(1);
                return;
            }

            // Build the child process settings.
            var startInfo = new ProcessStartInfo
            {
                FileName = targetPath,
                WorkingDirectory = Path.Combine(currentDir, FolderName),
                UseShellExecute = false
            };

            AppendForwardedArguments(startInfo, args);

            // Start the process and validate the result.
            var process = Process.Start(startInfo);

            if (process == null)
            {
                Log("Error: Failed to start the process (Process.Start returned null).", logPath);
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Log($"Critical Error: {ex.Message}\nStack Trace: {ex.StackTrace}", logPath);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Waits for the previous ASLM process to exit before applying a pending update.
    /// </summary>
    private static void WaitForRequestedProcessExit(string[] args, string logPath)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], WaitProcessArgument, StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(args[index + 1], out var processId))
            {
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(processId);
                Log($"Waiting for ASLM process {processId} to exit before restart.", logPath);
                if (!process.WaitForExit(30000))
                {
                    Log($"ASLM process {processId} did not exit within 30 seconds.", logPath);
                }
            }
            catch (ArgumentException)
            {
                // The process already exited, which is exactly what we need.
            }
            catch (Exception ex)
            {
                Log("Failed while waiting for previous ASLM process: " + ex.Message, logPath);
            }

            return;
        }
    }

    /// <summary>
    /// Forwards user arguments to ASLM while removing launcher-only restart arguments.
    /// </summary>
    private static void AppendForwardedArguments(ProcessStartInfo startInfo, string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], WaitProcessArgument, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            startInfo.ArgumentList.Add(args[index]);
        }
    }

    /// <summary>
    /// Starts the external patcher from a temporary shadow copy when a self-update is pending.
    /// </summary>
    private static bool TryStartPendingPatcher(string rootDir, string logPath)
    {
        var pendingPath = Path.Combine(rootDir, PendingUpdatePath);
        if (!File.Exists(pendingPath))
        {
            return false;
        }

        var patcherDir = Path.Combine(rootDir, PatcherFolderName);
        var patcherExeName = ResolvePatcherExecutableName(patcherDir);
        if (string.IsNullOrWhiteSpace(patcherExeName))
        {
            Log(
                "Pending update found, but patcher is missing. Checked: " +
                string.Join(", ", PatcherExeNames.Select(name => Path.Combine(patcherDir, name))),
                logPath);
            return false;
        }

        try
        {
            var shadowDir = Path.Combine(Path.GetTempPath(), "Patcher_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(shadowDir);

            CopyDirectory(patcherDir, shadowDir);

            var shadowPatcher = Path.Combine(shadowDir, patcherExeName);
            var startInfo = new ProcessStartInfo
            {
                FileName = shadowPatcher,
                WorkingDirectory = shadowDir,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--root");
            startInfo.ArgumentList.Add(rootDir);

            if (Process.Start(startInfo) == null)
            {
                Log("Failed to start patcher (Process.Start returned null).", logPath);
                return false;
            }

            Log("Pending update detected. Handed control to patcher.", logPath);
            return true;
        }
        catch (Exception ex)
        {
            Log("Failed to start patcher: " + ex, logPath);
            return false;
        }
    }

    /// <summary>
    /// Returns the available patcher executable name from the known candidates.
    /// </summary>
    private static string? ResolvePatcherExecutableName(string patcherDir)
    {
        foreach (var candidate in PatcherExeNames)
        {
            if (File.Exists(Path.Combine(patcherDir, candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Copies the complete patcher folder into a temporary shadow directory before replacement starts.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }


    // Log output.

    /// <summary>
    /// Appends a timestamped message to the launcher log.
    /// </summary>
    private static void Log(string message, string logPath)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry);
        }
        catch
        {
            // Ignore logging failures to avoid blocking the launcher.
        }
    }
}
