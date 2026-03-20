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
