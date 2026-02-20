using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace ASLM.Services
{
    /// <summary>
    /// Manages a Win32 Job Object that groups the current ASLM process with all
    /// its child processes (modules). This achieves two goals:
    /// 1. All module processes appear as subprocesses of ASLM in Task Manager.
    /// 2. All child processes are killed automatically when ASLM exits or crashes
    ///    (via JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE).
    /// </summary>
    public sealed class ProcessTracker : IDisposable
    {
        private SafeFileHandle? _jobHandle;
        private readonly ILogger<ProcessTracker> _logger;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessTracker"/> class.
        /// Creates a Job Object, configures KILL_ON_JOB_CLOSE, and assigns the
        /// current process to it so that child processes inherit the same group.
        /// </summary>
        public ProcessTracker(ILogger<ProcessTracker> logger)
        {
            _logger = logger;

            if (!OperatingSystem.IsWindows())
            {
                _logger.LogWarning("ProcessTracker: Job Objects are only supported on Windows.");
                return;
            }

            _jobHandle = CreateJobObject(IntPtr.Zero, "ASLM_ProcessGroup");
            if (_jobHandle.IsInvalid)
            {
                _logger.LogError("Failed to create Job Object. Error: {Error}", Marshal.GetLastWin32Error());
                return;
            }

            // Configure: kill all processes in the job when the handle is closed
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOBOBJECTLIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr infoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, infoPtr, false);
                if (!SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)length))
                {
                    _logger.LogError("Failed to set Job Object info. Error: {Error}", Marshal.GetLastWin32Error());
                    return;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }

            // Assign the CURRENT process (ASLM) to the Job Object.
            // This is essential: when child processes are also assigned to this job,
            // Task Manager groups them all under the ASLM entry because they share
            // the same Job Object as the main application process.
            if (!AssignProcessToJobObject(_jobHandle, Process.GetCurrentProcess().Handle))
            {
                // If we cannot assign ASLM to the job (e.g., already in a job),
                // we must NOT assign child processes to this new job either.
                // Otherwise, they will be split into a separate group in Task Manager.
                // By abandoning this custom job, child processes will naturally inherit
                // ASLM's existing job (or lack thereof), keeping them grouped together.
                _logger.LogWarning("Failed to assign current process to Job Object (Error: {Error}). Disabling custom process grouping.", Marshal.GetLastWin32Error());

                _jobHandle.Dispose();
                _jobHandle = null;
            }
            else
            {
                _logger.LogInformation("ProcessTracker initialized — ASLM and child processes are grouped.");
            }
        }

        /// <summary>
        /// Assigns a child process to the Job Object so it appears as a subprocess
        /// of ASLM in Task Manager and is killed when ASLM exits.
        /// </summary>
        /// <param name="process">The child process to track.</param>
        /// <returns>True if the process was successfully assigned.</returns>
        public bool AddProcess(Process process)
        {
            if (_disposed) return false;

            // If the job handle was disabled (because ASLM couldn't be assigned),
            // we return true to indicate "handled via default inheritance".
            if (_jobHandle == null || _jobHandle.IsInvalid)
                return true;

            try
            {
                bool result = AssignProcessToJobObject(_jobHandle, process.Handle);
                if (!result)
                {
                    _logger.LogWarning("Failed to assign process {PID} to Job Object. Error: {Error}",
                        process.Id, Marshal.GetLastWin32Error());
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception assigning process to Job Object.");
                return false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _jobHandle?.Dispose();
        }

        // ─── Win32 P/Invoke ──────────────────────────────────────────

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(SafeFileHandle hJob, JobObjectInfoType infoType,
            IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle hJob, IntPtr hProcess);

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9
        }

        [Flags]
        private enum JOBOBJECTLIMIT : uint
        {
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public JOBOBJECTLIMIT LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public long Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
