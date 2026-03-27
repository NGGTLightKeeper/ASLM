// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
        private readonly ModuleConsoleService _consoleService;
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
        /// <param name="portManager">Service to assign ports for settings resolution.</param>
        /// <param name="processTracker">Service that groups child processes under ASLM.</param>
        /// <param name="consoleService">Service to report console output and process sessions.</param>
        /// <param name="logger">Logger instance.</param>
        public ModuleRunner(
            EngineInstaller engineInstaller,
            PortManager portManager,
            ProcessTracker processTracker,
            ModuleConsoleService consoleService,
            ILogger<ModuleRunner> logger)
        {
            _engineInstaller = engineInstaller;
            _portManager = portManager;
            _processTracker = processTracker;
            _consoleService = consoleService;
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
            _consoleService.EnsureModule(module);
            var moduleLog = CreateModuleLog(module, log);

            // 1. Install engine dependencies (libraries) first
            if (!await InstallDependenciesAsync(module, moduleLog, ct))
                return false;

            // 2. Run firstRun commands
            if (module.Commands.FirstRun.Count == 0)
            {
                moduleLog.Report("No first-run commands defined.");
                module.Status.FirstRunCompleted = true;
                return true;
            }

            moduleLog.Report($"Running {module.Commands.FirstRun.Count} setup command(s)...");

            foreach (var cmd in module.Commands.FirstRun)
            {
                if (ct.IsCancellationRequested) return false;
                
                moduleLog.Report($"[Setup] {cmd.Name}: {cmd.Description}");
                bool success = await RunCommandAsync(module, cmd, moduleLog, ct, trackProcess: false, sessionStage: "Setup");
                
                if (!success)
                {
                    moduleLog.Report($"✗ Setup failed at step: {cmd.Name}");
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
            _consoleService.EnsureModule(module);
            var moduleLog = CreateModuleLog(module, log);

            if (module.Commands.Run.Count == 0)
            {
                moduleLog.Report($"No run commands for {module.Name}.");
                return true;
            }

            // Synchronize settings: resolve target values, check current values in parallel,
            // then apply only the settings that actually need updating.
            if (module.Settings != null && module.Settings.Count > 0)
            {
                moduleLog.Report("Synchronizing module settings...");

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
                            moduleLog.Report($"[Sync] '{t.Setting.Key}' is already up to date ({current!.Trim()})");
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
                            moduleLog.Report($"[Sync] Applying '{setting.Key}' = {target}");
                            await ExecuteSettingCommandAsync(module, setting, isSet: true, newValue: target, ct);
                        }
                    }
                }
            }

            moduleLog.Report($"Starting {module.Name}...");

            foreach (var cmd in module.Commands.Run)
            {
                if (ct.IsCancellationRequested) return false;

                moduleLog.Report($"[Run] {cmd.Name}: {cmd.Description}");
                // Run commands are long-running; we start them in the background and track their processes.
                _ = RunCommandAsync(module, cmd, moduleLog, ct, trackProcess: true, sessionStage: "Run");
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
            _consoleService.AppendOverviewLine(moduleSourcePath, "Stopping module processes...");
            _consoleService.UpdateModuleEnabledState(moduleSourcePath, false);

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

            _consoleService.AppendOverviewLine(moduleSourcePath, "Module processes stopped.");
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
                _consoleService.AppendOverviewLine(moduleId, "Stopping all module processes...");
                _consoleService.UpdateModuleEnabledState(moduleId, false);

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
                ConfigureProcessForStreaming(psi, exePath, engineDep.Id);

                using var process = new Process { StartInfo = psi };

                if (!process.Start())
                {
                    log.Report($"✗ Failed to start package manager for {engineDep.Id}");
                    return false;
                }

                var sessionHandle = _consoleService.StartProcessSession(
                    module,
                    new ModuleCommand
                    {
                        Name = $"Dependencies: {engineDep.Id}",
                        Description = $"Install libraries via {engineConfig.PackageManager.Command}",
                        Exec = args
                    },
                    "Dependencies",
                    $"Exec: {Path.GetFileName(exePath)} {args}",
                    process,
                    isTrackedProcess: false);

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        var line = $"  {e.Data}";
                        log.Report(line);
                        _consoleService.AppendProcessLine(sessionHandle, line);
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        var line = $"  {e.Data}";
                        log.Report(line);
                        _consoleService.AppendProcessLine(sessionHandle, line);
                    }
                };

                // Assign to the job object so dependency installs stay grouped under ASLM.
                _processTracker.AddProcess(process);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(ct);
                _consoleService.CompleteProcessSession(sessionHandle, process.ExitCode);

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
            bool injectSettings = true,
            string sessionStage = "Command")
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
                ConfigureProcessForStreaming(psi, fileName, cmd.Engine);

                // Apply settings as environment variables so the module can consume them dynamically
                if (injectSettings)
                {
                    InjectSettingsIntoEnvironment(module, psi);
                }
                
                var execMessage = $"Exec: {Path.GetFileName(fileName)} {arguments}";
                log.Report(execMessage);

                var process = new Process { StartInfo = psi };

                // 4. Start & Wait
                if (!process.Start())
                {
                    log.Report("Failed to start process.");
                    process.Dispose();
                    return false;
                }

                var sessionHandle = _consoleService.StartProcessSession(
                    module,
                    cmd,
                    sessionStage,
                    execMessage,
                    process,
                    isTrackedProcess: trackProcess);

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        var line = $"  {e.Data}";
                        log.Report(line);
                        _consoleService.AppendProcessLine(sessionHandle, line);
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        var line = $"  {e.Data}";
                        log.Report(line);
                        _consoleService.AppendProcessLine(sessionHandle, line);
                    }
                };

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

                    _ = MonitorObservedProcessesAsync(module, process.Id, ct);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(ct);
                _consoleService.CompleteProcessSession(sessionHandle, process.ExitCode);

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
                if (!trimmed.StartsWith("Exec:"))
                {
                    lock (lockObject)
                    {
                        outputBuilder.AppendLine(trimmed);
                    }
                }
            });

            bool success = await RunCommandAsync(
                module,
                cmd,
                log,
                ct,
                trackProcess: false,
                injectSettings: isSet,
                sessionStage: "Settings");
            if (!success) return null;

            // Get the last non-empty line (to ignore banners or headers)
            var lines = outputBuilder.ToString()
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();
            
            return lines.Count > 0 ? lines[^1] : string.Empty;
        }

        // Console forwarding

        /// <summary>
        /// Mirrors module log messages into the shared consoles store.
        /// </summary>
        private IProgress<string> CreateModuleLog(ModuleConfig module, IProgress<string> log)
        {
            return new Progress<string>(message =>
            {
                log.Report(message);
                _consoleService.AppendOverviewLine(module, message);
            });
        }

        /// <summary>
        /// Tunes process startup so redirected console output is as complete and timely as possible.
        /// </summary>
        private static void ConfigureProcessForStreaming(ProcessStartInfo psi, string fileName, string? engineId)
        {
            if (!IsPythonProcess(fileName, engineId))
            {
                return;
            }

            if (!HasPythonUnbufferedFlag(psi.Arguments))
            {
                psi.Arguments = string.IsNullOrWhiteSpace(psi.Arguments)
                    ? "-u"
                    : $"-u {psi.Arguments}";
            }

            psi.Environment["PYTHONUNBUFFERED"] = "1";
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
        }

        /// <summary>
        /// Returns whether the command is executed by a Python runtime.
        /// </summary>
        private static bool IsPythonProcess(string fileName, string? engineId)
        {
            if (!string.IsNullOrWhiteSpace(engineId) &&
                engineId.Contains("python", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var executableName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(executableName))
            {
                return false;
            }

            return executableName.StartsWith("python", StringComparison.OrdinalIgnoreCase) ||
                   executableName.StartsWith("py", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns whether the Python command line already enables unbuffered output.
        /// </summary>
        private static bool HasPythonUnbufferedFlag(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return false;
            }

            var parts = SplitCommand(arguments);
            return parts.Any(part => string.Equals(part, "-u", StringComparison.OrdinalIgnoreCase));
        }

        // Observed subprocesses

        /// <summary>
        /// Polls descendant processes of one tracked module process so externally spawned services stay visible.
        /// </summary>
        private async Task MonitorObservedProcessesAsync(ModuleConfig module, int rootProcessId, CancellationToken ct)
        {
            var emptyCycles = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var observedProcesses = GetDescendantProcesses(rootProcessId);
                    _consoleService.SyncObservedProcesses(module, observedProcesses);

                    if (observedProcesses.Count == 0 && !IsProcessAlive(rootProcessId))
                    {
                        emptyCycles++;
                        if (emptyCycles >= 3)
                        {
                            break;
                        }
                    }
                    else
                    {
                        emptyCycles = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Observed process polling failed for root process {RootProcessId}.", rootProcessId);
                }

                try
                {
                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _consoleService.SyncObservedProcesses(module, []);
        }

        /// <summary>
        /// Returns descendant processes of one root process.
        /// </summary>
        private static List<ObservedProcessInfo> GetDescendantProcesses(int rootProcessId)
        {
#if WINDOWS
            var snapshotEntries = TakeProcessSnapshot();
            var childrenByParent = snapshotEntries
                .GroupBy(entry => entry.ParentProcessId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var results = new List<ObservedProcessInfo>();
            var queue = new Queue<int>();
            var visited = new HashSet<int> { rootProcessId };

            queue.Enqueue(rootProcessId);

            while (queue.Count > 0)
            {
                var parentProcessId = queue.Dequeue();
                if (!childrenByParent.TryGetValue(parentProcessId, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    if (!visited.Add(child.ProcessId))
                    {
                        continue;
                    }

                    results.Add(new ObservedProcessInfo
                    {
                        ProcessId = child.ProcessId,
                        ProcessName = ResolveObservedProcessName(child)
                    });
                    queue.Enqueue(child.ProcessId);
                }
            }

            return results;
#else
            return [];
#endif
        }

        /// <summary>
        /// Returns whether the process is still alive.
        /// </summary>
        private static bool IsProcessAlive(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves a stable display name for one observed child process.
        /// </summary>
        private static string ResolveObservedProcessName(ProcessSnapshotEntry entry)
        {
            try
            {
                using var process = Process.GetProcessById(entry.ProcessId);
                if (!string.IsNullOrWhiteSpace(process.ProcessName))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                // Fall back to the process snapshot name.
            }

            var snapshotName = Path.GetFileNameWithoutExtension(entry.ExecutableName);
            return string.IsNullOrWhiteSpace(snapshotName)
                ? $"Process {entry.ProcessId}"
                : snapshotName;
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

#if WINDOWS
        /// <summary>
        /// Takes one snapshot of the Windows process table.
        /// </summary>
        private static List<ProcessSnapshotEntry> TakeProcessSnapshot()
        {
            var results = new List<ProcessSnapshotEntry>();
            var snapshotHandle = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshotHandle == IntPtr.Zero || snapshotHandle == InvalidHandleValue)
            {
                return results;
            }

            try
            {
                var processEntry = new ProcessEntry32
                {
                    dwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
                };

                if (!Process32First(snapshotHandle, ref processEntry))
                {
                    return results;
                }

                do
                {
                    results.Add(new ProcessSnapshotEntry
                    {
                        ProcessId = unchecked((int)processEntry.th32ProcessID),
                        ParentProcessId = unchecked((int)processEntry.th32ParentProcessID),
                        ExecutableName = processEntry.szExeFile
                    });
                }
                while (Process32Next(snapshotHandle, ref processEntry));

                return results;
            }
            finally
            {
                CloseHandle(snapshotHandle);
            }
        }

        private const uint Th32csSnapProcess = 0x00000002;
        private static readonly IntPtr InvalidHandleValue = new(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct ProcessEntry32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        private sealed class ProcessSnapshotEntry
        {
            public int ProcessId { get; set; }
            public int ParentProcessId { get; set; }
            public string ExecutableName { get; set; } = string.Empty;
        }
#endif

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
