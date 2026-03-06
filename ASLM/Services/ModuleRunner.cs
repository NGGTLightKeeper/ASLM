using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ASLM.Models;
using Microsoft.Extensions.Logging;

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
        private readonly PortManager _portManager;
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
        public ModuleRunner(EngineInstaller engineInstaller, PortManager portManager, ProcessTracker processTracker, ILogger<ModuleRunner> logger)
        {
            _engineInstaller = engineInstaller;
            _portManager = portManager;
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

            // --- Enforce Setting Synchronization ---
            if (module.Settings != null && module.Settings.Count > 0)
            {
                log.Report("Synchronizing module settings...");
                foreach (var setting in module.Settings)
                {
                    if (ct.IsCancellationRequested) return false;
                    if (string.IsNullOrEmpty(setting.SetExec)) continue;

                    var targetValue = ResolveSettingValue(module, setting);
                    bool needsUpdate = true;

                    if (!string.IsNullOrEmpty(setting.GetExec))
                    {
                        var currentValue = await ExecuteSettingCommandAsync(module, setting, isSet: false, newValue: null, ct);
                        
                        if (currentValue != null && string.Equals(currentValue.Trim(), targetValue.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            log.Report($"[Sync] '{setting.Key}' is already up to date ({currentValue.Trim()})");
                            needsUpdate = false;
                        }
                    }

                    if (needsUpdate)
                    {
                        log.Report($"[Sync] Applying '{setting.Key}' = {targetValue}");
                        await ExecuteSettingCommandAsync(module, setting, isSet: true, newValue: targetValue, ct);
                    }
                }
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

        // --- Settings Propagation --------------------------------------------

        /// <summary>
        /// Injects the resolved settings into the given ProcessStartInfo environment.
        /// </summary>
        private void InjectSettingsIntoEnvironment(ModuleConfig module, ProcessStartInfo psi)
        {
            if (module.Settings == null) return;

            foreach (var setting in module.Settings)
            {
                var resolved = ResolveSettingValue(module, setting);
                var envKey = $"ASLM_{setting.Key.ToUpperInvariant()}";
                psi.Environment[envKey] = resolved;
            }

            // Also inject some useful module context
            psi.Environment["ASLM_MODULE_ID"] = module.Id;
            psi.Environment["ASLM_MODULE_DIR"] = Path.GetDirectoryName(module.SourcePath) ?? "";
        }

        /// <summary>
        /// Resolves the effective string value for a single setting.
        /// </summary>
        private string ResolveSettingValue(ModuleConfig module, ModuleSetting setting)
        {
            switch (setting.Type.ToLowerInvariant())
            {
                case "port":
                    var ports = _portManager.GetOrAssignPorts(module);
                    if (ports.TryGetValue(setting.Key, out var assigned))
                        return assigned.ToString();
                    
                    var rawPort = (setting.Value ?? setting.Default)?.ToString();
                    if (int.TryParse(rawPort, out var p))
                        return p.ToString();
                    return rawPort ?? string.Empty;

                case "engine":
                    // Returns true/false based on whether the engine is installed
                    // Key can be the engine ID itself, or something like "ollama-service" from "ollama-service_path"
                    var engineId = setting.Key;
                    if (engineId.EndsWith("_path")) engineId = engineId[..^5];
                    else if (engineId.EndsWith("_data")) engineId = engineId[..^5];
                    
                    return _engineInstaller.GetEngineConfig(engineId) != null ? "true" : "false";

                case "path":
                    // Returns the engine executable path, or empty string if not installed
                    var pathEngineId = setting.Key;
                    if (pathEngineId.EndsWith("_path")) pathEngineId = pathEngineId[..^5];
                    
                    var enginePath = _engineInstaller.GetEngineExecutablePath(pathEngineId);
                    return !string.IsNullOrEmpty(enginePath) ? enginePath.Replace('\\', '/') : "";

                case "data":
                    // Returns the engine data path: C:\Projects\ASLM\Data\<engineId>\
                    var dataEngineId = setting.Key;
                    if (dataEngineId.EndsWith("_data")) dataEngineId = dataEngineId[..^5];
                    
                    if (_engineInstaller.GetEngineConfig(dataEngineId) == null) return "";
                    
                    var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                    var rootDir = Directory.GetParent(appDir)?.FullName ?? appDir;
                    return (Path.Combine(rootDir, "Data", dataEngineId) + Path.DirectorySeparatorChar).Replace('\\', '/');

                case "bool":
                    var rawBool = (setting.Value ?? setting.Default)?.ToString();
                    return rawBool != null && rawBool.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

                default:
                    return (setting.Value ?? setting.Default)?.ToString() ?? string.Empty;
            }
        }

        // --- Dependencies Install --------------------------------------------

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
            bool trackProcess = false,
            bool injectSettings = true)
        {
            try
            {
                var moduleDir = Path.GetDirectoryName(module.SourcePath);
                if (string.IsNullOrEmpty(moduleDir)) return false;

                string fileName;
                string arguments = string.Empty;

                // 1. Resolve Execution Strategy
                var execString = cmd.Exec ?? string.Empty;

                // Replace {key} placeholders with setting values
                if (module.Settings != null)
                {
                    foreach (var setting in module.Settings)
                    {
                        var resolved = ResolveSettingValue(module, setting);
                        execString = execString.Replace($"{{{setting.Key}}}", resolved);
                    }
                }

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
                    // Combine engine + script/args
                    // cmd.Exec = "manage.py runserver"
                    // We need to ensure we run it in the module directory context
                    arguments = execString; 
                }
                else
                {
                    // Run directly (e.g. valid executable on PATH)
                    // Properly parse the command string to handle quotes
                    var parts = SplitCommand(execString);
                    if (parts.Count == 0) return false;

                    fileName = parts[0];
                    arguments = parts.Count > 1 ? ParseArguments(parts.Skip(1)) : "";
                }

                // 2. Prepare Process
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

                // Apply settings as environment variables so the module can consume them dynamically
                if (injectSettings)
                {
                    InjectSettingsIntoEnvironment(module, psi);
                }
                
                log.Report($"Exec: {Path.GetFileName(fileName)} {arguments}");

                var process = new Process { StartInfo = psi };
                
                // 3. Output Handling
                process.OutputDataReceived += (s, e) => { if (e.Data != null) log.Report($"  {e.Data}"); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) log.Report($"  err: {e.Data}"); };

                // 4. Start & Wait
                if (!process.Start())
                {
                    log.Report("Failed to start process.");
                    process.Dispose();
                    return false;
                }

                // Assign to Job Object — groups under ASLM in Task Manager
                _processTracker.AddProcess(process);

                // Track the process for module stop functionality
                if (trackProcess)
                {
                    lock (_processLock)
                    {
                        var list = _runningProcesses.GetOrAdd(module.SourcePath, _ => new List<Process>());
                        list.Add(process);
                    }
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(ct);

                // Remove from tracking after process exits naturally
                if (trackProcess)
                {
                    RemoveProcess(module.SourcePath, process);
                }

                if (process.ExitCode == 0)
                {
                    process.Dispose();
                    return true;
                }
                else
                {
                    log.Report($"Process exited with code {process.ExitCode}");
                    process.Dispose();
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                log.Report("Operation canceled.");
                return false;
            }
            catch (Exception ex)
            {
                log.Report($"Execution error: {ex.Message}");
                _logger.LogError(ex, "Command execution failed");
                return false;
            }
        }

        /// <summary>
        /// Executes a setting command (getExec or setExec) from the module's configuration.
        /// </summary>
        /// <returns>The standard output of the command if successful, otherwise null.</returns>
        public async Task<string?> ExecuteSettingCommandAsync(ModuleConfig module, ModuleSetting setting, bool isSet, string? newValue, CancellationToken ct)
        {
            var execStr = isSet ? setting.SetExec : setting.GetExec;
            if (string.IsNullOrEmpty(execStr)) return null;

            // Replace {value} placeholder specifically for setExec
            if (isSet && newValue != null)
            {
                execStr = execStr.Replace("{value}", newValue);
            }

            var cmd = new ModuleCommand
            {
                Name = isSet ? $"Set {setting.Key}" : $"Get {setting.Key}",
                Engine = setting.Engine,
                Exec = execStr
            };

            var outputBuilder = new StringBuilder();
            var lockObject = new object();
            var log = new Progress<string>(msg =>
            {
                if (string.IsNullOrEmpty(msg)) return;

                // Capture the exact output, avoiding log prefixes and errors
                var trimmed = msg.TrimStart();
                if (!trimmed.StartsWith("Exec:") && !trimmed.StartsWith("err:"))
                {
                    lock (lockObject)
                    {
                        outputBuilder.AppendLine(trimmed);
                    }
                }
            });

            bool success = await RunCommandAsync(module, cmd, log, ct, trackProcess: false, injectSettings: isSet);
            if (!success) return null;

            // Get the last non-empty line (to ignore banners or headers)
            var lines = outputBuilder.ToString()
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();
            
            return lines.Count > 0 ? lines[^1] : null;
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
