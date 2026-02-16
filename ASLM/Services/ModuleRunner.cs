using System.Diagnostics;
using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services
{
    /// <summary>
    /// Handles the execution of module commands (Run, FirstRun, Settings).
    /// Resolves engine paths via EngineInstaller and manages process execution.
    /// </summary>
    public class ModuleRunner
    {
        private readonly EngineInstaller _engineInstaller;
        private readonly ILogger<ModuleRunner> _logger;

        public ModuleRunner(EngineInstaller engineInstaller, ILogger<ModuleRunner> logger)
        {
            _engineInstaller = engineInstaller;
            _logger = logger;
        }

        /// <summary>
        /// Executes all 'FirstRun' commands for a module.
        /// First installs required engine libraries, then runs firstRun commands.
        /// Updates the module's status upon success.
        /// </summary>
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
                bool success = await RunCommandAsync(module, cmd, log, ct);
                
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
        /// These are typically long-running processes.
        /// </summary>
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
                // Run commands are long-running — we don't wait for exit
                _ = RunCommandAsync(module, cmd, log, ct);
            }

            return true;
        }

        /// <summary>
        /// Installs required libraries for each engine dependency using the
        /// engine's <see cref="EnginePackageManager"/> configuration.
        /// </summary>
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
        public async Task<bool> RunCommandAsync(
            ModuleConfig module, 
            ModuleCommand cmd, 
            IProgress<string> log, 
            CancellationToken ct)
        {
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
                    // Combine engine + script/args
                    // cmd.Exec = "manage.py runserver"
                    // We need to ensure we run it in the module directory context
                    arguments = cmd.Exec; 
                }
                else
                {
                    // Run directly (e.g. valid executable on PATH)
                    // Be careful with parsing "cmd arg1 arg2"
                    var parts = SplitCommand(cmd.Exec);
                    fileName = parts.First();
                    arguments = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";
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
                
                log.Report($"Exec: {Path.GetFileName(fileName)} {arguments}");

                using var process = new Process { StartInfo = psi };
                
                // 3. Output Handling
                process.OutputDataReceived += (s, e) => { if (e.Data != null) log.Report($"  {e.Data}"); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) log.Report($"  err: {e.Data}"); };

                // 4. Start & Wait
                if (!process.Start())
                {
                    log.Report("Failed to start process.");
                    return false;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(ct);

                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    log.Report($"Process exited with code {process.ExitCode}");
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

        private static string[] SplitCommand(string command)
        {
            return command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
