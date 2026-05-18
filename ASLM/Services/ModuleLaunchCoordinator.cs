// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services
{
    /// <summary>
    /// Describes the outcome of a module launch attempt orchestrated by <see cref="ModuleLaunchCoordinator"/>.
    /// </summary>
    public enum ModuleLaunchStatus
    {
        Started,
        AlreadyRunning,
        NotFound,
        NoRunCommands,
        FirstRunFailed,
        Error
    }

    /// <summary>
    /// Returns the result of one coordinated launch attempt.
    /// </summary>
    public sealed record ModuleLaunchResult(
        ModuleLaunchStatus Status,
        string? Message,
        ModuleConfig? EffectiveConfig);

    /// <summary>
    /// Runs the same launch sequence as the module dashboard card (first-run, persist enabled, start run commands).
    /// </summary>
    public sealed class ModuleLaunchCoordinator
    {
        private readonly ModuleInstaller _installer;
        private readonly ModuleRunner _runner;
        private readonly ModuleStartThrottle _startThrottle;
        private readonly ILogger<ModuleLaunchCoordinator> _logger;

        /// <summary>
        /// Creates the coordinator.
        /// </summary>
        public ModuleLaunchCoordinator(
            ModuleInstaller installer,
            ModuleRunner runner,
            ModuleStartThrottle startThrottle,
            ILogger<ModuleLaunchCoordinator> logger)
        {
            _installer = installer;
            _runner = runner;
            _startThrottle = startThrottle;
            _logger = logger;
        }

        /// <summary>
        /// Resolves a module by stable id and starts it when it is not already running.
        /// </summary>
        public async Task<ModuleLaunchResult> LaunchOrEnsureRunningAsync(
            string moduleId,
            IProgress<string>? log,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return new ModuleLaunchResult(ModuleLaunchStatus.Error, "moduleId is required.", null);
            }

            var trimmedId = moduleId.Trim();
            List<ModuleConfig> matches;
            try
            {
                var modules = await _installer.DiscoverModulesAsync();
                matches = modules
                    .Where(m => string.Equals(m.Id, trimmedId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(m => m.SourcePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Module discovery failed while launching '{ModuleId}'.", trimmedId);
                return new ModuleLaunchResult(ModuleLaunchStatus.Error, ex.Message, null);
            }

            if (matches.Count == 0)
            {
                return new ModuleLaunchResult(ModuleLaunchStatus.NotFound, $"Module '{trimmedId}' was not found.", null);
            }

            if (matches.Count > 1)
            {
                _logger.LogWarning(
                    "Multiple installed modules share id '{ModuleId}'; using manifest at {SourcePath}.",
                    trimmedId,
                    matches[0].SourcePath);
            }

            await _startThrottle.WaitAsync(ct);
            try
            {
                return await LaunchOrEnsureRunningCoreAsync(matches[0], log, ct);
            }
            finally
            {
                _startThrottle.Release();
            }
        }

        private async Task<ModuleLaunchResult> LaunchOrEnsureRunningCoreAsync(
            ModuleConfig discovered,
            IProgress<string>? log,
            CancellationToken ct)
        {
            var moduleLog = log ?? new Progress<string>(_ => { });

            var fresh = await _installer.LoadModuleConfig(discovered.SourcePath);
            if (fresh == null)
            {
                return new ModuleLaunchResult(
                    ModuleLaunchStatus.NotFound,
                    $"Manifest for '{discovered.Id}' could not be loaded.",
                    null);
            }

            if (fresh.Commands.Run.Count == 0)
            {
                return new ModuleLaunchResult(
                    ModuleLaunchStatus.NoRunCommands,
                    "This module defines no run commands.",
                    fresh);
            }

            var runningPaths = _runner.GetRunningModuleSourcePaths();
            if (runningPaths.Contains(fresh.SourcePath, StringComparer.OrdinalIgnoreCase))
            {
                return new ModuleLaunchResult(ModuleLaunchStatus.AlreadyRunning, null, fresh);
            }

            if (!fresh.Status.FirstRunCompleted)
            {
                var setupSuccess = await Task.Run(
                    () => _runner.ExecuteFirstRunAsync(fresh, moduleLog, ct),
                    ct);

                if (!setupSuccess)
                {
                    return new ModuleLaunchResult(
                        ModuleLaunchStatus.FirstRunFailed,
                        "First-run setup did not complete successfully.",
                        fresh);
                }

                fresh.Status.FirstRunCompleted = true;
                await _installer.SaveConfigAsync(fresh);
            }

            fresh.Status.Enabled = true;
            await _installer.SaveConfigAsync(fresh);

            _ = Task.Run(
                () => _runner.ExecuteRunAsync(fresh, moduleLog, CancellationToken.None),
                CancellationToken.None);

            return new ModuleLaunchResult(ModuleLaunchStatus.Started, null, fresh);
        }
    }
}
