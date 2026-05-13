// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services
{
    // Update manager

    /// <summary>
    /// Checks, prepares, and applies ASLM and module updates.
    /// </summary>
    public sealed class UpdateManager
    {
        private const string UpdateWorkDirName = ".aslm-update";
        private const string PendingFileName = "pending.json";
        private const string UpdateSourceFileName = "ASLM_UpdateSource.json";

        private readonly AppDataStore _appData;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly GitHubUpdateClient _github;
        private readonly NotificationCenter _notifications;
        private readonly ILogger<UpdateManager> _logger;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };


        // Initialization

        /// <summary>
        /// Creates the update manager.
        /// </summary>
        public UpdateManager(
            AppDataStore appData,
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            GitHubUpdateClient github,
            NotificationCenter notifications,
            ILogger<UpdateManager> logger)
        {
            _appData = appData;
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _github = github;
            _notifications = notifications;
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
        public string CurrentAppVersion => ResolveCurrentAppDisplayVersion();


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
            var appSource = source!;
            var assetName = GetConfiguredAppAssetName(appSource);
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            var releases = await _github.GetReleasesAsync(appSource.Source.Repo, includePrerelease, ct);
            foreach (var release in releases)
            {
                var asset = release.Assets.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, assetName, StringComparison.OrdinalIgnoreCase));
                if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
                {
                    continue;
                }

                if (!ShouldOfferAppUpdate(release.TagName, includePrerelease))
                {
                    continue;
                }

                var candidate = new UpdateCandidate
                {
                    TargetKind = "app",
                    TargetId = "aslm",
                    Name = "ASLM",
                    CurrentVersion = ResolveCurrentAppReleaseReference(),
                    RemoteVersion = release.TagName,
                    Channel = includePrerelease ? "pre-release" : "release",
                    Mode = "release",
                    DownloadUrl = asset.BrowserDownloadUrl,
                    ReleaseTag = release.TagName,
                    IsPrerelease = release.Prerelease
                };

                _notifications.PublishUpdateCandidate(candidate);
                return candidate;
            }

            return null;
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
                var branchCandidate = await CheckModuleBranchUpdateAsync(module, ct);
                if (branchCandidate != null)
                {
                    _notifications.PublishUpdateCandidate(branchCandidate);
                }

                return branchCandidate;
            }

            var releaseCandidate = await CheckModuleReleaseUpdateAsync(module, ct);
            if (releaseCandidate != null)
            {
                _notifications.PublishUpdateCandidate(releaseCandidate);
            }

            return releaseCandidate;
        }

        /// <summary>
        /// Resolves the concrete install target that should be used for a module during setup.
        /// </summary>
        public async Task<UpdateCandidate?> ResolveModuleInstallCandidateAsync(
            ModuleConfig module,
            CancellationToken ct = default)
        {
            module.Normalize();
            if (!string.Equals(module.Source.Type, "github", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(module.Source.Repo))
            {
                return null;
            }

            if (string.Equals(module.Update.Mode, "branch", StringComparison.OrdinalIgnoreCase))
            {
                return await ResolveModuleBranchInstallCandidateAsync(module, ct);
            }

            return await ResolveModuleReleaseInstallCandidateAsync(module, ct);
        }

        /// <summary>
        /// Returns selectable release or pre-release versions for one module repository.
        /// </summary>
        public async Task<List<UpdateCandidate>> GetModuleReleaseCandidatesAsync(
            ModuleConfig module,
            CancellationToken ct = default)
        {
            module.Normalize();
            if (!string.Equals(module.Source.Type, "github", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(module.Source.Repo))
            {
                return [];
            }

            var includePrerelease = IsPrereleaseMode(module.Update.Mode);
            var releases = await _github.GetReleasesAsync(module.Source.Repo, includePrerelease, ct);
            var candidates = new List<UpdateCandidate>();

            foreach (var release in releases)
            {
                var downloadUrl = ResolveModuleDownloadUrl(module, release);
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    continue;
                }

                candidates.Add(BuildModuleReleaseCandidate(module, release, downloadUrl));
            }

            return candidates;
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
            // Persist without ModulesChanged: the shell rebuilds module cards on that event and would drop
            // in-memory update-check results (HasUpdate / candidate) tied to the current ModuleViewModel instances.
            _moduleInstaller.SaveModuleConfig(module, raiseModulesChanged: false);
        }

        /// <summary>
        /// Rebuilds a module <see cref="UpdateCandidate"/> from <see cref="ModuleUpdateConfig.PendingUpdate"/> when the
        /// persisted snapshot still reflects an update newer than the local installation.
        /// </summary>
        public bool TryRestorePendingUpdateCandidate(ModuleConfig module, [NotNullWhen(true)] out UpdateCandidate? candidate)
        {
            candidate = null;
            module.Normalize();
            var pending = module.Update.PendingUpdate;
            if (pending == null || string.IsNullOrWhiteSpace(pending.RemoteVersion))
            {
                return false;
            }

            pending.Normalize();
            if (string.IsNullOrWhiteSpace(pending.RemoteVersion))
            {
                return false;
            }

            if (!string.Equals(pending.UpdateMode, module.Update.Mode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(module.Update.Mode, "branch", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(pending.CommitSha) || string.IsNullOrWhiteSpace(pending.Branch))
                {
                    return false;
                }

                if (!string.Equals(
                    pending.Branch.Trim(),
                    module.Update.Branch.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.Equals(
                    pending.CommitSha.Trim(),
                    module.Update.InstalledCommitSha?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                candidate = new UpdateCandidate
                {
                    TargetKind = "module",
                    TargetId = module.Id,
                    Name = module.Name,
                    CurrentVersion = BuildModuleCurrentVersion(module),
                    RemoteVersion = pending.RemoteVersion,
                    Channel = "branch",
                    Mode = "branch",
                    ReferenceName = pending.ReferenceName ?? pending.Branch,
                    CommitSha = pending.CommitSha,
                    Module = module
                };
                return true;
            }

            if (string.IsNullOrWhiteSpace(pending.ReleaseTag))
            {
                return false;
            }

            var installed = ResolveInstalledModuleReleaseTag(module);
            if (AreEquivalentVersionReferences(pending.ReleaseTag, installed))
            {
                return false;
            }

            if (!IsLatestReleaseSelection(module.Update.SelectedReleaseTag) &&
                !string.IsNullOrWhiteSpace(module.Update.SelectedReleaseTag) &&
                !string.IsNullOrWhiteSpace(pending.ReleaseSelectionKey) &&
                !string.Equals(
                    pending.ReleaseSelectionKey.Trim(),
                    module.Update.SelectedReleaseTag.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var displayName = pending.IsVirtualLatest
                ? ModuleUpdateConfig.LatestReleaseTag
                : (!string.IsNullOrWhiteSpace(pending.DisplayName) ? pending.DisplayName : pending.RemoteVersion);

            var channel = !string.IsNullOrWhiteSpace(pending.Channel)
                ? pending.Channel
                : (IsPrereleaseMode(module.Update.Mode) ? "pre-release" : "release");

            candidate = new UpdateCandidate
            {
                TargetKind = "module",
                TargetId = module.Id,
                Name = module.Name,
                DisplayName = displayName,
                CurrentVersion = BuildModuleCurrentVersion(module),
                RemoteVersion = pending.RemoteVersion,
                Channel = channel,
                Mode = module.Update.Mode,
                DownloadUrl = pending.DownloadUrl ?? string.Empty,
                ReleaseTag = pending.ReleaseTag,
                IsVirtualLatest = pending.IsVirtualLatest,
                IsPrerelease = pending.IsPrerelease,
                PublishedAt = null,
                Module = module
            };
            return true;
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
            var operationKey = NotificationCenter.BuildOperationKey("app-update", candidate.TargetId);
            await _notifications.StartDownloadAsync(
                operationKey,
                "Preparing ASLM update",
                $"{candidate.Name} {candidate.RemoteVersion}",
                candidate.TargetKind,
                candidate.TargetId);

            var source = await LoadAppUpdateSourceAsync(ct);
            if (source == null)
            {
                log?.Report("ASLM update source is not configured.");
                _notifications.FailDownload(operationKey, "ASLM update source is not configured.");
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

            var notificationProgress = _notifications.CreateDownloadProgressBridge(operationKey, progress);

            try
            {
                await _github.DownloadFileAsync(candidate.DownloadUrl, archivePath, log, notificationProgress, ct);
                var payloadDir = await Task.Run(() =>
                {
                    // Archive extraction and payload inspection can touch a lot of files, so keep it off the UI thread.
                    ExtractZipSafe(archivePath, extractDir);
                    return ResolveSinglePayloadDirectory(extractDir);
                }, ct);

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
                _notifications.CompleteDownload(operationKey, "ASLM update prepared.");
                return true;
            }
            catch (OperationCanceledException)
            {
                _notifications.FailDownload(operationKey, "ASLM update download canceled.");
                throw;
            }
            catch
            {
                _notifications.FailDownload(operationKey, "ASLM update download failed.");
                throw;
            }
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
            module.Normalize();

            if (IsModuleAlreadyAtInstallTarget(module, candidate))
            {
                log?.Report("The selected version is already installed.");
                return false;
            }

            var operationKey = NotificationCenter.BuildOperationKey("module-update", module.Id);
            await _notifications.StartDownloadAsync(
                operationKey,
                "Updating module",
                $"{module.Name} {candidate.RemoteVersion}",
                candidate.TargetKind,
                candidate.TargetId);

            var moduleDir = Path.GetDirectoryName(module.SourcePath);
            if (string.IsNullOrWhiteSpace(moduleDir))
            {
                _notifications.FailDownload(operationKey, "Module update target could not be resolved.");
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

            try
            {
                Directory.CreateDirectory(extractDir);

                var notificationProgress = _notifications.CreateDownloadProgressBridge(operationKey, progress);
                await DownloadModuleArchiveAsync(module, candidate, archivePath, log, notificationProgress, ct);
                var preparedUpdate = await Task.Run(() =>
                {
                    // Extraction and manifest validation are local filesystem work and should not block the UI thread.
                    ExtractZipSafe(archivePath, extractDir);

                    var newManifestPath = FindUpdatedModuleManifest(extractDir);
                    var newConfig = LoadModuleConfigFromPath(newManifestPath);
                    return new PreparedModuleUpdate(
                        NewManifestPath: newManifestPath,
                        NewConfig: newConfig,
                        ModuleSourceDir: Path.GetDirectoryName(newManifestPath)!);
                }, ct);

                var newConfig = preparedUpdate.NewConfig;
                if (newConfig == null || !string.Equals(newConfig.Id, module.Id, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Downloaded module archive does not match the installed module id.");
                }

                var wasEnabled = module.Status.Enabled;

                if (wasEnabled)
                {
                    log?.Report($"Stopping {module.Name} before update...");
                    await _moduleRunner.StopModuleAsync(module.SourcePath);
                    module.Status.Enabled = false;
                    _moduleInstaller.SaveModuleConfig(module, raiseModulesChanged: false);
                }

                await StopProcessesRunningFromDirectoryAsync(moduleDir, log, ct);

                var success = await ApplyPreparedModuleUpdateAsync(
                    module,
                    candidate,
                    preparedUpdate,
                    moduleDir,
                    wasEnabled,
                    log,
                    ct);
                if (success)
                {
                    _notifications.CompleteDownload(operationKey, $"{module.Name} updated successfully.");
                }
                else
                {
                    _notifications.FailDownload(operationKey, $"{module.Name} update failed.");
                }

                return success;
            }
            catch (OperationCanceledException)
            {
                _notifications.FailDownload(operationKey, $"{module.Name} update canceled.");
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Module update failed for {ModuleId}.", module.Id);
                log?.Report($"Module update failed: {ex.Message}");
                _notifications.FailDownload(operationKey, $"Module update failed: {ex.Message}");
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
                ReferenceName = selected.Name,
                CommitSha = selected.CommitSha,
                Module = module
            };
        }

        /// <summary>
        /// Resolves the branch install target selected by the module manifest.
        /// </summary>
        private async Task<UpdateCandidate?> ResolveModuleBranchInstallCandidateAsync(
            ModuleConfig module,
            CancellationToken ct)
        {
            var branches = await _github.GetBranchesAsync(module.Source.Repo, ct);
            var selected = branches.FirstOrDefault(branch =>
                string.Equals(branch.Name, module.Update.Branch, StringComparison.OrdinalIgnoreCase));
            if (selected == null || string.IsNullOrWhiteSpace(selected.CommitSha))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(module.Update.InstalledCommitSha) &&
                string.Equals(
                    selected.CommitSha.Trim(),
                    module.Update.InstalledCommitSha.Trim(),
                    StringComparison.OrdinalIgnoreCase))
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
                ReferenceName = selected.Name,
                CommitSha = selected.CommitSha,
                Module = module
            };
        }

        /// <summary>
        /// Checks whether a release-tracked module has a newer GitHub release.
        /// </summary>
        private async Task<UpdateCandidate?> CheckModuleReleaseUpdateAsync(ModuleConfig module, CancellationToken ct)
        {
            var currentTag = ResolveInstalledModuleReleaseTag(module);
            var candidates = await GetModuleReleaseCandidatesAsync(module, ct);
            if (candidates.Count == 0)
            {
                return null;
            }

            if (!IsLatestReleaseSelection(module.Update.SelectedReleaseTag) &&
                !string.IsNullOrWhiteSpace(module.Update.SelectedReleaseTag))
            {
                var selected = candidates.FirstOrDefault(candidate =>
                    string.Equals(candidate.ReleaseTag, module.Update.SelectedReleaseTag, StringComparison.OrdinalIgnoreCase));

                if (selected == null || string.IsNullOrWhiteSpace(selected.ReleaseTag))
                {
                    return null;
                }

                return !AreEquivalentVersionReferences(selected.ReleaseTag, currentTag) ? selected : null;
            }

            var latest = candidates[0];
            return !AreEquivalentVersionReferences(latest.ReleaseTag ?? string.Empty, currentTag)
                ? latest
                : null;
        }

        /// <summary>
        /// Resolves the release install target selected by the module manifest.
        /// </summary>
        private async Task<UpdateCandidate?> ResolveModuleReleaseInstallCandidateAsync(
            ModuleConfig module,
            CancellationToken ct)
        {
            var candidates = await GetModuleReleaseCandidatesAsync(module, ct);
            if (candidates.Count == 0)
            {
                return null;
            }

            UpdateCandidate resolved;
            if (!IsLatestReleaseSelection(module.Update.SelectedReleaseTag) &&
                !string.IsNullOrWhiteSpace(module.Update.SelectedReleaseTag))
            {
                var selected = candidates.FirstOrDefault(candidate =>
                    string.Equals(candidate.ReleaseTag, module.Update.SelectedReleaseTag, StringComparison.OrdinalIgnoreCase));
                resolved = selected ?? candidates[0];
            }
            else
            {
                resolved = candidates[0];
            }

            if (string.IsNullOrWhiteSpace(resolved.ReleaseTag))
            {
                return null;
            }

            var installed = ResolveInstalledModuleReleaseTag(module);
            return AreEquivalentVersionReferences(resolved.ReleaseTag, installed) ? null : resolved;
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

        /// <summary>
        /// Builds one release-backed module update candidate from a GitHub release payload.
        /// </summary>
        private static UpdateCandidate BuildModuleReleaseCandidate(
            ModuleConfig module,
            GitHubReleaseInfo release,
            string downloadUrl)
        {
            return new UpdateCandidate
            {
                TargetKind = "module",
                TargetId = module.Id,
                Name = module.Name,
                DisplayName = BuildReleaseDisplayName(release),
                CurrentVersion = BuildModuleCurrentVersion(module),
                RemoteVersion = release.TagName,
                Channel = release.Prerelease ? "pre-release" : "release",
                Mode = module.Update.Mode,
                DownloadUrl = downloadUrl,
                ReleaseTag = release.TagName,
                IsPrerelease = release.Prerelease,
                PublishedAt = release.PublishedAt,
                Module = module
            };
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
                    candidate.ReferenceName ?? module.Update.Branch,
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
        /// Loads one module configuration directly from a specific manifest path synchronously.
        /// </summary>
        private ModuleConfig? LoadModuleConfigFromPath(string path)
        {
            using var stream = File.OpenRead(path);
            var config = JsonSerializer.Deserialize<ModuleConfig>(stream, _jsonOptions);
            config?.Normalize();
            return config;
        }

        /// <summary>
        /// Applies a prepared module update while keeping the heavy file operations off the UI thread.
        /// </summary>
        private async Task<bool> ApplyPreparedModuleUpdateAsync(
            ModuleConfig module,
            UpdateCandidate candidate,
            PreparedModuleUpdate preparedUpdate,
            string moduleDir,
            bool wasEnabled,
            IProgress<string>? log,
            CancellationToken ct)
        {
            try
            {
                await Task.Run(async () =>
                {
                    var preservePaths = BuildPreservePathSet(module.Update.Preserve);
                    ReportPreservedPaths(moduleDir, preservePaths, log);

                    // Replace stale module files while leaving declared local state in place.
                    await ClearDirectoryForUpdateAsync(moduleDir, preservePaths, log, ct);
                    CopyDirectory(preparedUpdate.ModuleSourceDir, moduleDir, preservePaths);
                }, ct);

                var installedPath = Path.Combine(moduleDir, "ASLM_Module.json");
                var installed = await _moduleInstaller.LoadModuleConfig(installedPath)
                    ?? throw new InvalidOperationException("Updated module manifest could not be loaded.");

                MergeModuleState(module, installed, candidate);
                await _moduleInstaller.SaveConfigAsync(installed, raiseModulesChanged: false);

                if (installed.Update.RunFirstRunAfterUpdate)
                {
                    log?.Report($"Running first-run setup for {installed.Name}...");
                    installed.Status.FirstRunCompleted = false;

                    var setupSuccess = await Task.Run(
                        () => _moduleRunner.ExecuteFirstRunAsync(
                            installed,
                            log ?? NoOpProgress<string>.Instance,
                            ct),
                        ct);

                    if (!setupSuccess)
                    {
                        await _moduleInstaller.SaveConfigAsync(installed, raiseModulesChanged: false);
                        log?.Report("Module update applied, but setup failed.");
                        return false;
                    }
                }

                if (wasEnabled && installed.Commands.Run.Count > 0)
                {
                    installed.Status.Enabled = true;
                    await _moduleInstaller.SaveConfigAsync(installed, raiseModulesChanged: false);
                    _ = Task.Run(() => _moduleRunner.ExecuteRunAsync(
                        installed,
                        log ?? NoOpProgress<string>.Instance,
                        CancellationToken.None));
                }

                installed.Status.LastUpdated = DateTime.UtcNow.ToString("o");
                await _moduleInstaller.SaveConfigAsync(installed, raiseModulesChanged: false);
                log?.Report($"{installed.Name} updated to {candidate.RemoteVersion}.");
                return true;
            }
            catch
            {
                log?.Report("Update failed. Module backup restoration is disabled.");
                throw;
            }
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
            newConfig.Update.Branch = candidate.ReferenceName ?? oldConfig.Update.Branch;
            newConfig.Update.AssetName = oldConfig.Update.AssetName ?? newConfig.Update.AssetName;
            newConfig.Update.RunFirstRunAfterUpdate = oldConfig.Update.RunFirstRunAfterUpdate;
            newConfig.Update.Preserve = oldConfig.Update.Preserve
                .Concat(newConfig.Update.Preserve)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            newConfig.Update.InstalledCommitSha = candidate.CommitSha ?? oldConfig.Update.InstalledCommitSha;
            newConfig.Update.InstalledReleaseTag = candidate.ReleaseTag ?? oldConfig.Update.InstalledReleaseTag;
            newConfig.Update.SelectedReleaseTag =
                string.IsNullOrWhiteSpace(oldConfig.Update.SelectedReleaseTag) ||
                IsLatestReleaseSelection(oldConfig.Update.SelectedReleaseTag)
                    ? ModuleUpdateConfig.LatestReleaseTag
                    : candidate.ReleaseTag ?? oldConfig.Update.SelectedReleaseTag;
            newConfig.Update.PendingUpdate = null;
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
        /// Returns whether the selected module update mode includes GitHub pre-releases.
        /// </summary>
        private static bool IsPrereleaseMode(string? mode)
        {
            return IsPrereleaseChannel(mode);
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

            return ResolveInstalledModuleReleaseTag(module);
        }

        /// <summary>
        /// Returns the locally installed release tag or version string for one module.
        /// </summary>
        private static string ResolveInstalledModuleReleaseTag(ModuleConfig module)
        {
            return module.Update.InstalledReleaseTag ?? module.Status.InstalledVersion ?? module.Version;
        }

        /// <summary>
        /// Returns whether the install candidate already matches the local installation (no file work needed).
        /// </summary>
        private static bool IsModuleAlreadyAtInstallTarget(ModuleConfig module, UpdateCandidate candidate)
        {
            if (string.Equals(module.Update.Mode, "branch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Mode, "branch", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(candidate.CommitSha) ||
                    string.IsNullOrWhiteSpace(module.Update.InstalledCommitSha))
                {
                    return false;
                }

                return string.Equals(
                    candidate.CommitSha.Trim(),
                    module.Update.InstalledCommitSha.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(candidate.ReleaseTag))
            {
                return false;
            }

            var installed = ResolveInstalledModuleReleaseTag(module);
            return AreEquivalentVersionReferences(candidate.ReleaseTag, installed);
        }

        /// <summary>
        /// Resolves the local ASLM version label shown in settings when no GitHub tag is known yet.
        /// </summary>
        private static string ResolveCurrentAppDisplayVersion()
        {
            var informationalVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                var buildMetadataIndex = informationalVersion.IndexOf('+');
                return buildMetadataIndex > 0
                    ? informationalVersion[..buildMetadataIndex]
                    : informationalVersion;
            }

            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        }

        /// <summary>
        /// Resolves the best local reference used to compare ASLM against GitHub release tags.
        /// </summary>
        private string ResolveCurrentAppReleaseReference()
        {
            return _appData.Data.Updates.InstalledReleaseTag ?? CurrentAppVersion;
        }

        /// <summary>
        /// Returns whether the current ASLM installation should treat the release tag as an available update.
        /// </summary>
        private bool ShouldOfferAppUpdate(string releaseTag, bool includePrerelease)
        {
            var currentReference = ResolveCurrentAppReleaseReference();
            if (AreEquivalentVersionReferences(releaseTag, currentReference))
            {
                return false;
            }

            // Once the app has a persisted GitHub tag, exact tag comparison becomes the source of truth.
            if (!string.IsNullOrWhiteSpace(_appData.Data.Updates.InstalledReleaseTag))
            {
                return true;
            }

            if (IsRemoteVersionNewer(releaseTag, currentReference))
            {
                return true;
            }

            // Fresh local builds often only carry the base display version plus build metadata.
            // In the pre-release channel we still want the latest GitHub tag to win when the tags differ.
            return includePrerelease && !AreEquivalentVersionReferences(releaseTag, CurrentAppVersion);
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
        /// Returns whether two local or remote version references point at the same GitHub tag identity.
        /// </summary>
        internal static bool AreEquivalentVersionReferences(string left, string right)
        {
            var leftNormalized = NormalizeVersionReference(left);
            var rightNormalized = NormalizeVersionReference(right);

            // Stable numeric versions should compare semantically so 1.0 and 1.0.0 are treated as the same release.
            if (!leftNormalized.Contains('-', StringComparison.Ordinal) &&
                !rightNormalized.Contains('-', StringComparison.Ordinal) &&
                Version.TryParse(leftNormalized, out var leftVersion) &&
                Version.TryParse(rightNormalized, out var rightVersion))
            {
                return leftVersion == rightVersion;
            }

            return string.Equals(leftNormalized, rightNormalized, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Normalizes a version or release tag while preserving pre-release identifiers and removing build metadata.
        /// </summary>
        private static string NormalizeVersionReference(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            {
                normalized = normalized[1..];
            }

            var buildMetadataIndex = normalized.IndexOf('+');
            return buildMetadataIndex > 0 ? normalized[..buildMetadataIndex] : normalized;
        }

        /// <summary>
        /// Returns whether the selected release marker points at the moving latest target.
        /// </summary>
        internal static bool IsLatestReleaseSelection(string? selectedReleaseTag)
        {
            return string.IsNullOrWhiteSpace(selectedReleaseTag) ||
                   string.Equals(
                       selectedReleaseTag.Trim(),
                       ModuleUpdateConfig.LatestReleaseTag,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a short display form of one commit SHA.
        /// </summary>
        private static string ShortSha(string sha)
        {
            return string.IsNullOrWhiteSpace(sha) ? string.Empty : sha[..Math.Min(7, sha.Length)];
        }

        /// <summary>
        /// Builds the compact picker label shown for one release candidate.
        /// </summary>
        private static string BuildReleaseDisplayName(GitHubReleaseInfo release)
        {
            var label = string.IsNullOrWhiteSpace(release.TagName)
                ? release.Name
                : release.TagName;

            if (release.Prerelease)
            {
                label += " pre-release";
            }

            if (release.PublishedAt.HasValue)
            {
                label += $" - {release.PublishedAt.Value:yyyy-MM-dd}";
            }

            return label;
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
        /// Builds the normalized set of paths that must stay in place during module replacement.
        /// </summary>
        private static HashSet<string> BuildPreservePathSet(IEnumerable<string> relativePaths)
        {
            var preservePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var relativePath in relativePaths)
            {
                if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                {
                    continue;
                }

                var normalized = NormalizeRelativePath(relativePath);
                if (normalized.Length == 0 ||
                    normalized == "." ||
                    normalized.Contains("..", StringComparison.Ordinal))
                {
                    continue;
                }

                preservePaths.Add(normalized);
            }

            return preservePaths;
        }

        /// <summary>
        /// Logs preserved paths without traversing their contents.
        /// </summary>
        private static void ReportPreservedPaths(
            string moduleRoot,
            IReadOnlySet<string> preservePaths,
            IProgress<string>? log)
        {
            foreach (var relativePath in preservePaths)
            {
                var fullPath = ResolveChildPath(moduleRoot, relativePath);
                if (File.Exists(fullPath))
                {
                    log?.Report($"Preserving file in place: {relativePath}");
                    continue;
                }

                if (Directory.Exists(fullPath))
                {
                    log?.Report($"Preserving directory in place: {relativePath}");
                }
            }
        }

        /// <summary>
        /// Recursively copies one directory into another while skipping preserved module paths.
        /// </summary>
        private static void CopyDirectory(
            string sourceDir,
            string destDir,
            IReadOnlySet<string> preservePaths,
            string relativeRoot = "")
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.EnumerateFiles(sourceDir))
            {
                var relativePath = CombineRelativePath(relativeRoot, Path.GetFileName(file));
                if (IsPreservedPath(relativePath, preservePaths))
                {
                    continue;
                }

                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var subdir in Directory.EnumerateDirectories(sourceDir))
            {
                var relativePath = CombineRelativePath(relativeRoot, Path.GetFileName(subdir));
                if (IsPreservedPath(relativePath, preservePaths))
                {
                    continue;
                }

                CopyDirectory(
                    subdir,
                    Path.Combine(destDir, Path.GetFileName(subdir)),
                    preservePaths,
                    relativePath);
            }
        }

        /// <summary>
        /// Removes every non-preserved file and directory inside one root directory.
        /// </summary>
        private static void ClearDirectory(string directory, IReadOnlySet<string> preservePaths)
        {
            Directory.CreateDirectory(directory);
            ClearDirectory(directory, directory, preservePaths);
        }

        /// <summary>
        /// Removes non-preserved contents from one directory while preserving declared descendants.
        /// </summary>
        private static void ClearDirectory(
            string currentDir,
            string rootDir,
            IReadOnlySet<string> preservePaths)
        {
            foreach (var file in Directory.EnumerateFiles(currentDir))
            {
                var relativePath = NormalizeRelativePath(Path.GetRelativePath(rootDir, file));
                if (IsPreservedPath(relativePath, preservePaths))
                {
                    continue;
                }

                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var subdir in Directory.EnumerateDirectories(currentDir))
            {
                var relativePath = NormalizeRelativePath(Path.GetRelativePath(rootDir, subdir));
                if (IsPreservedPath(relativePath, preservePaths))
                {
                    continue;
                }

                if (HasPreservedDescendant(relativePath, preservePaths))
                {
                    ClearDirectory(subdir, rootDir, preservePaths);
                    if (!Directory.EnumerateFileSystemEntries(subdir).Any())
                    {
                        Directory.Delete(subdir);
                    }

                    continue;
                }

                Directory.Delete(subdir, recursive: true);
            }
        }

        /// <summary>
        /// Clears a module directory with retries for Windows file locks from recently stopped processes.
        /// </summary>
        private static async Task ClearDirectoryForUpdateAsync(
            string directory,
            IReadOnlySet<string> preservePaths,
            IProgress<string>? log,
            CancellationToken ct)
        {
            const int maxAttempts = 6;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await Task.Run(() => ClearDirectory(directory, preservePaths), ct);
                    return;
                }
                catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < maxAttempts)
                {
                    log?.Report($"Module files are still in use. Retrying cleanup ({attempt}/{maxAttempts - 1})...");
                    await StopProcessesRunningFromDirectoryAsync(directory, log, ct);
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct);
                }
            }
        }

        /// <summary>
        /// Returns whether a failed delete may succeed after process cleanup or a short retry delay.
        /// </summary>
        private static bool IsTransientFileAccessException(Exception ex)
        {
            return ex is IOException or UnauthorizedAccessException;
        }

        /// <summary>
        /// Stops untracked or orphaned processes whose executable lives under one module directory.
        /// </summary>
        private static async Task<int> StopProcessesRunningFromDirectoryAsync(
            string directory,
            IProgress<string>? log,
            CancellationToken ct)
        {
            var moduleRoot = EnsureTrailingSeparator(Path.GetFullPath(directory));
            var currentProcessId = Environment.ProcessId;
            var stoppedCount = 0;

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    if (process.Id == currentProcessId || process.HasExited)
                    {
                        continue;
                    }

                    var executablePath = TryGetProcessExecutablePath(process);
                    if (!IsPathUnderDirectory(executablePath, moduleRoot))
                    {
                        continue;
                    }

                    log?.Report(
                        $"Stopping process using module files: {Path.GetFileName(executablePath)} (PID {process.Id}).");

                    process.Kill(entireProcessTree: true);
                    stoppedCount++;
                    await WaitForProcessExitAsync(process, TimeSpan.FromSeconds(5), ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Best effort only. The following delete retry will surface persistent blockers.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return stoppedCount;
        }

        /// <summary>
        /// Returns the full executable path for a process when the OS allows it.
        /// </summary>
        private static string? TryGetProcessExecutablePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns whether one path is inside a normalized directory root.
        /// </summary>
        private static bool IsPathUnderDirectory(string? path, string normalizedRoot)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Waits briefly after killing a process tree so Windows can release executable file handles.
        /// </summary>
        private static async Task WaitForProcessExitAsync(
            Process process,
            TimeSpan timeout,
            CancellationToken ct)
        {
            if (process.HasExited)
            {
                return;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Continue to the delete retry; it will report a real blocker if the process survived.
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
        /// Combines relative path segments for module path comparisons.
        /// </summary>
        private static string CombineRelativePath(string basePath, string childName)
        {
            return string.IsNullOrWhiteSpace(basePath)
                ? NormalizeRelativePath(childName)
                : NormalizeRelativePath($"{basePath}/{childName}");
        }

        /// <summary>
        /// Returns whether one relative module path is preserved or lives inside a preserved directory.
        /// </summary>
        private static bool IsPreservedPath(string relativePath, IReadOnlySet<string> preservePaths)
        {
            var normalized = NormalizeRelativePath(relativePath);
            return preservePaths.Contains(normalized) ||
                   preservePaths.Any(path =>
                       normalized.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns whether one relative directory contains a preserved descendant.
        /// </summary>
        private static bool HasPreservedDescendant(string relativePath, IReadOnlySet<string> preservePaths)
        {
            var normalized = NormalizeRelativePath(relativePath);
            var prefix = normalized.Length == 0 ? string.Empty : normalized + "/";
            return preservePaths.Any(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Normalizes relative paths for cross-platform module path comparisons.
        /// </summary>
        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath.Trim().Replace('\\', '/').Trim('/');
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

        /// <summary>
        /// Carries the validated extracted module payload into the apply phase.
        /// </summary>
        private sealed record PreparedModuleUpdate(
            string NewManifestPath,
            ModuleConfig? NewConfig,
            string ModuleSourceDir);
    }
}
