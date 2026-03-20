// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    // Model installer

    /// <summary>
    /// Discovers model manifests and downloads model files from external sources.
    /// </summary>
    public class ModelInstaller
    {
        private readonly HttpClient _httpClient = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Discovery

        /// <summary>
        /// Scans <c>Models/*/ASLM_Model.json</c> files asynchronously.
        /// </summary>
        public async Task<List<ModelConfig>> DiscoverModelsAsync(CancellationToken ct = default)
        {
            var baseDir = GetRootDirectory();
            var modelsRoot = Path.Combine(baseDir, "Models");
            var models = new System.Collections.Concurrent.ConcurrentBag<ModelConfig>();

            if (!Directory.Exists(modelsRoot))
            {
                return models.ToList();
            }

            var files = Directory.EnumerateFiles(modelsRoot, "ASLM_Model.json", SearchOption.AllDirectories);
            await Parallel.ForEachAsync(
                files,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = ct
                },
                async (jsonFile, token) =>
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(jsonFile, token);
                        var config = JsonSerializer.Deserialize<ModelConfig>(json, _jsonOptions);
                        if (config == null)
                        {
                            return;
                        }

                        config.Normalize();

                        // Treat manifests without an explicit version as the initial schema.
                        if (config.FileVersion == 0)
                        {
                            config.FileVersion = 1;
                        }

                        if (config.FileVersion != 1)
                        {
                            Debug.WriteLine($"Unsupported fileVersion {config.FileVersion} in {jsonFile}, skipping.");
                            return;
                        }

                        config.SourcePath = jsonFile;

                        // Reset the installed flag when the manifest exists but the payload files are gone.
                        if (config.Status.Installed)
                        {
                            var modelDir = Path.GetDirectoryName(jsonFile)!;
                            var hasFiles = Directory.EnumerateFiles(modelDir).Take(2).Count() > 1;

                            if (!hasFiles)
                            {
                                Debug.WriteLine($"Files missing for model {config.Name}, resetting installed status.");
                                config.Status.Installed = false;
                                config.Status.InstalledVersion = null;
                                await SaveConfigAsync(config);
                            }
                        }

                        models.Add(config);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to parse {jsonFile}: {ex.Message}");
                    }
                });

            return models
                .OrderBy(model => model.Name)
                .ToList();
        }


        // Installation

        /// <summary>
        /// Downloads all files required by one model configuration.
        /// </summary>
        public async Task InstallAsync(
            ModelConfig config,
            IProgress<string> log,
            IProgress<DownloadProgress>? downloadProgress = null,
            CancellationToken ct = default)
        {
            var modelDir = Path.GetDirectoryName(config.SourcePath)!;

            log.Report($"=== Installing Model: {config.Name} ===");
            log.Report($"Source: HuggingFace ({config.Source.RepoId})");
            log.Report($"Destination: {modelDir}");

            // Fetch the remote file list when the manifest does not pin one locally.
            if (config.Files == null || config.Files.Count == 0)
            {
                log.Report("No files specified in config. Fetching file list from HuggingFace...");

                try
                {
                    config.Files = await FetchFileListAsync(config.Source.RepoId, ct);
                    log.Report($"Found {config.Files.Count} files to download.");
                }
                catch (Exception ex)
                {
                    log.Report($"Failed to fetch file list: {ex.Message}");
                    throw;
                }
            }

            var totalFiles = config.Files.Count;
            var currentFile = 0;

            foreach (var fileName in config.Files)
            {
                ct.ThrowIfCancellationRequested();
                currentFile++;

                var url = $"https://huggingface.co/{config.Source.RepoId}/resolve/main/{fileName}";
                var destPath = Path.Combine(modelDir, fileName);

                log.Report($"[{currentFile}/{totalFiles}] Downloading {fileName}...");
                await DownloadFileAsync(url, destPath, log, downloadProgress, ct);
            }

            // Persist the installed state only after every file completes successfully.
            config.Status.Installed = true;
            config.Status.InstalledVersion = config.Version;
            config.Status.LastChecked = DateTime.UtcNow.ToString("o");

            await SaveConfigAsync(config);
            log.Report($"=== {config.Name} installed successfully ===");
        }

        // Remote file list

        /// <summary>
        /// Loads the downloadable file list for a HuggingFace repository.
        /// </summary>
        private async Task<List<string>> FetchFileListAsync(string repoId, CancellationToken ct)
        {
            var url = $"https://huggingface.co/api/models/{repoId}";

            using var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var files = new List<string>();
            if (doc.RootElement.TryGetProperty("siblings", out var siblings))
            {
                foreach (var sibling in siblings.EnumerateArray())
                {
                    if (!sibling.TryGetProperty("rfilename", out var rfilename))
                    {
                        continue;
                    }

                    var name = rfilename.GetString();
                    if (!string.IsNullOrEmpty(name) &&
                        !name.StartsWith(".git") &&
                        !name.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(name);
                    }
                }
            }

            return files;
        }

        // File download

        /// <summary>
        /// Downloads one model file and reports throttled progress updates.
        /// </summary>
        private async Task DownloadFileAsync(
            string url,
            string destPath,
            IProgress<string> log,
            IProgress<DownloadProgress>? downloadProgress,
            CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var directoryPath = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

                var buffer = new byte[65536];
                long downloaded = 0;
                int bytesRead;
                var throttle = Stopwatch.StartNew();

                // Reset the per-file progress bar before the first bytes arrive.
                downloadProgress?.Report(new DownloadProgress(0, 0, totalBytes));

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    downloaded += bytesRead;

                    if (totalBytes > 0 && throttle.ElapsedMilliseconds >= 50)
                    {
                        throttle.Restart();
                        downloadProgress?.Report(new DownloadProgress(
                            (double)downloaded / totalBytes,
                            downloaded,
                            totalBytes));
                    }
                }

                downloadProgress?.Report(new DownloadProgress(
                    1.0,
                    downloaded,
                    totalBytes > 0 ? totalBytes : downloaded));
                log.Report("  Download complete.");
            }
            catch (Exception ex)
            {
                log.Report($"  Download failed: {ex.Message}");
                throw;
            }
        }


        // Persistence helpers

        /// <summary>
        /// Returns the application root directory.
        /// </summary>
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }

        // Config save

        /// <summary>
        /// Saves one model manifest back to disk.
        /// </summary>
        private async Task SaveConfigAsync(ModelConfig config)
        {
            if (string.IsNullOrEmpty(config.SourcePath))
            {
                return;
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(config.SourcePath, json);
        }
    }
}
