using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    /// <summary>
    /// Manages the discovery and installation of Modules from GitHub ZIP archives.
    /// </summary>
    public class ModuleInstaller
    {
        private readonly HttpClient _httpClient = new();
        private readonly ModuleRunner _moduleRunner;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleInstaller"/> class.
        /// </summary>
        /// <param name="moduleRunner">The module runner service used for first-run execution.</param>
        public ModuleInstaller(ModuleRunner moduleRunner)
        {
            _moduleRunner = moduleRunner;
        }

        // --- Discovery -------------------------------------------------------

        /// <summary>
        /// Scans <c>Modules/*/ASLM_Module.json</c> to find installed modules asynchronously.
        /// </summary>
        /// <returns>A list of discovered module configurations.</returns>
        public async Task<List<ModuleConfig>> DiscoverModulesAsync()
        {
            var baseDir = GetRootDirectory();
            var modulesRoot = Path.Combine(baseDir, "Modules");
            var modules = new List<ModuleConfig>();

            if (!Directory.Exists(modulesRoot))
                return modules;

            // Get all JSON files first (synchronously, usually fast)
            var jsonFiles = Directory.GetFiles(modulesRoot, "ASLM_Module.json", SearchOption.AllDirectories);

            // Process files in parallel
            var tasks = jsonFiles.Select(LoadModuleConfig);

            var results = await Task.WhenAll(tasks);

            // Filter out failures
            foreach (var result in results)
            {
                if (result != null)
                    modules.Add(result);
            }

            return modules;
        }

        /// <summary>
        /// Loads a single module configuration from the specified JSON file path.
        /// </summary>
        /// <param name="jsonFile">Path to ASLM_Module.json</param>
        /// <returns>The deserialized ModuleConfig or null if failed.</returns>
        public async Task<ModuleConfig?> LoadModuleConfig(string jsonFile)
        {
            if (!File.Exists(jsonFile)) return null;

            try
            {
                await using var stream = File.OpenRead(jsonFile);
                var config = await JsonSerializer.DeserializeAsync<ModuleConfig>(stream, _jsonOptions);
                if (config != null)
                {
                    // Backward compatibility: files without fileVersion are treated as v1
                    if (config.FileVersion == 0)
                        config.FileVersion = 1;

                    if (config.FileVersion != 1)
                    {
                        Debug.WriteLine($"Unsupported fileVersion {config.FileVersion} in {jsonFile}, skipping.");
                        return null;
                    }

                    config.SourcePath = jsonFile;
                    
                    // If the JSON exists, we assume it's installed.
                    config.Status.Installed = true;
                    
                    return config;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse {jsonFile}: {ex.Message}");
            }
            return null;
        }

        // --- Source Download --------------------------------------------------

        /// <summary>
        /// Downloads module source code from <see cref="ModuleSource"/> (e.g. GitHub)
        /// into the module's directory.
        /// </summary>
        /// <param name="module">The module configuration.</param>
        /// <param name="log">Progress logger.</param>
        /// <param name="downloadProgress">Download progress reporter.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if source was downloaded successfully (or skipped appropriately).</returns>
        public async Task<bool> DownloadSourceAsync(
            ModuleConfig module,
            IProgress<string> log,
            IProgress<DownloadProgress>? downloadProgress = null,
            CancellationToken ct = default)
        {
            if (module.Source.Type != "github" || string.IsNullOrEmpty(module.Source.Repo))
            {
                log.Report("No GitHub source defined, skipping download.");
                return true;
            }

            var moduleDir = Path.GetDirectoryName(module.SourcePath);
            if (string.IsNullOrEmpty(moduleDir)) return false;

            // Note: This assumes the default branch is 'main'.
            var zipUrl = $"https://github.com/{module.Source.Repo}/archive/refs/heads/main.zip";
            var tempZip = Path.GetTempFileName();
            var tempExtractDir = Path.Combine(Path.GetTempPath(), "ASLM_ModuleSrc_" + Guid.NewGuid());

            try
            {
                log.Report($"Downloading source from: {module.Source.Repo}");
                await DownloadFileAsync(zipUrl, tempZip, log, downloadProgress, ct);

                await Task.Run(() =>
                {
                    // Extract to temp
                    Directory.CreateDirectory(tempExtractDir);
                    ZipFile.ExtractToDirectory(tempZip, tempExtractDir);

                    // GitHub archives have a top-level folder like "RepoName-main/"
                    var innerDir = Directory.GetDirectories(tempExtractDir).FirstOrDefault();
                    var sourceDir = innerDir ?? tempExtractDir;

                    // Copy extracted files into the module directory (merge, not replace)
                    CopyDirectory(sourceDir, moduleDir);
                }, ct);

                log.Report("✓ Source downloaded.");
                return true;
            }
            catch (Exception ex)
            {
                log.Report($"✗ Source download failed: {ex.Message}");
                return false;
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
                if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
            }
        }

        // --- Installation ----------------------------------------------------

        /// <summary>
        /// Downloads a ZIP from a URL (e.g. GitHub archive), extracts it to <c>Modules/{ModuleName}</c>,
        /// and discovers the contained <c>ASLM_Module.json</c>.
        /// </summary>
        /// <param name="zipUrl">Direct link to a ZIP file.</param>
        /// <param name="log">Progress logger.</param>
        /// <param name="downloadProgress">Download progress reporter.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The installed module configuration.</returns>
        public async Task<ModuleConfig> InstallFromUrlAsync(
            string zipUrl,
            IProgress<string> log,
            IProgress<DownloadProgress>? downloadProgress = null,
            CancellationToken ct = default)
        {
            var baseDir = GetRootDirectory();
            var modulesRoot = Path.Combine(baseDir, "Modules");
            var tempZip = Path.GetTempFileName();
            var tempExtractDir = Path.Combine(Path.GetTempPath(), "ASLM_Module_Install_" + Guid.NewGuid());
            
            try
            {
                log.Report($"Downloading module from: {zipUrl}");

                // 1. Download ZIP
                await DownloadFileAsync(zipUrl, tempZip, log, downloadProgress, ct);

                // 2. Extract to temp folder to inspect contents
                Directory.CreateDirectory(tempExtractDir);
                
                try 
                {
                    log.Report("Extracting archive...");

                    var jsonFile = await Task.Run(() =>
                    {
                        ZipFile.ExtractToDirectory(tempZip, tempExtractDir);
                        return Directory.EnumerateFiles(tempExtractDir, "ASLM_Module.json", SearchOption.AllDirectories).FirstOrDefault();
                    }, ct);

                    // 3. Find ASLM_Module.json
                    if (jsonFile == null)
                    {
                        throw new InvalidOperationException("Invalid module: ASLM_Module.json not found in archive.");
                    }

                    // 4. Parse Config to get ID/Name
                    var json = await File.ReadAllTextAsync(jsonFile, ct);
                    var config = JsonSerializer.Deserialize<ModuleConfig>(json, _jsonOptions);
                    
                    if (config == null || string.IsNullOrWhiteSpace(config.Id))
                        throw new InvalidOperationException("Invalid module: Could not parse config or ID is missing.");

                    // Backward compatibility: files without fileVersion are treated as v1
                    if (config.FileVersion == 0)
                        config.FileVersion = 1;

                    if (config.FileVersion != 1)
                        throw new InvalidOperationException($"Unsupported fileVersion {config.FileVersion}. This version of ASLM does not support this module format.");

                    // 5. Move to final destination: Modules/{ModuleId}
                    var moduleSourceDir = Path.GetDirectoryName(jsonFile)!;
                    var finalDir = Path.Combine(modulesRoot, config.Id);

                    log.Report($"Installing to: {finalDir}");

                    await Task.Run(() =>
                    {
                        if (Directory.Exists(finalDir))
                        {
                            log.Report("Removing old version...");
                            Directory.Delete(finalDir, true);
                        }
                        Directory.CreateDirectory(finalDir);

                        // Copy all files from the folder containing the JSON to the final dir
                        CopyDirectory(moduleSourceDir, finalDir);
                    }, ct);
                    
                    config.SourcePath = Path.Combine(finalDir, "ASLM_Module.json");
                    config.Status.Installed = true;
                    config.Status.InstalledVersion = config.Version;
                    config.Status.LastUpdated = DateTime.UtcNow.ToString("o");
                    
                    await SaveConfigAsync(config);
                    
                    // 6. Execute First Run logic
                    var success = await _moduleRunner.ExecuteFirstRunAsync(config, log, ct);
                    if (success)
                    {
                        await SaveConfigAsync(config); // Save updated status
                        log.Report($"Module '{config.Name}' installed successfully!");
                    }
                    else
                    {
                        log.Report($"Module '{config.Name}' installed, but setup failed.");
                    }

                    return config;
                }
                finally
                {
                    if (Directory.Exists(tempExtractDir))
                        Directory.Delete(tempExtractDir, true);
                }
            }
            finally
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
        }

        /// <summary>
        /// Recursively copies a directory to a destination.
        /// </summary>
        /// <param name="sourceDir">Source directory path.</param>
        /// <param name="destDir">Destination directory path.</param>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (var subdir in Directory.GetDirectories(sourceDir))
            {
                var destSubdir = Path.Combine(destDir, Path.GetFileName(subdir));
                CopyDirectory(subdir, destSubdir);
            }
        }

        // --- Download --------------------------------------------------------

        /// <summary>
        /// Helper to download a file with progress tracking.
        /// </summary>
        private async Task DownloadFileAsync(
            string url,
            string destinationPath,
            IProgress<string> log,
            IProgress<DownloadProgress>? downloadProgress,
            CancellationToken ct)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            log.Report(totalBytes > 0
                ? $"  Downloading: {totalBytes / 1024.0 / 1024.0:F1} MB..."
                : "  Downloading (size unknown)...");

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

                if (totalBytes > 0 && throttle.ElapsedMilliseconds >= 50)
                {
                    throttle.Restart();
                    downloadProgress?.Report(new DownloadProgress(
                        (double)downloaded / totalBytes, downloaded, totalBytes));
                }
            }

            downloadProgress?.Report(new DownloadProgress(
                1.0, downloaded, totalBytes > 0 ? totalBytes : downloaded));

            log.Report("  Download complete.");
        }

        /// <summary>
        /// Returns the application root directory.
        /// </summary>
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }

        /// <summary>
        /// Saves the module configuration synchronously.
        /// </summary>
        public void SaveModuleConfig(ModuleConfig config)
        {
            if (string.IsNullOrEmpty(config.SourcePath)) return;
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(config.SourcePath, json);
        }

        /// <summary>
        /// Saves the module configuration asynchronously.
        /// </summary>
        public async Task SaveConfigAsync(ModuleConfig config)
        {
            if (string.IsNullOrEmpty(config.SourcePath)) return;
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(config.SourcePath, json);
        }
    }
}
