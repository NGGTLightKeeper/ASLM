using System.Diagnostics;

/// <summary>
/// Entry point for the ASLM Launcher.
/// Attempts to launch the main ASLM application located in the 'App' subdirectory.
/// Logs any startup errors to 'Launcher.log'.
/// </summary>
internal class Program
{
    private const string FolderName = "App";
    private const string ExeName = "ASLM.exe";
    private const string LogFileName = "Launcher.log";

    private static void Main(string[] args)
    {
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var logPath = Path.Combine(currentDir, LogFileName);
        var targetPath = Path.Combine(currentDir, FolderName, ExeName);

        try
        {
            if (!File.Exists(targetPath))
            {
                Log("Error: Target executable not found at " + targetPath, logPath);
                Environment.Exit(1);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = targetPath,
                WorkingDirectory = Path.Combine(currentDir, FolderName),
                UseShellExecute = false
            };

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
    /// Appends a message to the log file with a timestamp.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="logPath">The full path to the log file.</param>
    private static void Log(string message, string logPath)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry);
        }
        catch
        {
            // If logging fails (e.g., permissions), we can't do much.
            // Failing silently is acceptable for a launcher to avoid crash loops.
        }
    }
}
