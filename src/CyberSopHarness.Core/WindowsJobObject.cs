using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CyberSopHarness.Core;

public sealed class WindowsJobObject : IDisposable
{
    private const int JobObjectExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitJobTime = 0x00000004;
    private const uint JobObjectLimitActiveProcess = 0x00000008;
    private const uint JobObjectLimitJobMemory = 0x00000200;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const uint ActiveProcessLimit = 1;
    private const ulong JobMemoryLimitBytes = 256UL * 1024UL * 1024UL;
    private static readonly long JobUserTimeLimitTicks = TimeSpan.FromSeconds(30).Ticks;
    private IntPtr _handle;

    public WindowsJobObject()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Job Objects require Windows");
        _handle = CreateJobObject(IntPtr.Zero, null);
        if (_handle == IntPtr.Zero) throw new InvalidOperationException("CreateJobObject failed: " + Marshal.GetLastWin32Error());
        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                PerJobUserTimeLimit = JobUserTimeLimitTicks,
                LimitFlags = JobObjectLimitKillOnJobClose | JobObjectLimitJobTime | JobObjectLimitActiveProcess | JobObjectLimitJobMemory,
                ActiveProcessLimit = ActiveProcessLimit
            },
            JobMemoryLimit = new UIntPtr(JobMemoryLimitBytes)
        };
        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, buffer, false);
            if (!SetInformationJobObject(_handle, JobObjectExtendedLimitInformationClass, buffer, (uint)size)) throw new InvalidOperationException("SetInformationJobObject failed: " + Marshal.GetLastWin32Error());
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public bool IsValid => _handle != IntPtr.Zero;

    public string BoundaryHash => Canonicalization.Sha256Hex("windows-job-object:v1:kill-on-close:active-process=1:job-memory=268435456:job-user-time=30s");

    public void Assign(Process process)
    {
        ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
        AssignHandle(process.Handle);
    }

    public void AssignHandle(IntPtr processHandle)
    {
        ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
        if (!AssignProcessToJobObject(_handle, processHandle)) throw new InvalidOperationException("AssignProcessToJobObject failed: " + Marshal.GetLastWin32Error());
    }

    public void Terminate(uint exitCode = 1)
    {
        ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
        if (!TerminateJobObject(_handle, exitCode)) throw new InvalidOperationException("TerminateJobObject failed: " + Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        CloseHandle(_handle);
        _handle = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr job, int informationClass, IntPtr jobObjectInformation, uint jobObjectInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

public sealed class WindowsContainedProcess : IDisposable
{
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint ResumeFailure = 0xffffffff;
    private IntPtr _processHandle;
    private IntPtr _threadHandle;

    private WindowsContainedProcess(WindowsJobObject job, int processId, IntPtr processHandle, IntPtr threadHandle)
    {
        _job = job;
        ProcessId = processId;
        _processHandle = processHandle;
        _threadHandle = threadHandle;
    }

    public int ProcessId { get; }
    private readonly WindowsJobObject _job;

    public static WindowsContainedProcess Start(WindowsJobObject job, string applicationPath, string arguments)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("suspended Windows process creation requires Windows");
        var startup = new StartupInfo { Cb = Marshal.SizeOf<StartupInfo>() };
        var commandLine = new System.Text.StringBuilder("\"" + applicationPath.Replace("\"", "\\\"", StringComparison.Ordinal) + "\" " + arguments);
        if (!CreateProcess(applicationPath, commandLine, IntPtr.Zero, IntPtr.Zero, false, CreateSuspended | CreateNoWindow, IntPtr.Zero, null, ref startup, out var processInfo)) throw new InvalidOperationException("CreateProcess failed: " + Marshal.GetLastWin32Error());
        try
        {
            job.AssignHandle(processInfo.ProcessHandle);
            if (ResumeThread(processInfo.ThreadHandle) == ResumeFailure) throw new InvalidOperationException("ResumeThread failed: " + Marshal.GetLastWin32Error());
            return new WindowsContainedProcess(job, processInfo.ProcessId, processInfo.ProcessHandle, processInfo.ThreadHandle);
        }
        catch
        {
            TerminateProcess(processInfo.ProcessHandle, 1);
            CloseHandle(processInfo.ThreadHandle);
            CloseHandle(processInfo.ProcessHandle);
            throw;
        }
    }

    public void Stop(uint exitCode = 1)
    {
        ObjectDisposedException.ThrowIf(_processHandle == IntPtr.Zero, this);
        _job.Terminate(exitCode);
    }

    public void Dispose()
    {
        if (_threadHandle != IntPtr.Zero) CloseHandle(_threadHandle);
        if (_processHandle != IntPtr.Zero) CloseHandle(_processHandle);
        _threadHandle = IntPtr.Zero;
        _processHandle = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public int ProcessId;
        public int ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string applicationName, System.Text.StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint creationFlags, IntPtr environment, string? currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr threadHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr processHandle, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
