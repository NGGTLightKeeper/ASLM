using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ASLM.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace ASLM.Services
{
    /// <summary>
    /// Handles the execution of module commands (Run, FirstRun, Settings).
    /// Resolves engine paths via EngineInstaller and manages process execution.
    /// Tracks running processes and supports stopping/killing them.
    /// </summary>
    public class ModuleRunner : IDisposable
    {
        private readonly EngineInstaller _engineInstaller;
        private readonly ProcessTracker _processTracker;
        private readonly ILogger<ModuleRunner> _logger;
        private bool _disposed;

        /// <summary>
        /// Tracks all running processes per module.
        /// Key = module SourcePath (unique per instance), Value = list of running processes.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<Process>> _runningProcesses = new();
        private readonly object _processLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleRunner"/> class.
        /// </summary>
        /// <param name="engineInstaller">Service to resolve engine paths.</param>
        /// <param name="processTracker">Job Object tracker for child process grouping and cleanup.</param>
        /// <param name="logger">Logger instance.</param>
        public ModuleRunner(EngineInstaller engineInstaller, ProcessTracker processTracker, ILogger<ModuleRunner> logger)
        {
            _engineInstaller = engineInstaller;
            _processTracker = processTracker;
            _logger = logger;
        }

        /// <summary>
        /// Executes all 'FirstRun' commands for a module.
        /// First installs required engine libraries, then runs firstRun commands.
        /// Updates the module's status upon success.
        /// </summary>
        /// <param name="module">The module configuration.</param>
        /// <param name="log">Progress reporter for logging output.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if all setup steps succeeded; otherwise, false.</returns>
        public async Task<bool> ExecuteFirstRunAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct)
        {
            // 1. Install engine dependencies (libraries) first
            if (!await InstallDependenciesAsync(module, log, ct))
                return false;

            // 2. Run firstRun commands
            if (module.Commands.FirstRun.Count == 0)
            {
                log.Report("No first-run commands defined.");
                module.Status.FirstRunCompleted = true;
                return true;
            }

            log.Report($"Running {module.Commands.FirstRun.Count} setup command(s)...");

            foreach (var cmd in module.Commands.FirstRun)
            {
                if (ct.IsCancellationRequested) return false;
                
                log.Report($"[Setup] {cmd.Name}: {cmd.Description}");
                bool success = await RunCommandAsync(module, cmd, log, ct, trackProcess: false);
                
                if (!success)
                {
                    log.Report($"✗ Setup failed at step: {cmd.Name}");
                    return false;
                }
            }

            module.Status.FirstRunCompleted = true;
            return true;
        }

        /// <summary>
        /// Executes all 'Run' commands for a module (e.g. start a server).
        /// These are typically long-running processes that are tracked for lifecycle management.
        /// </summary>
        /// <param name="module">The module configuration.</param>
        /// <param name="log">Progress reporter for logging output.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if commands were started successfully.</returns>
        public async Task<bool> ExecuteRunAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct)
        {
            if (module.Commands.Run.Count == 0)
            {
                log.Report($"No run commands for {module.Name}.");
                return true;
            }

            log.Report($"Starting {module.Name}...");

            foreach (var cmd in module.Commands.Run)
            {
                if (ct.IsCancellationRequested) return false;

                log.Report($"[Run] {cmd.Name}: {cmd.Description}");
                // Run commands are long-running — we don't wait for exit, but we track the process
                _ = RunCommandAsync(module, cmd, log, ct, trackProcess: true);
            }

            return true;
        }

        /// <summary>
        /// Stops all running processes for a given module.
        /// Uses SourcePath as the unique identifier to avoid collisions
        /// when multiple module instances share the same Id.
        /// </summary>
        /// <param name="moduleSourcePath">The module's SourcePath (unique per instance).</param>
        public async Task StopModuleAsync(string moduleSourcePath)
        {
            List<Process> processes;
            lock (_processLock)
            {
                if (!_runningProcesses.TryRemove(moduleSourcePath, out processes!))
                    return;
            }

            _logger.LogInformation("Stopping {Count} process(es) for module '{ModulePath}'", processes.Count, moduleSourcePath);

            foreach (var process in processes)
            {
                await KillProcessSafeAsync(process);
            }
        }

        /// <summary>
        /// Stops all running module processes. Called on application shutdown.
        /// </summary>
        public async Task StopAllModulesAsync()
        {
            List<KeyValuePair<string, List<Process>>> allEntries;
            lock (_processLock)
            {
                allEntries = _runningProcesses.ToList();
                _runningProcesses.Clear();
            }

            _logger.LogInformation("Stopping all module processes ({Count} modules)...", allEntries.Count);

            var tasks = new List<Task>();
            foreach (var (moduleId, processes) in allEntries)
            {
                foreach (var process in processes)
                {
                    tasks.Add(KillProcessSafeAsync(process));
                }
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("All module processes stopped.");
        }

        /// <summary>
        /// Installs required libraries for each engine dependency using the
        /// engine's <see cref="EnginePackageManager"/> configuration.
        /// </summary>
        /// <param name="module">The module configuration.</param>
        /// <param name="log">Progress reporter.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if dependencies installed successfully.</returns>
        private async Task<bool> InstallDependenciesAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct)
        {
            foreach (var engineDep in module.Dependencies.Engines)
            {
                if (engineDep.Libraries.Count == 0)
                    continue;

                var engineConfig = _engineInstaller.GetEngineConfig(engineDep.Id);
                if (engineConfig == null)
                {
                    log.Report($"✗ Engine '{engineDep.Id}' not found or not installed.");
                    return false;
                }

                if (engineConfig.PackageManager == null)
                {
                    log.Report($"⚠ Engine '{engineDep.Id}' has no packageManager defined, skipping library install.");
                    continue;
                }

                // Resolve the executable: custom packageManager.executable or engine executable
                var engineDir = Path.GetDirectoryName(engineConfig.SourcePath) ?? "";
                string exePath;
                if (!string.IsNullOrEmpty(engineConfig.PackageManager.Executable))
                {
                    exePath = Path.Combine(engineDir, engineConfig.PackageManager.Executable);
                }
                else
                {
                    exePath = _engineInstaller.GetEngineExecutablePath(engineDep.Id)
                              ?? throw new InvalidOperationException($"Engine executable not found for '{engineDep.Id}'.");
                }

                var libs = string.Join(" ", engineDep.Libraries);
                var args = $"{engineConfig.PackageManager.Command} {libs}";
                log.Report($"[Deps] Installing libraries for {engineDep.Id}: {libs}");

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(module.SourcePath) ?? "",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.OutputDataReceived += (s, e) => { if (e.Data != null) log.Report($"  {e.Data}"); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) log.Report($"  {e.Data}"); };

                if (!process.Start())
                {
                    log.Report($"✗ Failed to start package manager for {engineDep.Id}");
                    return false;
                }

                // Assign to Job Object — groups under ASLM in Task Manager
                _processTracker.AddProcess(process);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(ct);

                if (process.ExitCode != 0)
                {
                    log.Report($"✗ Library install failed with exit code {process.ExitCode}");
                    return false;
                }

                log.Report($"✓ Libraries installed for {engineDep.Id}");
            }

            return true;
        }

        /// <summary>
        /// Executes a single module command.
        /// </summary>
        /// <param name="module">The module configuration.</param>
        /// <param name="cmd">The command to execute.</param>
        /// <param name="log">Progress reporter.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="trackProcess">If true, the process is tracked for lifecycle management (long-running).</param>
        /// <returns>True if the command executed successfully (exit code 0).</returns>
        public async Task<bool> RunCommandAsync(
            ModuleConfig module, 
            ModuleCommand cmd, 
            IProgress<string> log, 
            CancellationToken ct,
            bool trackProcess = false)
        {
            Process? process = null;
            try
            {
                var moduleDir = Path.GetDirectoryName(module.SourcePath);
                if (string.IsNullOrEmpty(moduleDir)) return false;

                string fileName;
                string arguments;

                // 1. Resolve Execution Strategy
                if (!string.IsNullOrEmpty(cmd.Engine))
                {
                    // Run via Engine (e.g. Python)
                    var enginePath = _engineInstaller.GetEngineExecutablePath(cmd.Engine);
                    if (string.IsNullOrEmpty(enginePath))
                    {
                        log.Report($"Error: Engine '{cmd.Engine}' not found or not installed.");
                        return false;
                    }

                    fileName = enginePath;
                    arguments = cmd.Exec; 
                }
                else
                {
                    // Run directly
                    var parts = SplitCommand(cmd.Exec);
                    if (parts.Count == 0) return false;

                    fileName = parts[0];
                    arguments = parts.Count > 1 ? ParseArguments(parts.Skip(1)) : "";
                }

                // 2. Prepare Process StartInfo
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = moduleDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                log.Report($"Exec: {Path.GetFileName(fileName)} {arguments}");

                // 3. Start Process
                // On Windows, use robust launch (Suspended -> Job -> Resume)
                if (OperatingSystem.IsWindows())
                {
                    process = StartSuspendedAndAssignToJob(psi, _processTracker, log);
                    if (process == null)
                    {
                        log.Report("Failed to start process (Windows robust launch).");
                        return false;
                    }
                }
                else
                {
                    process = new Process { StartInfo = psi };
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) log.Report($"  {e.Data}"); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) log.Report($"  err: {e.Data}"); };

                    if (!process.Start())
                    {
                        log.Report("Failed to start process.");
                        process.Dispose();
                        return false;
                    }

                    _processTracker.AddProcess(process);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                // 4. Track Process
                if (trackProcess)
                {
                    lock (_processLock)
                    {
                        var list = _runningProcesses.GetOrAdd(module.SourcePath, _ => new List<Process>());
                        list.Add(process);
                    }
                }

                // 5. Wait for Exit
                await process.WaitForExitAsync(ct);

                // Remove from tracking
                if (trackProcess)
                {
                    RemoveProcess(module.SourcePath, process);
                }

                int exitCode = process.ExitCode;
                process.Dispose(); // Dispose only after getting exit code

                if (exitCode == 0)
                {
                    return true;
                }
                else
                {
                    log.Report($"Process exited with code {exitCode}");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                log.Report("Operation canceled.");
                process?.Dispose();
                return false;
            }
            catch (Exception ex)
            {
                log.Report($"Execution error: {ex.Message}");
                _logger.LogError(ex, "Command execution failed");
                process?.Dispose();
                return false;
            }
        }

        /// <summary>
        /// Safely kills a process and its children.
        /// </summary>
        private async Task KillProcessSafeAsync(Process process)
        {
            try
            {
                if (process.HasExited)
                {
                    process.Dispose();
                    return;
                }

                // Kill the entire process tree
                process.Kill(entireProcessTree: true);
                
                // Wait a short time for the process to exit
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Process {PID} did not exit within timeout after Kill.", process.Id);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error killing process.");
            }
            finally
            {
                process.Dispose();
            }
        }

        /// <summary>
        /// Removes a specific process from the tracking dictionary.
        /// </summary>
        private void RemoveProcess(string moduleSourcePath, Process process)
        {
            lock (_processLock)
            {
                if (_runningProcesses.TryGetValue(moduleSourcePath, out var list))
                {
                    list.Remove(process);
                    if (list.Count == 0)
                    {
                        _runningProcesses.TryRemove(moduleSourcePath, out _);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Kill all tracked processes
            lock (_processLock)
            {
                foreach (var (_, processes) in _runningProcesses)
                {
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (!process.HasExited)
                                process.Kill(entireProcessTree: true);
                        }
                        catch { /* best effort */ }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
                _runningProcesses.Clear();
            }
        }

        /// <summary>
        /// Starts a process in a suspended state, assigns it to the Job Object, and then resumes it.
        /// This ensures the process is part of the job group from the very first moment.
        /// </summary>
        private unsafe Process? StartSuspendedAndAssignToJob(ProcessStartInfo psi, ProcessTracker tracker, IProgress<string> log)
        {
            try
            {
                var commandLine = BuildCommandLine(psi.FileName, psi.Arguments);
                var startupInfo = new STARTUPINFO();
                startupInfo.cb = Marshal.SizeOf(startupInfo);
                startupInfo.dwFlags = 0x00000100; // STARTF_USESTDHANDLES

                // Create pipes for stdout/stderr
                var security = new SECURITY_ATTRIBUTES();
                security.nLength = Marshal.SizeOf(security);
                security.bInheritHandle = 1; // True

                IntPtr hReadOut, hWriteOut;
                IntPtr hReadErr, hWriteErr;

                if (!CreatePipe(out hReadOut, out hWriteOut, ref security, 0) ||
                    !CreatePipe(out hReadErr, out hWriteErr, ref security, 0))
                {
                    log.Report("Failed to create pipes for process.");
                    return null;
                }

                // Ensure read handles are NOT inherited
                SetHandleInformation(hReadOut, 1, 0); // HANDLE_FLAG_INHERIT = 1
                SetHandleInformation(hReadErr, 1, 0);

                startupInfo.hStdOutput = hWriteOut;
                startupInfo.hStdError = hWriteErr;
                startupInfo.hStdInput = IntPtr.Zero; // We don't need input for now

                if (psi.CreateNoWindow)
                {
                    startupInfo.dwFlags |= 0x00000001; // STARTF_USESHOWWINDOW
                    startupInfo.wShowWindow = 0;       // SW_HIDE
                }

                // Flags: CREATE_SUSPENDED (0x4) | CREATE_UNICODE_ENVIRONMENT (0x400) | CREATE_NO_WINDOW (0x08000000)
                uint creationFlags = 0x00000404;
                if (psi.CreateNoWindow) creationFlags |= 0x08000000;

                var processInfo = new PROCESS_INFORMATION();
                var workingDir = string.IsNullOrEmpty(psi.WorkingDirectory) ? null : psi.WorkingDirectory;

                // Create process in suspended state
                bool success = CreateProcess(
                    null,
                    new StringBuilder(commandLine),
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true, // Inherit handles
                    creationFlags,
                    IntPtr.Zero, // Environment (inherit for now)
                    workingDir,
                    ref startupInfo,
                    out processInfo);

                // Close write ends of pipes in parent regardless of success
                CloseHandle(hWriteOut);
                CloseHandle(hWriteErr);

                if (!success)
                {
                    CloseHandle(hReadOut);
                    CloseHandle(hReadErr);
                    log.Report("Failed to CreateProcess (Win32).");
                    return null;
                }

                // The Magic: Add process to Job Object while it is still suspended
                // This guarantees the process is in the job BEFORE it executes any code
                // or spawns any children.
                try
                {
                    // We need a Process object for the tracker API, but we have a raw handle.
                    // Process.GetProcessById creates a new Process object attached to the PID.
                    using var p = Process.GetProcessById(processInfo.dwProcessId);
                    tracker.AddProcess(p);
                }
                catch (Exception ex)
                {
                    log.Report($"Warning: Failed to add process {processInfo.dwProcessId} to Job Object: {ex.Message}");
                }

                // Resume the main thread to let the process run
                ResumeThread(processInfo.hThread);

                // Start async pipe readers
                // We fire and forget these tasks but they will run until pipe closes (process exit)
                _ = ReadPipe(hReadOut, log);
                _ = ReadPipe(hReadErr, log, isError: true);

                // Clean up raw handles (Process object will open its own)
                CloseHandle(processInfo.hThread);
                CloseHandle(processInfo.hProcess);

                // Return a Process object representing the running process
                // This allows the caller to WaitForExitAsync etc.
                return Process.GetProcessById(processInfo.dwProcessId);
            }
            catch (Exception ex)
            {
                log.Report($"Failed to start process via Win32: {ex.Message}");
                return null;
            }
        }

        // P/Invoke Declarations
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

        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
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
        internal static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetHandleInformation(IntPtr hObject, int dwMask, int dwFlags);

        private static string BuildCommandLine(string fileName, string arguments)
        {
            // Minimal escaping
            var sb = new StringBuilder();
            var quote = fileName.Contains(' ') && !fileName.StartsWith("\"");
            if (quote) sb.Append('"');
            sb.Append(fileName);
            if (quote) sb.Append('"');
            if (!string.IsNullOrEmpty(arguments))
            {
                sb.Append(' ');
                sb.Append(arguments);
            }
            return sb.ToString();
        }

        private async Task ReadPipe(IntPtr hPipe, IProgress<string> log, bool isError = false)
        {
            // Simple stream reader wrapper around the handle
            try
            {
                using var stream = new FileStream(new SafeFileHandle(hPipe, true), FileAccess.Read);
                using var reader = new StreamReader(stream, Console.OutputEncoding);
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break; // End of stream

                    if (isError) log.Report($"  err: {line}");
                    else log.Report($"  {line}");
                }
            }
            catch { /* Ignore pipe errors */ }
        }

        /// <summary>
        /// Splits a command string into arguments, respecting quoted strings.
        /// Supports single (') and double (") quotes.
        /// </summary>
        /// <param name="command">The command string to split.</param>
        /// <returns>A list of argument strings.</returns>
        private static List<string> SplitCommand(string command)
        {
            var args = new List<string>();
            var currentArg = new StringBuilder();
            var inQuotes = false;
            var quoteChar = '\0';

            foreach (var c in command)
            {
                if (inQuotes)
                {
                    if (c == quoteChar)
                    {
                        inQuotes = false;
                        quoteChar = '\0';
                    }
                    else
                    {
                        currentArg.Append(c);
                    }
                }
                else
                {
                    if (char.IsWhiteSpace(c))
                    {
                        if (currentArg.Length > 0)
                        {
                            args.Add(currentArg.ToString());
                            currentArg.Clear();
                        }
                    }
                    else if (c == '"' || c == '\'')
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else
                    {
                        currentArg.Append(c);
                    }
                }
            }

            if (currentArg.Length > 0)
            {
                args.Add(currentArg.ToString());
            }

            return args;
        }

        /// <summary>
        /// Reconstructs an argument string from a list of arguments, quoting if necessary.
        /// </summary>
        /// <param name="args">The list of arguments.</param>
        /// <returns>A single command-line argument string.</returns>
        private static string ParseArguments(IEnumerable<string> args)
        {
            // Simple reconstruction: join with spaces.
            // If an argument contains spaces, it should ideally be quoted,
            // but since we split it ourselves, we assume the user provided it correctly.
            // However, for passing to ProcessStartInfo.Arguments, we might want to ensure quotes.
            // But usually ProcessStartInfo handles raw strings if passed as a single string.
            // Here we just join them back because we split them to find the executable (first part).

            // Note: Since we are passing the rest as 'arguments' to ProcessStartInfo,
            // usually we can just take the substring after the executable.
            // But reconstruction is safer if we want to support complex parsing later.

            return string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        }
    }
}
