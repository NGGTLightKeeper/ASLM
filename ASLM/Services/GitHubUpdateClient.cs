// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    /// <summary>
    /// Provides the GitHub API calls needed by ASLM update checks.
    /// </summary>
    public sealed class GitHubUpdateClient
    {
        private static readonly TimeSpan ReleaseCacheLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan BranchCacheLifetime = TimeSpan.FromMinutes(5);

        private readonly HttpClient _httpClient = new();
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        private readonly SemaphoreSlim _cacheGate = new(1, 1);
        private readonly Dictionary<string, CacheEntry<List<GitHubReleaseInfo>>> _releaseCache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry<List<GitHubBranchInfo>>> _branchCache =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the GitHub client with the headers required by the REST API.
        /// </summary>
        public GitHubUpdateClient()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ASLM-Updater");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        /// <summary>
        /// Returns the most recent release allowed by the requested channel.
        /// </summary>
        public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(
            string repo,
            bool includePrerelease,
            CancellationToken ct = default)
        {
            var releases = await GetReleasesAsync(repo, includePrerelease, ct);
            return releases.FirstOrDefault();
        }

        /// <summary>
        /// Returns the release list allowed by the requested channel.
        /// </summary>
        public async Task<List<GitHubReleaseInfo>> GetReleasesAsync(
            string repo,
            bool includePrerelease,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(repo))
            {
                return [];
            }

            var releases = await GetOrFetchCachedValueAsync(
                cache: _releaseCache,
                cacheKey: repo.Trim(),
                lifetime: ReleaseCacheLifetime,
                fetch: async cancellationToken => await FetchReleasesAsync(repo, cancellationToken),
                ct);

            return releases
                .Where(release => !release.Draft)
                .Where(release => includePrerelease || !release.Prerelease)
                .OrderByDescending(release => release.PublishedAt ?? release.CreatedAt ?? DateTimeOffset.MinValue)
                .ToList();
        }

        /// <summary>
        /// Returns all repository branch names with their current commit SHA.
        /// </summary>
        public Task<List<GitHubBranchInfo>> GetBranchesAsync(string repo, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(repo))
            {
                return Task.FromResult(new List<GitHubBranchInfo>());
            }

            return GetOrFetchCachedValueAsync(
                cache: _branchCache,
                cacheKey: repo.Trim(),
                lifetime: BranchCacheLifetime,
                fetch: async cancellationToken => await FetchBranchesAsync(repo, cancellationToken),
                ct);
        }

        /// <summary>
        /// Downloads one URL to the requested file path with throttled progress.
        /// </summary>
        public async Task DownloadFileAsync(
            string url,
            string destinationPath,
            IProgress<string>? log = null,
            IProgress<DownloadProgress>? downloadProgress = null,
            CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            log?.Report($"Downloading {url}");

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

            var buffer = new byte[65536];
            long downloaded = 0;
            int bytesRead;
            var throttle = Stopwatch.StartNew();

            downloadProgress?.Report(new DownloadProgress(0, 0, totalBytes));

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloaded += bytesRead;

                if (totalBytes > 0 && throttle.ElapsedMilliseconds >= 75)
                {
                    throttle.Restart();
                    downloadProgress?.Report(new DownloadProgress((double)downloaded / totalBytes, downloaded, totalBytes));
                }
            }

            downloadProgress?.Report(new DownloadProgress(1.0, downloaded, totalBytes > 0 ? totalBytes : downloaded));
            log?.Report("Download complete.");
        }

        /// <summary>
        /// Downloads a repository ZIP archive for the requested ref.
        /// </summary>
        public Task DownloadRepositoryZipAsync(
            string repo,
            string reference,
            string destinationPath,
            IProgress<string>? log = null,
            IProgress<DownloadProgress>? downloadProgress = null,
            CancellationToken ct = default)
        {
            var url = BuildApiUrl(repo, $"zipball/{Uri.EscapeDataString(reference)}");
            return DownloadFileAsync(url, destinationPath, log, downloadProgress, ct);
        }


        // Cache helpers

        /// <summary>
        /// Returns a cached value when still fresh, otherwise fetches and stores a new one.
        /// </summary>
        private async Task<T> GetOrFetchCachedValueAsync<T>(
            Dictionary<string, CacheEntry<T>> cache,
            string cacheKey,
            TimeSpan lifetime,
            Func<CancellationToken, Task<T>> fetch,
            CancellationToken ct)
        {
            if (TryGetCachedValue(cache, cacheKey, out var cached))
            {
                return cached;
            }

            await _cacheGate.WaitAsync(ct);
            try
            {
                if (TryGetCachedValue(cache, cacheKey, out cached))
                {
                    return cached;
                }

                try
                {
                    var fetched = await fetch(ct);
                    cache[cacheKey] = new CacheEntry<T>(fetched, DateTimeOffset.UtcNow.Add(lifetime));
                    return fetched;
                }
                catch when (TryGetAnyCachedValue(cache, cacheKey, out var stale))
                {
                    // When GitHub throttles the client, stale metadata is still more useful than a hard failure.
                    return stale;
                }
            }
            finally
            {
                _cacheGate.Release();
            }
        }

        /// <summary>
        /// Returns whether a cached value is still valid.
        /// </summary>
        private static bool TryGetCachedValue<T>(
            Dictionary<string, CacheEntry<T>> cache,
            string cacheKey,
            out T value)
        {
            if (cache.TryGetValue(cacheKey, out var entry) &&
                entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                value = entry.Value;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// Returns any cached value even when it has expired.
        /// </summary>
        private static bool TryGetAnyCachedValue<T>(
            Dictionary<string, CacheEntry<T>> cache,
            string cacheKey,
            out T value)
        {
            if (cache.TryGetValue(cacheKey, out var entry))
            {
                value = entry.Value;
                return true;
            }

            value = default!;
            return false;
        }


        // API fetchers

        /// <summary>
        /// Loads the full release list from GitHub for the requested repository.
        /// </summary>
        private async Task<List<GitHubReleaseInfo>> FetchReleasesAsync(string repo, CancellationToken ct)
        {
            using var stream = await _httpClient.GetStreamAsync(BuildApiUrl(repo, "releases?per_page=100"), ct);
            return await JsonSerializer.DeserializeAsync<List<GitHubReleaseInfo>>(stream, _jsonOptions, ct) ?? [];
        }

        /// <summary>
        /// Loads every branch name and commit SHA from GitHub for the requested repository.
        /// </summary>
        private async Task<List<GitHubBranchInfo>> FetchBranchesAsync(string repo, CancellationToken ct)
        {
            var branches = new List<GitHubBranchInfo>();
            var page = 1;

            while (true)
            {
                using var stream = await _httpClient.GetStreamAsync(BuildApiUrl(repo, $"branches?per_page=100&page={page}"), ct);
                var payload = await JsonSerializer.DeserializeAsync<List<GitHubBranchPayload>>(stream, _jsonOptions, ct) ?? [];
                if (payload.Count == 0)
                {
                    break;
                }

                branches.AddRange(payload
                    .Where(branch => !string.IsNullOrWhiteSpace(branch.Name))
                    .Select(branch => new GitHubBranchInfo(branch.Name, branch.Commit?.Sha ?? string.Empty)));

                if (payload.Count < 100)
                {
                    break;
                }

                page++;
            }

            return branches
                .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Builds a GitHub REST API URL for the requested repository path.
        /// </summary>
        private static string BuildApiUrl(string repo, string path) =>
            $"https://api.github.com/repos/{repo.Trim().Trim('/')}/{path}";

        /// <summary>
        /// Stores one cached GitHub payload with its expiration timestamp.
        /// </summary>
        private sealed record CacheEntry<T>(T Value, DateTimeOffset ExpiresAt);
    }

    /// <summary>
    /// Minimal GitHub release payload used by update services.
    /// </summary>
    public sealed class GitHubReleaseInfo
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("zipball_url")]
        public string ZipballUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAssetInfo> Assets { get; set; } = [];
    }

    /// <summary>
    /// Minimal GitHub release asset payload used by update services.
    /// </summary>
    public sealed class GitHubReleaseAssetInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    internal sealed class GitHubBranchPayload
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("commit")]
        public GitHubBranchCommitPayload? Commit { get; set; }
    }

    internal sealed class GitHubBranchCommitPayload
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
    }
}
