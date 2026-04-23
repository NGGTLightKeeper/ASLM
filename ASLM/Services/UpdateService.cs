// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services
{
    // Update service

    /// <summary>
    /// Checks, prepares, and applies ASLM and module updates.
    /// </summary>
    public sealed class UpdateService
    {
        private const string UpdateWorkDirName = ".aslm-update";
        private const string PendingFileName = "pending.json";
        private const string UpdateSourceFileName = "ASLM_UpdateSource.json";

        private readonly AppDataService _appData;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly GitHubUpdateClient _github;
        private readonly ILogger<UpdateService> _logger;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };


        // Initialization

        /// <summary>
        /// Creates the update service.
        /// </summary>
        public UpdateService(
            AppDataService appData,
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            GitHubUpdateClient github,
            ILogger<UpdateService> logger)
        {
            _appData = appData;
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _github = github;
            _logger = logger;
        }


        // State access

        /// <summary>
        /// Gets whether a prepared ASLM self-update is waiting for restart.
        /// </summary>
        public bool HasPendingAppUpdate => File.Exists(GetPendingUpdatePath());

        /// <summary>
        /// Returns the current ASLM app version.
        /// </summary>
        public string CurrentAppVersion =>
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.0.0";


        // Configuration

        /// <summary>
        /// Loads the shipped ASLM update source configuration.
        /// </summary>
        public async Task<AppUpdateSourceConfig?> LoadAppUpdateSourceAsync(CancellationToken ct = default)
        {
            var path = Path.Combine(GetRootDirectory(), "Data", "App", UpdateSourceFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            var config = await JsonSerializer.DeserializeAsync<AppUpdateSourceConfig>(stream, _jsonOptions, ct);
            config?.Normalize();
            return config;
        }


        // Update checks

        /// <summary>
        /// Checks the configured GitHub release channel for an ASLM build update.
        /// </summary>
        public async Task<UpdateCandidate?> CheckAppUpdateAsync(CancellationToken ct = default)
        {
            var source = await LoadAppUpdateSourceAsync(ct);
            if (!IsValidAppGitHubSource(source))
            {
                return null;
            }

            var channel = string.IsNullOrWhiteSpace(_appData.Data.Updates.AppChannel)
                ? source!.DefaultChannel
                : _appData.Data.Updates.AppChannel;
            var includePrerelease = IsPrereleaseChannel(channel);
            var release = await _github.GetLatestReleaseAsync(source!.Source.Repo, includePrerelease, ct);
            if (release == null)
            {
                return null;
            }

            var assetName = GetConfiguredAppAssetName(source);
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            var asset = release.Assets.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, assetName, StringComparison.OrdinalIgnoreCase));
            if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                return null;
            }

            if (!IsRemoteVersionNewer(release.TagName, CurrentAppVersion))
            {
                return null;
            }

            return new UpdateCandidate
            {
                TargetKind = "app",
                TargetId = "aslm",
                Name = "ASLM",
                CurrentVersion = CurrentAppVersion,
                RemoteVersion = release.TagName,
                Channel = includePrerelease ? "pre-release" : "release",
                Mode = "release",
                DownloadUrl = asset.BrowserDownloadUrl,
                ReleaseTag = release.TagName,
                IsPrerelease = release.Prerelease
            };
        }

        /// <summary>
        /// Checks one module for an available update.
        /// </summary>
        public async Task<UpdateCandidate?> CheckModuleUpdateAsync(ModuleConfig module, CancellationToken ct = default)
        {
            module.Normalize();
            if (!string.Equals(module.Source.Type, "github", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(module.Source.Repo))
            {
                return null;
            }

            if (string.Equals(module.Update.Mode, "branch", StringComparison.OrdinalIgnoreCase))
            {
                return await CheckModuleBranchUpdateAsync(module, ct);
            }

            return await CheckModuleReleaseUpdateAsync(module, ct);
        }

        /// <summary>
        /// Checks ASLM and every installed module for available updates.
        /// </summary>
        public async Task<List<UpdateCandidate>> CheckAllUpdatesAsync(CancellationToken ct = default)
        {
            var candidates = new List<UpdateCandidate>();

            try
            {
                var appCandidate = await CheckAppUpdateAsync(ct);
                if (appCandidate != null)
                {
                    candidates.Add(appCandidate);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "ASLM update check failed.");
            }

            var modules = await _moduleInstaller.DiscoverModulesAsync();
            foreach (var module in modules)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var candidate = await CheckModuleUpdateAsync(module, ct);
                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Module update check failed for {ModuleId}.", module.Id);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Returns selectable branches for one module repository.
        /// </summary>
        public Task<List<GitHubBranchInfo>> GetModuleBranchesAsync(ModuleConfig module, CancellationToken ct = default)
        {
            return string.Equals(module.Source.Type, "github", StringComparison.OrdinalIgnoreCase)
                ? _github.GetBranchesAsync(module.Source.Repo, ct)
                : Task.FromResult(new List<GitHubBranchInfo>());
        }

        /// <summary>
        /// Saves one module manifest after update preferences changed in UI.
        /// </summary>
        public void SaveModuleUpdatePreferences(ModuleConfig module)
        {
            module.Update.Normalize();
            _moduleInstaller.SaveModuleConfig(module);
        }


        // App update preparation

        /// <summary>
        /// Downloads and stages an ASLM app update for the external patcher.
        /// </summary>
        public async Task<bool> PrepareAppUpdateAsync(
            UpdateCandidate candidate,
            IProgress<string>? log = null,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken ct = default)
        {
            var source = await LoadAppUpdateSourceAsync(ct);
            if (source == null)
            {
                log?.Report("ASLM update source is not configured.");
                return false;
            }

            var rootDir = GetRootDirectory();
            var workDir = Path.Combine(rootDir, UpdateWorkDirName);
            var stagingRoot = Path.Combine(workDir, "staging", SanitizeFileName(candidate.RemoteVersion));
            var extractDir = Path.Combine(stagingRoot, "extract");
            var archivePath = Path.Combine(stagingRoot, "download.zip");
            var backupPath = Path.Combine(workDir, "backup", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));

            TryDeleteDirectory(stagingRoot);
            Directory.CreateDirectory(extractDir);

            await _github.DownloadFileAsync(candidate.DownloadUrl, archivePath, log, progress, ct);
            ExtractZipSafe(archivePath, extractDir);

            // GitHub assets may unpack either directly into the payload root or into a single wrapper folder.
            var payloadDir = ResolveSinglePayloadDirectory(extractDir);
            var pending = new PendingAppUpdate
            {
                Version = candidate.RemoteVersion,
                StagingPath = payloadDir,
                TargetRoot = rootDir,
                BackupPath = backupPath,
                Preserve = source.Preserve,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            };

            Directory.CreateDirectory(workDir);
            var pendingJson = JsonSerializer.Serialize(pending, _jsonOptions);
            await File.WriteAllTextAsync(GetPendingUpdatePath(), pendingJson, ct);
            log?.Report("ASLM update prepared. It will be applied on restart.");
            return true;
        }


        // Module update application

        /// <summary>
        /// Downloads and applies one module update.
        /// </summary>
        public async Task<bool> ApplyModuleUpdateAsync(
            UpdateCandidate candidate,
            IProgress<string>? log = null,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken ct = default)
        {
            var module = candidate.Module ?? throw new InvalidOperationException(
                "Module update candidate does not contain module metadata.");
            var moduleDir = Path.GetDirectoryName(module.SourcePath);
            if (string.IsNullOrWhiteSpace(moduleDir))
            {
                return false;
            }

            var updateRoot = Path.Combine(
                GetRootDirectory(),
                UpdateWorkDirName,
                "modules",
                module.Id,
                Guid.NewGuid().ToString("N"));
            var archivePath = Path.Combine(updateRoot, "module.zip");
            var extractDir = Path.Combine(updateRoot, "extract");
            var backupDir = Path.Combine(updateRoot, "backup");
            var preserveDir = Path.Combine(updateRoot, "preserve");

            try
            {
                Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(backupDir);
                Directory.CreateDirectory(preserveDir);

                await DownloadModuleArchiveAsync(module, candidate, archivePath, log, progress, ct);
                ExtractZipSafe(archivePath, extractDir);

                var newManifestPath = FindUpdatedModuleManifest(extractDir);
                var newConfig = await LoadModuleConfigFromPathAsync(newManifestPath, ct);
                if (newConfig == null || !string.Equals(newConfig.Id, module.Id, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Downloaded module archive does not match the installed module id.");
                }

                var moduleSourceDir = Path.GetDirectoryName(newManifestPath)!;
                var wasEnabled = module.Status.Enabled;

                if (wasEnabled)
                {
                    log?.Report($"Stopping {module.Name} before update...");
                    await _moduleRunner.StopModuleAsync(module.SourcePath);
                    module.Status.Enabled = false;
                    _moduleInstaller.SaveModuleConfig(module);
                }

                CopyDirectory(moduleDir, backupDir);
                CopyPreservedPaths(moduleDir, preserveDir, module.Update.Preserve, log);

                try
                {
                    // Replace the whole module directory so stale files disappear instead of silently lingering.
                    ClearDirectory(moduleDir);
                    CopyDirectory(moduleSourceDir, moduleDir);
                    RestorePreservedPaths(preserveDir, moduleDir, log);

                    var installedPath = Path.Combine(moduleDir, "ASLM_Module.json");
                    var installed = await _moduleInstaller.LoadModuleConfig(installedPath)
                        ?? throw new InvalidOperationException("Updated module manifest could not be loaded.");

                    MergeModuleState(module, installed, candidate);
                    await _moduleInstaller.SaveConfigAsync(installed);

                    if (installed.Update.RunFirstRunAfterUpdate)
                    {
                        log?.Report($"Running first-run setup for {installed.Name}...");
                        installed.Status.FirstRunCompleted = false;

                        var setupSuccess = await _moduleRunner.ExecuteFirstRunAsync(
                            installed,
                            log ?? NoOpProgress<string>.Instance,
                            ct);

                        if (!setupSuccess)
                        {
                            await _moduleInstaller.SaveConfigAsync(installed);
                            log?.Report("Module update applied, but setup failed.");
                            return false;
                        }
                    }

                    if (wasEnabled && installed.Commands.Run.Count > 0)
                    {
                        installed.Status.Enabled = true;
                        await _moduleInstaller.SaveConfigAsync(installed);
                        _ = Task.Run(() => _moduleRunner.ExecuteRunAsync(
                            installed,
                            log ?? NoOpProgress<string>.Instance,
                            CancellationToken.None));
                    }

                    installed.Status.LastUpdated = DateTime.UtcNow.ToString("o");
                    await _moduleInstaller.SaveConfigAsync(installed);
                    log?.Report($"{installed.Name} updated to {candidate.RemoteVersion}.");
                    return true;
                }
                catch
                {
                    log?.Report("Update failed. Restoring previous module files...");
                    ClearDirectory(moduleDir);
                    CopyDirectory(backupDir, moduleDir);
                    throw;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Module update failed for {ModuleId}.", module.Id);
                log?.Report($"Module update failed: {ex.Message}");
                return false;
            }
            finally
            {
                TryDeleteDirectory(updateRoot);
            }
        }


        // App check helpers

        /// <summary>
        /// Returns whether the shipped ASLM update source contains a valid GitHub configuration.
        /// </summary>
        private static bool IsValidAppGitHubSource(AppUpdateSourceConfig? source)
        {
            return source != null &&
                   string.Equals(source.Source.Type, "github", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(source.Source.Repo);
        }

        /// <summary>
        /// Returns the configured ASLM asset name for the current OS and architecture.
        /// </summary>
        private string? GetConfiguredAppAssetName(AppUpdateSourceConfig source)
        {
            var assetKey = ResolveCurrentAppAssetKey();
            if (source.Assets.TryGetValue(assetKey, out var assetName) && !string.IsNullOrWhiteSpace(assetName))
            {
                return assetName;
            }

            _logger.LogWarning("ASLM update asset for key '{AssetKey}' is not configured.", assetKey);
            return null;
        }


        // Module check helpers

        /// <summary>
        /// Checks whether a branch-tracked module has moved to a newer commit.
        /// </summary>
        private async Task<UpdateCandidate?> CheckModuleBranchUpdateAsync(ModuleConfig module, CancellationToken ct)
        {
            var branches = await _github.GetBranchesAsync(module.Source.Repo, ct);
            var selected = branches.FirstOrDefault(branch =>
                string.Equals(branch.Name, module.Update.Branch, StringComparison.OrdinalIgnoreCase));

            if (selected == null ||
                string.IsNullOrWhiteSpace(selected.CommitSha) ||
                string.Equals(selected.CommitSha, module.Update.InstalledCommitSha, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new UpdateCandidate
            {
                TargetKind = "module",
                TargetId = module.Id,
                Name = module.Name,
                CurrentVersion = BuildModuleCurrentVersion(module),
                RemoteVersion = $"{selected.Name}@{ShortSha(selected.CommitSha)}",
                Channel = "branch",
                Mode = "branch",
                CommitSha = selected.CommitSha,
                Module = module
            };
        }

        /// <summary>
        /// Checks whether a release-tracked module has a newer GitHub release.
        /// </summary>
        private async Task<UpdateCandidate?> CheckModuleReleaseUpdateAsync(ModuleConfig module, CancellationToken ct)
        {
            var includePrerelease = IsPrereleaseChannel(module.Update.Channel);
            var release = await _github.GetLatestReleaseAsync(module.Source.Repo, includePrerelease, ct);
            if (release == null)
            {
                return null;
            }

            var downloadUrl = ResolveModuleDownloadUrl(module, release);
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return null;
            }

            var currentTag = module.Update.InstalledReleaseTag ?? module.Status.InstalledVersion ?? module.Version;
            if (string.Equals(currentTag, release.TagName, StringComparison.OrdinalIgnoreCase) ||
                !IsRemoteVersionNewer(release.TagName, currentTag))
            {
                return null;
            }

            return new UpdateCandidate
            {
                TargetKind = "module",
                TargetId = module.Id,
                Name = module.Name,
                CurrentVersion = BuildModuleCurrentVersion(module),
                RemoteVersion = release.TagName,
                Channel = includePrerelease ? "pre-release" : "release",
                Mode = "release",
                DownloadUrl = downloadUrl,
                ReleaseTag = release.TagName,
                IsPrerelease = release.Prerelease,
                Module = module
            };
        }

        /// <summary>
        /// Resolves the archive URL used to update one module release.
        /// </summary>
        private static string ResolveModuleDownloadUrl(ModuleConfig module, GitHubReleaseInfo release)
        {
            if (!string.IsNullOrWhiteSpace(module.Update.AssetName))
            {
                return release.Assets.FirstOrDefault(asset =>
                    string.Equals(asset.Name, module.Update.AssetName, StringComparison.OrdinalIgnoreCase))
                    ?.BrowserDownloadUrl ?? string.Empty;
            }

            return release.ZipballUrl;
        }


        // Module apply helpers

        /// <summary>
        /// Downloads the archive for one module update candidate.
        /// </summary>
        private async Task DownloadModuleArchiveAsync(
            ModuleConfig module,
            UpdateCandidate candidate,
            string archivePath,
            IProgress<string>? log,
            IProgress<DownloadProgress>? progress,
            CancellationToken ct)
        {
            if (string.Equals(candidate.Mode, "branch", StringComparison.OrdinalIgnoreCase))
            {
                await _github.DownloadRepositoryZipAsync(
                    module.Source.Repo,
                    module.Update.Branch,
                    archivePath,
                    log,
                    progress,
                    ct);
                return;
            }

            await _github.DownloadFileAsync(candidate.DownloadUrl, archivePath, log, progress, ct);
        }

        /// <summary>
        /// Locates the module manifest inside one extracted archive.
        /// </summary>
        private static string FindUpdatedModuleManifest(string extractDir)
        {
            var manifestPath = Directory
                .EnumerateFiles(extractDir, "ASLM_Module.json", SearchOption.AllDirectories)
                .FirstOrDefault();

            return manifestPath ?? throw new InvalidOperationException(
                "Downloaded module archive does not contain ASLM_Module.json.");
        }

        /// <summary>
        /// Loads one module configuration directly from a specific manifest path.
        /// </summary>
        private async Task<ModuleConfig?> LoadModuleConfigFromPathAsync(string path, CancellationToken ct)
        {
            await using var stream = File.OpenRead(path);
            var config = await JsonSerializer.DeserializeAsync<ModuleConfig>(stream, _jsonOptions, ct);
            config?.Normalize();
            return config;
        }

        /// <summary>
        /// Carries forward user state and update metadata from the old module into the new manifest.
        /// </summary>
        private static void MergeModuleState(ModuleConfig oldConfig, ModuleConfig newConfig, UpdateCandidate candidate)
        {
            var oldSettingValues = oldConfig.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                .ToDictionary(
                    setting => setting.Key,
                    setting => (setting.Value, setting.UseCustomValue),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var setting in newConfig.Settings)
            {
                if (!oldSettingValues.TryGetValue(setting.Key, out var oldValue))
                {
                    continue;
                }

                setting.Value = oldValue.Value;
                setting.UseCustomValue = oldValue.UseCustomValue;
            }

            // Runtime status and update settings are owned by the local installation, not by the downloaded package.
            newConfig.Status.Installed = true;
            newConfig.Status.Enabled = false;
            newConfig.Status.InstalledVersion = newConfig.Version;
            newConfig.Status.LastChecked = DateTime.UtcNow.ToString("o");
            newConfig.Status.LastUpdated = DateTime.UtcNow.ToString("o");

            newConfig.Update.Mode = oldConfig.Update.Mode;
            newConfig.Update.Channel = oldConfig.Update.Channel;
            newConfig.Update.Branch = oldConfig.Update.Branch;
            newConfig.Update.AssetName = oldConfig.Update.AssetName ?? newConfig.Update.AssetName;
            newConfig.Update.RunFirstRunAfterUpdate = oldConfig.Update.RunFirstRunAfterUpdate;
            newConfig.Update.Preserve = oldConfig.Update.Preserve
                .Concat(newConfig.Update.Preserve)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            newConfig.Update.InstalledCommitSha = candidate.CommitSha ?? oldConfig.Update.InstalledCommitSha;
            newConfig.Update.InstalledReleaseTag = candidate.ReleaseTag ?? oldConfig.Update.InstalledReleaseTag;
            newConfig.Update.Normalize();
        }


        // Version helpers

        /// <summary>
        /// Returns whether the selected update channel should include GitHub pre-releases.
        /// </summary>
        private static bool IsPrereleaseChannel(string? channel)
        {
            return string.Equals(channel, "pre-release", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(channel, "prerelease", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the current application asset key from OS and architecture.
        /// </summary>
        private static string ResolveCurrentAppAssetKey()
        {
            var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? "linux"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                        ? "osx"
                        : throw new PlatformNotSupportedException("The current OS is not supported by the updater.");

            var architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => throw new PlatformNotSupportedException(
                    $"The current architecture '{RuntimeInformation.ProcessArchitecture}' is not supported by the updater.")
            };

            return $"{os}-{architecture}";
        }

        /// <summary>
        /// Returns the best local version label for one module.
        /// </summary>
        private static string BuildModuleCurrentVersion(ModuleConfig module)
        {
            if (string.Equals(module.Update.Mode, "branch", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(module.Update.InstalledCommitSha))
            {
                return $"{module.Update.Branch}@{ShortSha(module.Update.InstalledCommitSha)}";
            }

            return module.Update.InstalledReleaseTag ?? module.Status.InstalledVersion ?? module.Version;
        }

        /// <summary>
        /// Returns whether a remote version should be treated as newer than the current version.
        /// </summary>
        private static bool IsRemoteVersionNewer(string remoteVersion, string currentVersion)
        {
            var remote = NormalizeVersion(remoteVersion);
            var current = NormalizeVersion(currentVersion);
            if (Version.TryParse(remote, out var remoteParsed) && Version.TryParse(current, out var currentParsed))
            {
                return remoteParsed > currentParsed;
            }

            return !string.Equals(remoteVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Normalizes common Git tag prefixes and suffixes before semantic comparison.
        /// </summary>
        private static string NormalizeVersion(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            {
                normalized = normalized[1..];
            }

            var suffixIndex = normalized.IndexOfAny(['-', '+']);
            return suffixIndex > 0 ? normalized[..suffixIndex] : normalized;
        }

        /// <summary>
        /// Returns a short display form of one commit SHA.
        /// </summary>
        private static string ShortSha(string sha)
        {
            return string.IsNullOrWhiteSpace(sha) ? string.Empty : sha[..Math.Min(7, sha.Length)];
        }


        // File system helpers

        /// <summary>
        /// Extracts one ZIP archive and rejects entries that escape the target directory.
        /// </summary>
        private static void ExtractZipSafe(string zipPath, string destination)
        {
            Directory.CreateDirectory(destination);
            var destinationPrefix = EnsureTrailingSeparator(Path.GetFullPath(destination));

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                var targetPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!targetPath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"ZIP entry '{entry.FullName}' escapes the destination directory.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                entry.ExtractToFile(targetPath, overwrite: true);
            }
        }

        /// <summary>
        /// Collapses archives that unpack into a single wrapper folder.
        /// </summary>
        private static string ResolveSinglePayloadDirectory(string extractDir)
        {
            var directories = Directory.GetDirectories(extractDir);
            var files = Directory.GetFiles(extractDir);
            return directories.Length == 1 && files.Length == 0 ? directories[0] : extractDir;
        }

        /// <summary>
        /// Copies the requested preserved files and directories out of one module before replacement.
        /// </summary>
        private static void CopyPreservedPaths(
            string sourceRoot,
            string preserveRoot,
            IEnumerable<string> relativePaths,
            IProgress<string>? log)
        {
            foreach (var relativePath in relativePaths)
            {
                var sourcePath = ResolveChildPath(sourceRoot, relativePath);
                if (File.Exists(sourcePath))
                {
                    var destinationPath = ResolveChildPath(preserveRoot, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                    log?.Report($"Preserved file: {relativePath}");
                    continue;
                }

                if (Directory.Exists(sourcePath))
                {
                    var destinationPath = ResolveChildPath(preserveRoot, relativePath);
                    CopyDirectory(sourcePath, destinationPath);
                    log?.Report($"Preserved directory: {relativePath}");
                }
            }
        }

        /// <summary>
        /// Restores preserved files and directories back into one replaced module directory.
        /// </summary>
        private static void RestorePreservedPaths(string preserveRoot, string targetRoot, IProgress<string>? log)
        {
            if (!Directory.Exists(preserveRoot))
            {
                return;
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(preserveRoot))
            {
                var relativePath = Path.GetRelativePath(preserveRoot, entry);
                var targetPath = ResolveChildPath(targetRoot, relativePath);

                if (File.Exists(entry))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    File.Copy(entry, targetPath, overwrite: true);
                    log?.Report($"Restored file: {relativePath}");
                    continue;
                }

                if (Directory.Exists(entry))
                {
                    CopyDirectory(entry, targetPath);
                    log?.Report($"Restored directory: {relativePath}");
                }
            }
        }

        /// <summary>
        /// Recursively copies one directory into another.
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.EnumerateFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var subdir in Directory.EnumerateDirectories(sourceDir))
            {
                CopyDirectory(subdir, Path.Combine(destDir, Path.GetFileName(subdir)));
            }
        }

        /// <summary>
        /// Removes every file and directory inside one root directory.
        /// </summary>
        private static void ClearDirectory(string directory)
        {
            Directory.CreateDirectory(directory);

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var subdir in Directory.EnumerateDirectories(directory))
            {
                Directory.Delete(subdir, recursive: true);
            }
        }

        /// <summary>
        /// Returns the full path to the pending ASLM self-update file.
        /// </summary>
        private string GetPendingUpdatePath()
        {
            return Path.Combine(GetRootDirectory(), UpdateWorkDirName, PendingFileName);
        }

        /// <summary>
        /// Resolves one relative child path and rejects directory traversal.
        /// </summary>
        private static string ResolveChildPath(string rootPath, string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
            {
                throw new InvalidOperationException("Absolute paths are not allowed.");
            }

            var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
            var combined = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
            if (!EnsureTrailingSeparator(combined).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Path '{relativePath}' escapes the target root.");
            }

            return combined;
        }

        /// <summary>
        /// Ensures directory paths always end with a separator for secure prefix checks.
        /// </summary>
        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Sanitizes free-form text so it can be used as a filesystem name.
        /// </summary>
        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string((value ?? string.Empty)
                .Select(character => invalidChars.Contains(character) ? '_' : character)
                .ToArray());

            return string.IsNullOrWhiteSpace(sanitized) ? Guid.NewGuid().ToString("N") : sanitized;
        }

        /// <summary>
        /// Deletes one directory on a best-effort basis.
        /// </summary>
        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }
        }

        /// <summary>
        /// Returns the ASLM root directory above the running App folder.
        /// </summary>
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }


        // Helper types

        /// <summary>
        /// Represents a no-op progress sink used when the caller does not provide logging.
        /// </summary>
        private sealed class NoOpProgress<T> : IProgress<T>
        {
            public static NoOpProgress<T> Instance { get; } = new();

            /// <summary>
            /// Ignores one reported value.
            /// </summary>
            public void Report(T value)
            {
            }
        }
    }
}
