// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services
{
    // Module runner

    /// <summary>
    /// Executes module setup, runtime, and settings commands and tracks their processes.
    /// </summary>
    public class ModuleRunner : IDisposable
    {
        private readonly EngineInstaller _engineInstaller;
        private readonly PortManager _portManager;
        private readonly ProcessTracker _processTracker;
        private readonly ILogger<ModuleRunner> _logger;
        private bool _disposed;

        // Running processes

        /// <summary>
        /// Tracks running processes by module source path.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<Process>> _runningProcesses = new();
        private readonly object _processLock = new();

        // Initialization

        /// <summary>
        /// Creates the module runner.
        /// </summary>
        /// <param name="engineInstaller">Service to resolve engine paths.</param>
        /// <param name="processTracker">Service that groups child processes under ASLM.</param>
        /// <param name="logger">Logger instance.</param>
        public ModuleRunner(EngineInstaller engineInstaller, PortManager portManager, ProcessTracker processTracker, ILogger<ModuleRunner> logger)
        {
            _engineInstaller = engineInstaller;
            _portManager = portManager;
            _processTracker = processTracker;
            _logger = logger;
        }

        // Setup execution

        /// <summary>
        /// Executes first-run setup for a module.
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

        // Run execution

        /// <summary>
        /// Executes the long-running run commands for a module.
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

            // Synchronize settings: resolve target values, check current values in parallel,
            // then apply only the settings that actually need updating.
            if (module.Settings != null && module.Settings.Count > 0)
            {
                log.Report("Synchronizing module settings...");

                // Collect only settings that have a SetExec defined.
                var settingsToSync = module.Settings
                    .Where(s => !string.IsNullOrEmpty(s.SetExec))
                    .ToList();

                if (settingsToSync.Count > 0)
                {
                    // Resolve all target values up front (no I/O, very fast).
                    var targets = settingsToSync
                        .Select(s => (Setting: s, Target: ResolveSettingValue(module, s)))
                        .ToList();

                    // Fire all GetExec checks in parallel to avoid N sequential process spawns.
                    var checkTasks = targets.Select(async t =>
                    {
                        if (string.IsNullOrEmpty(t.Setting.GetExec))
                        {
                            return (t.Setting, t.Target, NeedsUpdate: true);
                        }

                        var current = await ExecuteSettingCommandAsync(module, t.Setting, isSet: false, newValue: null, ct);
                        var upToDate = current != null &&
                                       string.Equals(current.Trim(), t.Target.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (upToDate)
                        {
                            log.Report($"[Sync] '{t.Setting.Key}' is already up to date ({current!.Trim()})");
                        }

                        return (t.Setting, t.Target, NeedsUpdate: !upToDate);
                    });

                    var results = await Task.WhenAll(checkTasks);

                    // Apply Set commands sequentially for those that need updating.
                    foreach (var (setting, target, needsUpdate) in results)
                    {
                        if (ct.IsCancellationRequested) return false;

                        if (needsUpdate)
                        {
                            log.Report($"[Sync] Applying '{setting.Key}' = {target}");
                            await ExecuteSettingCommandAsync(module, setting, isSet: true, newValue: target, ct);
                        }
                    }
                }
            }

            log.Report($"Starting {module.Name}...");

            foreach (var cmd in module.Commands.Run)
            {
                if (ct.IsCancellationRequested) return false;

                log.Report($"[Run] {cmd.Name}: {cmd.Description}");
                // Run commands are long-running; we start them in the background and track their processes.
                _ = RunCommandAsync(module, cmd, log, ct, trackProcess: true);
            }

            return true;
        }

        // Module stop

        /// <summary>
        /// Stops all tracked processes for one module.
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

        // Global stop

        /// <summary>
        /// Stops every tracked module process.
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

        // Settings propagation

        /// <summary>
        /// Injects resolved module settings into the process environment.
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

            // Also expose some useful module context values to child processes.
            psi.Environment["ASLM_MODULE_ID"] = module.Id;
            psi.Environment["ASLM_MODULE_DIR"] = Path.GetDirectoryName(module.SourcePath) ?? "";
        }

        /// <summary>
        /// Returns the effective setting value that ASLM resolves for a module.
        /// </summary>
        public object? GetResolvedSettingValue(ModuleConfig module, ModuleSetting setting)
        {
            var resolved = ResolveSettingValue(module, setting);
            return setting.ParseSerializedValue(resolved);
        }

        /// <summary>
        /// Resolves the effective string value for one module setting.
        /// </summary>
        private string ResolveSettingValue(ModuleConfig module, ModuleSetting setting)
        {
            switch (setting.NormalizedType)
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
                    if (_engineInstaller.DiscoverEngines().Any(engine =>
                        engine.Id.Equals(setting.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        return _engineInstaller.GetEngineConfig(setting.Key) != null ? "true" : "false";
                    }

                    var rawEngine = (setting.Value ?? setting.Default)?.ToString();
                    return rawEngine != null && rawEngine.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

                case "path":
                    if (setting.UseCustomValue)
                    {
                        return (setting.Value ?? setting.Default)?.ToString() ?? string.Empty;
                    }

                    // Returns the engine executable path, or empty string if not installed.
                    var pathEngineId = TrimEngineSettingSuffix(setting.Key);
                    var enginePath = _engineInstaller.GetEngineExecutablePath(pathEngineId);
                    return !string.IsNullOrEmpty(enginePath) ? enginePath.Replace('\\', '/') : "";

                case "data":
                    if (setting.UseCustomValue)
                    {
                        return (setting.Value ?? setting.Default)?.ToString() ?? string.Empty;
                    }

                    // Returns the engine data path: <root>/Data/<engineId>/.
                    var dataEngineId = TrimEngineSettingSuffix(setting.Key);
                    if (_engineInstaller.GetEngineConfig(dataEngineId) == null) return "";

                    var rootDir = GetRootDirectory();
                    return (Path.Combine(rootDir, "Data", dataEngineId) + Path.DirectorySeparatorChar).Replace('\\', '/');

                case "models":
                    if (setting.UseCustomValue)
                    {
                        return (setting.Value ?? setting.Default)?.ToString() ?? string.Empty;
                    }

                    // Returns the engine models path: <root>/Models/<engineId>/.
                    var modelsEngineId = TrimEngineSettingSuffix(setting.Key);
                    if (_engineInstaller.GetEngineConfig(modelsEngineId) == null) return "";

                    var modelsRootDir = GetRootDirectory();
                    return (Path.Combine(modelsRootDir, "Models", modelsEngineId) + Path.DirectorySeparatorChar).Replace('\\', '/');

                case "bool":
                    var rawBool = (setting.Value ?? setting.Default)?.ToString();
                    return rawBool != null && rawBool.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

                default:
                    return (setting.Value ?? setting.Default)?.ToString() ?? string.Empty;
            }
        }

        // Dependency install

        /// <summary>
        /// Installs engine-specific libraries required by a module.
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

                // Assign to the job object so dependency installs stay grouped under ASLM.
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

        // Command execution

        /// <summary>
        /// Executes one module command.
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

                // Assign to the job object so launched processes stay grouped under ASLM.
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

        // Setting commands

        /// <summary>
        /// Executes a get or set command declared by one module setting.
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
            
            return lines.Count > 0 ? lines[^1] : string.Empty;
        }

        // Process shutdown

        /// <summary>
        /// Safely kills a process and its child tree.
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

        // Tracking cleanup

        /// <summary>
        /// Removes one process from the tracking dictionary.
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

        // Disposal

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Kill all tracked processes before the runner is disposed.
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

        // Command parsing

        /// <summary>
        /// Splits a command string into arguments while respecting quotes.
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

        // Argument join

        /// <summary>
        /// Rebuilds an argument string from parsed arguments.
        /// </summary>
        /// <param name="args">The list of arguments.</param>
        /// <returns>A single command-line argument string.</returns>
        private static string ParseArguments(IEnumerable<string> args)
        {
            return string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        }

        // Engine key parsing

        /// <summary>
        /// Removes known engine setting suffixes from a setting key.
        /// </summary>
        private static string TrimEngineSettingSuffix(string settingKey)
        {
            if (settingKey.EndsWith("_path", StringComparison.OrdinalIgnoreCase) ||
                settingKey.EndsWith("_data", StringComparison.OrdinalIgnoreCase))
            {
                return settingKey[..^5];
            }

            if (settingKey.EndsWith("_models", StringComparison.OrdinalIgnoreCase))
            {
                return settingKey[..^7];
            }

            return settingKey;
        }

        // Root path

        /// <summary>
        /// Returns the application root directory.
        /// </summary>
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }
    }
}
