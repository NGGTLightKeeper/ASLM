using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    static void Main()
    {
        Console.WriteLine("Creating Job Object...");

        // 1. Создаем Job-объект
        IntPtr hJob = CreateJobObject(IntPtr.Zero, null);
        if (hJob == IntPtr.Zero)
        {
             Console.WriteLine($"Error: Failed to create Job Object. Code: {Marshal.GetLastWin32Error()}");
             return;
        }

        Console.WriteLine($"Job Object created. Handle: {hJob}");

        // 2. Настраиваем Job на автоматическое завершение процессов при закрытии дескриптора
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = 0x2000 // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr ptr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            if (!SetInformationJobObject(hJob, 9, ptr, (uint)length)) // 9 = JobObjectExtendedLimitInformation
            {
                Console.WriteLine($"Error: Failed to set Job limits. Code: {Marshal.GetLastWin32Error()}");
                return;
            }
            Console.WriteLine("Job limits set (KILL_ON_JOB_CLOSE).");
        }
        finally { Marshal.FreeHGlobal(ptr); }

        // 3. Запускаем notepad.exe (for test visibility) instead of python if python is not in path
        // Trying python first as requested, but falling back or just notifying user.
        string exeName = "cmd.exe"; // cmd is always available
        string args = "/c echo Child Process Running... && pause";

        Console.WriteLine($"Starting child process: {exeName} {args}");

        var startInfo = new ProcessStartInfo
        {
            FileName = exeName,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        try
        {
            Process pyProcess = Process.Start(startInfo);
            if (pyProcess == null)
            {
                 Console.WriteLine("Error: Process.Start returned null.");
                 return;
            }

            Console.WriteLine($"Child process started. PID: {pyProcess.Id}");

            // 4. Привязываем процесс к нашему Job-объекту
            if (!AssignProcessToJobObject(hJob, pyProcess.Handle))
            {
                Console.WriteLine($"Error: Failed to assign process to Job. Code: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                Console.WriteLine("Child process successfully assigned to Job Object.");
            }

            Console.WriteLine("Press any key to close parent (Child should die)...");
            Console.ReadKey();

            // Explicitly close handle not needed as OS cleans up on exit, but good practice
            CloseHandle(hJob);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    #region Win32 API
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags, MinimumWorkingSetSize, MaximumWorkingSetSize, ActiveProcessLimit;
        public IntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS { public UInt64 ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    #endregion
}
