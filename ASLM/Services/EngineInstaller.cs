using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;

namespace ASLM.Services
{
    /// <summary>
    /// Reports download progress to drive a UI progress bar.
    /// </summary>
    /// <param name="Fraction">Download completion from 0.0 to 1.0.</param>
    /// <param name="DownloadedBytes">Total bytes downloaded so far.</param>
    /// <param name="TotalBytes">Expected total file size in bytes.</param>
    public record DownloadProgress(double Fraction, long DownloadedBytes, long TotalBytes);

    /// <summary>
    /// Discovers, validates and installs external engine runtimes (Python, Node.js, etc.)
    /// based on declarative <c>ASLM_Engine.json</c> configuration files.
    /// Registered as a singleton — all mutable state is scoped to individual method calls.
    /// </summary>
    public class EngineInstaller
    {
        private readonly HttpClient _httpClient = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // --- Discovery -------------------------------------------------------

        /// <summary>
        /// Synchronously scans <c>Engines/*/ASLM_Engine.json</c> files and returns their configs.
        /// This method is intentionally synchronous because it is called from
        /// <see cref="App.CreateWindow"/> which runs on the UI thread and cannot safely await.
        /// </summary>
        /// <returns>A list of discovered engine configurations.</returns>
        public List<EngineConfig> DiscoverEngines()
        {
            var baseDir = GetRootDirectory();
            var enginesRoot = Path.Combine(baseDir, "Engines");
            var engines = new List<EngineConfig>();

            if (!Directory.Exists(enginesRoot))
                return engines;

            foreach (var jsonFile in Directory.EnumerateFiles(enginesRoot, "ASLM_Engine.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(jsonFile);
                    var config = JsonSerializer.Deserialize<EngineConfig>(json, _jsonOptions);
                    if (config != null)
                    {
                        config.SourcePath = jsonFile;

                        // Validate that "installed" engines actually have their runtime on disk.
                        // If a user manually deleted the runtime folder, reset the status.
                        if (config.Status.Installed)
                        {
                            var engineDir = Path.GetDirectoryName(jsonFile)!;
                            var runtimeDir = Path.Combine(engineDir, "runtime");

                            if (!Directory.Exists(runtimeDir) ||
                                !Directory.EnumerateFileSystemEntries(runtimeDir).Any())
                            {
                                Debug.WriteLine($"Runtime missing for {config.Name}, resetting installed status.");
                                config.Status.Installed = false;
                                config.Status.InstalledVersion = null;

                                // Persist the reset status back to JSON.
                                var updatedJson = JsonSerializer.Serialize(config, _jsonOptions);
                                File.WriteAllText(jsonFile, updatedJson);
                            }
                        }

                        engines.Add(config);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to parse {jsonFile}: {ex.Message}");
                }
            }

            return engines;
        }

        // --- Helpers ---------------------------------------------------------

        /// <summary>
        /// Resolves the absolute path to the engine's executable.
        /// Returns null if engine not found or not installed.
        /// </summary>
        /// <param name="engineId">The unique ID of the engine.</param>
        /// <returns>The full path to the executable, or null.</returns>
        public string? GetEngineExecutablePath(string engineId)
        {
            var engine = GetEngineConfig(engineId);

            if (engine == null || string.IsNullOrEmpty(engine.ExecutablePath))
                return null;

            var engineDir = Path.GetDirectoryName(engine.SourcePath);
            if (string.IsNullOrEmpty(engineDir)) return null;

            var fullPath = Path.Combine(engineDir, engine.ExecutablePath);
            return File.Exists(fullPath) ? fullPath : null;
        }

        /// <summary>
        /// Returns the full <see cref="EngineConfig"/> for the given engine ID,
        /// or null if not found / not installed.
        /// </summary>
        /// <param name="engineId">The unique ID of the engine.</param>
        /// <returns>The engine configuration, or null.</returns>
        public EngineConfig? GetEngineConfig(string engineId)
        {
            var engines = DiscoverEngines();
            return engines.FirstOrDefault(e => e.Id == engineId && e.Status.Installed);
        }

        // --- Installation ----------------------------------------------------

        /// <summary>
        /// Executes all installation steps declared in the engine config.
        /// </summary>
        /// <param name="config">Engine configuration with install steps.</param>
        /// <param name="log">Receives human-readable log messages for the UI console.</param>
        /// <param name="downloadProgress">Receives download fraction updates for the progress bar.</param>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        /// <returns>A task representing the installation process.</returns>
        public async Task InstallAsync(
            EngineConfig config,
            IProgress<string> log,
            IProgress<DownloadProgress>? downloadProgress = null,
            CancellationToken ct = default)
        {
            await Task.Run(async () =>
            {
                // Scoped state — safe for the singleton because only one install runs at a time.
                var baseDir = GetRootDirectory();
                var tempDir = Path.Combine(Path.GetTempPath(), "ASLM", config.Id);

                Directory.CreateDirectory(tempDir);

                log.Report($"=== Installing {config.Name} v{config.Version} ===");
                log.Report($"Base directory: {baseDir}");

                var context = new StepContext(baseDir, tempDir);

                for (int i = 0; i < config.Install.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var step = config.Install[i];
                    log.Report($"[{i + 1}/{config.Install.Count}] Action: {step.Action}");

                    switch (step.Action.ToLowerInvariant())
                    {
                        case "download":
                            await ExecuteDownloadAsync(step, context, log, downloadProgress, ct);
                            break;
                        case "extract":
                            ExecuteExtract(step, context, log);
                            break;
                        case "modify_file":
                            ExecuteModifyFile(step, context, log);
                            break;
                        case "execute":
                            await ExecuteCommandAsync(step, context, log, ct);
                            break;
                        case "move":
                            ExecuteMove(step, context, log);
                            break;
                        case "cleanup":
                            ExecuteCleanup(step, context, log);
                            break;
                        case "rename_file":
                            ExecuteRenameFile(step, context, log);
                            break;
                        case "delete_file":
                            ExecuteDeleteFile(step, context, log);
                            break;
                        default:
                            log.Report($"  ⚠ Unknown action: {step.Action}, skipping.");
                            break;
                    }
                }

                // Execute post-install steps (engine-specific fixes)
                if (config.PostInstall.Count > 0)
                {
                    log.Report($"Running {config.PostInstall.Count} post-install step(s)...");
                    for (int i = 0; i < config.PostInstall.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var step = config.PostInstall[i];
                        var label = step.Name ?? step.Action;
                        log.Report($"[PostInstall {i + 1}/{config.PostInstall.Count}] {label}");

                        switch (step.Action.ToLowerInvariant())
                        {
                            case "rename_file":
                                ExecuteRenameFile(step, context, log);
                                break;
                            case "delete_file":
                                ExecuteDeleteFile(step, context, log);
                                break;
                            case "modify_file":
                                ExecuteModifyFile(step, context, log);
                                break;
                            case "execute":
                                await ExecuteCommandAsync(step, context, log, ct);
                                break;
                            default:
                                log.Report($"  ⚠ Unknown post-install action: {step.Action}, skipping.");
                                break;
                        }
                    }
                }

                // Mark engine as installed and persist to disk.
                config.Status.Installed = true;
                config.Status.InstalledVersion = config.Version;
                config.Status.LastChecked = DateTime.UtcNow.ToString("o");

                await SaveConfigAsync(config);
                log.Report($"=== {config.Name} installed successfully ===");
            }, ct);
        }

        // --- Step Executors --------------------------------------------------

        /// <summary>
        /// Downloads a file from <c>step.Url</c> to <c>step.Dest</c>,
        /// optionally verifying the SHA-256 checksum.
        /// </summary>
        /// <param name="step">The installation step configuration.</param>
        /// <param name="ctx">Context for resolving paths.</param>
        /// <param name="log">Progress logger.</param>
        /// <param name="downloadProgress">Download progress reporter.</param>
        /// <param name="ct">Cancellation token.</param>
        private async Task ExecuteDownloadAsync(
            InstallStep step,
            StepContext ctx,
            IProgress<string> log,
            IProgress<DownloadProgress>? downloadProgress,
            CancellationToken ct)
        {
            var url = step.Url ?? throw new InvalidOperationException("Download step missing 'url'.");
            var dest = ctx.ResolvePath(step.Dest ?? throw new InvalidOperationException("Download step missing 'dest'."));

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            log.Report($"  Downloading: {url}");

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

            var buffer = new byte[65536];
            long downloaded = 0;
            int bytesRead;
            var throttle = Stopwatch.StartNew();

            downloadProgress?.Report(new DownloadProgress(0, 0, totalBytes));

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloaded += bytesRead;

                // Throttle UI updates to ~20 fps (every 50 ms).
                if (totalBytes > 0 && throttle.ElapsedMilliseconds >= 50)
                {
                    throttle.Restart();
                    downloadProgress?.Report(new DownloadProgress(
                        (double)downloaded / totalBytes, downloaded, totalBytes));
                }
            }

            // Report final 100%.
            downloadProgress?.Report(new DownloadProgress(
                1.0, downloaded, totalBytes > 0 ? totalBytes : downloaded));

            // Verify SHA-256 if a hash was provided.
            if (!string.IsNullOrWhiteSpace(step.Sha256))
            {
                log.Report("  Verifying checksum...");
                var hash = await ComputeSha256Async(dest, ct);
                if (!string.Equals(hash, step.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Checksum mismatch. Expected: {step.Sha256}, Got: {hash}");

                log.Report("  ✓ Checksum verified.");
            }

            log.Report("  ✓ Download complete.");
        }

        /// <summary>Extracts a zip archive from <c>step.Source</c> to <c>step.Dest</c>.</summary>
        /// <param name="step">The installation step configuration.</param>
        /// <param name="ctx">Context for resolving paths.</param>
        /// <param name="log">Progress logger.</param>
        private static void ExecuteExtract(InstallStep step, StepContext ctx, IProgress<string> log)
        {
            var source = ctx.ResolvePath(step.Source ?? throw new InvalidOperationException("Extract step missing 'source'."));
            var dest = ctx.ResolvePath(step.Dest ?? throw new InvalidOperationException("Extract step missing 'dest'."));

            log.Report($"  Extracting: {source}");
            log.Report($"  To: {dest}");

            Directory.CreateDirectory(dest);

            using var archive = ZipFile.OpenRead(source);
            foreach (var entry in archive.Entries)
            {
                var targetPath = Path.GetFullPath(Path.Combine(dest, entry.FullName));

                // Directory entry
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                // Ensure parent directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                try
                {
                    entry.ExtractToFile(targetPath, overwrite: true);
                }
                catch (IOException)
                {
                    // File is locked (e.g. by a running process) — skip it
                    log.Report($"  ⚠ Skipped (locked): {entry.FullName}");
                }
            }

            log.Report("  ✓ Extraction complete.");
        }

        /// <summary>Performs a find-and-replace inside <c>step.Path</c>.</summary>
        /// <param name="step">The installation step configuration.</param>
        /// <param name="ctx">Context for resolving paths.</param>
        /// <param name="log">Progress logger.</param>
        private static void ExecuteModifyFile(InstallStep step, StepContext ctx, IProgress<string> log)
        {
            var path = ctx.ResolvePath(step.Path ?? throw new InvalidOperationException("ModifyFile step missing 'path'."));
            var find = step.Find ?? throw new InvalidOperationException("ModifyFile step missing 'find'.");
            var replace = step.Replace ?? throw new InvalidOperationException("ModifyFile step missing 'replace'.");

            log.Report($"  Modifying: {path}");
            log.Report($"  Find: \"{find}\" → Replace: \"{replace}\"");

            var content = File.ReadAllText(path);

            if (!content.Contains(find))
            {
                log.Report("  ⚠ Pattern not found, skipping modification.");
                return;
            }

            content = content.Replace(find, replace);
            File.WriteAllText(path, content);

            log.Report("  ✓ File modified.");
        }

        /// <summary>
        /// Runs an external process. The first token of <c>step.Command</c> is resolved
        /// as the executable path; remaining tokens are arguments. Paths containing
        /// slashes are automatically resolved to absolute paths.
        /// </summary>
        /// <param name="step">The installation step configuration.</param>
        /// <param name="ctx">Context for resolving paths.</param>
        /// <param name="log">Progress logger.</param>
        /// <param name="ct">Cancellation token.</param>
        private static async Task ExecuteCommandAsync(InstallStep step, StepContext ctx, IProgress<string> log, CancellationToken ct)
        {
            var command = step.Command ?? throw new InvalidOperationException("Execute step missing 'command'.");
            command = ctx.ResolveVariables(command);

            // Split into executable + argument string.
            var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var exePath = ctx.ResolvePath(parts[0]);
            var arguments = parts.Length > 1 ? ctx.ResolveArgPaths(parts[1]) : "";

            log.Report($"  Executing: {exePath} {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = ctx.BaseDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            // Ensure Python writes UTF-8 regardless of Windows locale.
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
            psi.Environment["PYTHONUTF8"] = "1";

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Stream stdout / stderr in parallel.
            var stdoutTask = Task.Run(async () =>
            {
                while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
                    log.Report($"    > {line}");
            }, ct);

            var stderrTask = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync(ct) is { } line)
                    log.Report($"    ERR> {line}");
            }, ct);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
                log.Report($"  ⚠ Process exited with code {process.ExitCode}");
            else
                log.Report("  ✓ Command complete.");
        }

        /// <summary>Moves (renames) a directory from <c>step.Source</c> to <c>step.Dest</c>.</summary>
        /// <param name="step">The installation step configuration.</param>
        /// <param name="ctx">Context for resolving paths.</param>
        /// <param name="log">Progress logger.</param>
        private static void ExecuteMove(InstallStep step, StepContext ctx, IProgress<string> log)
        {
            var source = ctx.ResolvePath(step.Source ?? throw new InvalidOperationException("Move step missing 'source'."));
            var dest = ctx.ResolvePath(step.Dest ?? throw new InvalidOperationException("Move step missing 'dest'."));

            log.Report($"  Moving: {source}");
            log.Report($"  To: {dest}");

            if (Directory.Exists(dest))
                Directory.Delete(dest, true);

            Directory.Move(source, dest);

            log.Report("  ✓ Move complete.");
        }

        /// <summary>Recursively deletes <c>step.Target</c> directory (defaults to temp dir).</summary>
        /// <param name="step">The installation step configuration.</param>
        /// <param name="ctx">Context for resolving paths.</param>
        /// <param name="log">Progress logger.</param>
        private static void ExecuteCleanup(InstallStep step, StepContext ctx, IProgress<string> log)
        {
            var target = ctx.ResolvePath(step.Target ?? ctx.TempDir);

            log.Report($"  Cleaning up: {target}");

            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
                log.Report("  ✓ Cleanup complete.");
            }
            else
            {
                log.Report("  Directory not found, nothing to clean.");
            }
        }

        /// <summary>Renames a file from <c>step.Source</c> to <c>step.Dest</c>.</summary>
        /// <param name="step">The installation step configuration.</param>
        /// <param name="ctx">Context for resolving paths.</param>
        /// <param name="log">Progress logger.</param>
        private static void ExecuteRenameFile(InstallStep step, StepContext ctx, IProgress<string> log)
        {
            var source = ctx.ResolvePath(step.Source ?? throw new InvalidOperationException("rename_file step missing 'source'."));
            var dest = ctx.ResolvePath(step.Dest ?? throw new InvalidOperationException("rename_file step missing 'dest'."));

            if (!File.Exists(source))
            {
                log.Report($"  File not found: {source}, skipping.");
                return;
            }

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(source, dest);
            log.Report($"  ✓ Renamed: {Path.GetFileName(source)} → {Path.GetFileName(dest)}");
        }

        /// <summary>Deletes a file at <c>step.Target</c>.</summary>
        /// <param name="step">The installation step configuration.</param>
        /// <param name="ctx">Context for resolving paths.</param>
        /// <param name="log">Progress logger.</param>
        private static void ExecuteDeleteFile(InstallStep step, StepContext ctx, IProgress<string> log)
        {
            var target = ctx.ResolvePath(step.Target ?? throw new InvalidOperationException("delete_file step missing 'target'."));

            if (!File.Exists(target))
            {
                log.Report($"  File not found: {target}, skipping.");
                return;
            }

            File.Delete(target);
            log.Report($"  ✓ Deleted: {Path.GetFileName(target)}");
        }

        // --- Helpers ---------------------------------------------------------

        /// <summary>
        /// Returns the application root directory (parent of the <c>App/</c> folder).
        /// The MAUI executable runs from <c>ASLM/App/</c>, but data directories
        /// (<c>Engines/</c>, <c>Modules/</c>, etc.) live one level up at <c>ASLM/</c>.
        /// </summary>
        /// <returns>The root directory path.</returns>
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }

        /// <summary>Computes a lowercase hex SHA-256 hash for the given file.</summary>
        /// <param name="filePath">Path to the file to hash.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Lowercase hex string of the SHA256 hash.</returns>
        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            await using var stream = File.OpenRead(filePath);
            var hashBytes = await SHA256.HashDataAsync(stream, ct);
            return Convert.ToHexStringLower(hashBytes);
        }

        /// <summary>Serializes the engine config back to its source JSON file.</summary>
        /// <param name="config">The config to save.</param>
        private async Task SaveConfigAsync(EngineConfig config)
        {
            if (string.IsNullOrEmpty(config.SourcePath))
                return;

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(config.SourcePath, json);
        }

        // --- StepContext (encapsulates per-install mutable state) -------------

        /// <summary>
        /// Holds directory paths for a single installation run.
        /// Avoids storing mutable state as fields on the singleton service.
        /// </summary>
        /// <param name="baseDir">The application base directory.</param>
        /// <param name="tempDir">The temporary directory for this installation.</param>
        private sealed class StepContext(string baseDir, string tempDir)
        {
            public string BaseDir { get; } = baseDir;
            public string TempDir { get; } = tempDir;

            /// <summary>Replaces <c>{temp}</c> placeholder with the actual temp directory.</summary>
            public string ResolveVariables(string input)
                => input.Replace("{temp}", TempDir);

            /// <summary>
            /// Resolves <c>{temp}</c> variable and converts relative paths to absolute
            /// (relative to <see cref="BaseDir"/>). Normalises path separators.
            /// </summary>
            public string ResolvePath(string path)
            {
                path = ResolveVariables(path);

                if (!Path.IsPathRooted(path))
                    path = Path.Combine(BaseDir, path);

                return Path.GetFullPath(path);
            }

            /// <summary>
            /// Resolves path-like tokens inside an argument string.
            /// Any token containing <c>/</c> or <c>\</c> is treated as a path.
            /// </summary>
            public string ResolveArgPaths(string args)
            {
                var tokens = args.Split(' ');
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (tokens[i].Contains('/') || tokens[i].Contains('\\'))
                        tokens[i] = ResolvePath(tokens[i]);
                }
                return string.Join(' ', tokens);
            }
        }
    }
}
