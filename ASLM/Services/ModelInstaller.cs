using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    /// <summary>
    /// Discovers and installs Machine Learning models from external sources (e.g. HuggingFace).
    /// </summary>
    public class ModelInstaller
    {
        private readonly HttpClient _httpClient = new();
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // --- Discovery -------------------------------------------------------

        /// <summary>
        /// Synchronously scans <c>Models/*/ASLM_Model.json</c> files.
        /// </summary>
        public List<ModelConfig> DiscoverModels()
        {
            var baseDir = GetRootDirectory();
            var modelsRoot = Path.Combine(baseDir, "Models");
            var models = new List<ModelConfig>();

            if (!Directory.Exists(modelsRoot))
                return models;

            foreach (var jsonFile in Directory.EnumerateFiles(modelsRoot, "ASLM_Model.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(jsonFile);
                    var config = JsonSerializer.Deserialize<ModelConfig>(json, _jsonOptions);
                    if (config != null)
                    {
                        config.SourcePath = jsonFile;

                        // Validate installed status
                        if (config.Status.Installed)
                        {
                            var modelDir = Path.GetDirectoryName(jsonFile)!;
                            
                            // Naive check: are there any files beside the JSON?
                            // A stricter check would verify every file in config.Files exists.
                            var hasFiles = Directory.EnumerateFiles(modelDir).Count() > 1; 

                            if (!hasFiles)
                            {
                                Debug.WriteLine($"Files missing for model {config.Name}, resetting installed status.");
                                config.Status.Installed = false;
                                config.Status.InstalledVersion = null;
                                SaveConfig(config);
                            }
                        }

                        models.Add(config);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to parse {jsonFile}: {ex.Message}");
                }
            }

            return models;
        }

        // --- Installation ----------------------------------------------------

        /// <summary>
        /// Downloads model files from HuggingFace. If no files are listed in config,
        /// fetches the file list from the API first.
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

            // If no files specified, fetch the list from HuggingFace API
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
                    log.Report($"✗ Failed to fetch file list: {ex.Message}");
                    throw;
                }
            }

            int totalFiles = config.Files.Count;
            int currentFile = 0;

            foreach (var fileName in config.Files)
            {
                ct.ThrowIfCancellationRequested();
                currentFile++;

                var url = $"https://huggingface.co/{config.Source.RepoId}/resolve/main/{fileName}";
                var destPath = Path.Combine(modelDir, fileName);

                log.Report($"[{currentFile}/{totalFiles}] Downloading {fileName}...");
                
                await DownloadFileAsync(url, destPath, log, downloadProgress, ct);
            }

            // Update status
            config.Status.Installed = true;
            config.Status.InstalledVersion = config.Version;
            config.Status.LastChecked = DateTime.UtcNow.ToString("o");

            await SaveConfigAsync(config);
            log.Report($"=== {config.Name} installed successfully ===");
        }

        /// <summary>
        /// Queries the HuggingFace API definition for a model and extracts the list
        /// of files (siblings), excluding .git* and README.md.
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
                    if (sibling.TryGetProperty("rfilename", out var rfilename))
                    {
                        var name = rfilename.GetString();
                        if (!string.IsNullOrEmpty(name) && 
                            !name.StartsWith(".git") && 
                            !name.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                        {
                            files.Add(name);
                        }
                    }
                }
            }
            return files;
        }

        /// <summary>
        /// Downloads a single file with progress reporting and throttling.
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
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

                var buffer = new byte[65536];
                long downloaded = 0;
                int bytesRead;
                var throttle = Stopwatch.StartNew();

                // Reset progress for this file
                downloadProgress?.Report(new DownloadProgress(0, 0, totalBytes));

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    downloaded += bytesRead;

                    if (totalBytes > 0 && throttle.ElapsedMilliseconds >= 50)
                    {
                        throttle.Restart();
                        downloadProgress?.Report(new DownloadProgress(
                            (double)downloaded / totalBytes, downloaded, totalBytes));
                    }
                }

                downloadProgress?.Report(new DownloadProgress(1.0, downloaded, totalBytes > 0 ? totalBytes : downloaded));
                log.Report("  ✓ Download complete.");
            }
            catch (Exception ex)
            {
                log.Report($"  ✗ Download failed: {ex.Message}");
                throw;
            }
        }

        // --- Helpers ---------------------------------------------------------

        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }

        private void SaveConfig(ModelConfig config)
        {
            if (string.IsNullOrEmpty(config.SourcePath)) return;
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(config.SourcePath, json);
        }

        private async Task SaveConfigAsync(ModelConfig config)
        {
            if (string.IsNullOrEmpty(config.SourcePath)) return;
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(config.SourcePath, json);
        }
    }
}
