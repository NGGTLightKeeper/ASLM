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
    private const string PatcherExeName = "Patcher.exe";
    private const string PendingUpdatePath = ".aslm-update\\pending.json";

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

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

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
        var patcherPath = Path.Combine(patcherDir, PatcherExeName);
        if (!File.Exists(patcherPath))
        {
            Log("Pending update found, but patcher is missing: " + patcherPath, logPath);
            return false;
        }

        try
        {
            var shadowDir = Path.Combine(Path.GetTempPath(), "Patcher_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(shadowDir);

            foreach (var file in Directory.EnumerateFiles(patcherDir, "Patcher*"))
            {
                File.Copy(file, Path.Combine(shadowDir, Path.GetFileName(file)), overwrite: true);
            }

            var shadowPatcher = Path.Combine(shadowDir, PatcherExeName);
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
