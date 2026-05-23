// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Diagnostics;
using System.Text;

namespace ASLM.Services
{
    /// <summary>
    /// Detects whether the Docker CLI is installed and opens installation documentation.
    /// </summary>
    public sealed class DockerService
    {
        public const string WindowsInstallUrl =
            "https://www.docker.com/";

        private const int CliTimeoutSeconds = 5;


        // Platform checks

        /// <summary>
        /// Returns whether Docker CLI checks apply on the current operating system.
        /// </summary>
        public bool IsCheckRequiredOnThisPlatform() => OperatingSystem.IsWindows();

        /// <summary>
        /// Returns whether <c>docker</c> is available on PATH. Does not require a running daemon.
        /// </summary>
        public Task<bool> IsCliInstalledAsync(CancellationToken ct = default)
        {
            if (!IsCheckRequiredOnThisPlatform())
            {
                return Task.FromResult(true);
            }

            return Task.Run(() => IsCliInstalledCore(ct), ct);
        }


        // Install guide

        /// <summary>
        /// Opens the Docker installation documentation in the system browser.
        /// </summary>
        public async Task OpenInstallGuideAsync()
        {
            await Launcher.Default.OpenAsync(new Uri(WindowsInstallUrl));
        }


        // CLI probing

        /// <summary>
        /// Runs <c>docker --version</c> and returns whether the command succeeds.
        /// </summary>
        private static bool IsCliInstalledCore(CancellationToken ct)
        {
            try
            {
                var run = RunDocker(["--version"], ct);
                return run.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts one Docker CLI process and captures its exit code and output streams.
        /// </summary>
        private static (int ExitCode, string Stdout, string Stderr) RunDocker(
            IReadOnlyList<string> args,
            CancellationToken ct)
        {
            var arguments = string.Join(" ", args.Select(static arg => arg.Contains(' ') ? $"\"{arg}\"" : arg));
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            try
            {
                if (!process.Start())
                {
                    return (-1, string.Empty, string.Empty);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return (-1, string.Empty, string.Empty);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            if (!process.WaitForExit(TimeSpan.FromSeconds(CliTimeoutSeconds)))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort only.
                }

                return (-1, string.Empty, string.Empty);
            }

            return (process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
        }
    }
}
