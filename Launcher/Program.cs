using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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

    private static unsafe void Main(string[] args)
    {
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var logPath = Path.Combine(currentDir, LogFileName);
        var targetPath = Path.Combine(currentDir, FolderName, ExeName);
        var workingDir = Path.Combine(currentDir, FolderName);

        try
        {
            if (!File.Exists(targetPath))
            {
                Log("Error: Target executable not found at " + targetPath, logPath);
                Environment.Exit(1);
                return;
            }

            // On Windows, use CreateProcess with CREATE_BREAKAWAY_FROM_JOB to ensure
            // ASLM starts outside the Launcher's Job Object (if any).
            // This allows ASLM to create its own Process Group for its child modules.
            if (OperatingSystem.IsWindows())
            {
                if (LaunchWithBreakaway(targetPath, workingDir, logPath))
                {
                    return; // Success
                }
                Log("Warning: Breakaway launch failed, falling back to standard launch.", logPath);
            }

            // Fallback for non-Windows or failure
            var startInfo = new ProcessStartInfo
            {
                FileName = targetPath,
                WorkingDirectory = workingDir,
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

    private static unsafe bool LaunchWithBreakaway(string exePath, string workingDir, string logPath)
    {
        try
        {
            var startupInfo = new STARTUPINFO();
            startupInfo.cb = Marshal.SizeOf(startupInfo);

            var processInfo = new PROCESS_INFORMATION();

            // 0x01000000 = CREATE_BREAKAWAY_FROM_JOB
            // 0x00000400 = CREATE_UNICODE_ENVIRONMENT
            uint creationFlags = 0x01000000 | 0x00000400;

            // Simple command line quoting
            var commandLine = $"\"{exePath}\"";

            bool success = CreateProcess(
                null,
                new StringBuilder(commandLine),
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                creationFlags,
                IntPtr.Zero,
                workingDir,
                ref startupInfo,
                out processInfo);

            if (success)
            {
                // We don't need to hold onto the handles for the launcher
                CloseHandle(processInfo.hThread);
                CloseHandle(processInfo.hProcess);
                return true;
            }

            Log($"CreateProcess failed with error: {Marshal.GetLastWin32Error()}", logPath);
            return false;
        }
        catch (Exception ex)
        {
            Log($"Breakaway launch exception: {ex.Message}", logPath);
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr hObject);

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
