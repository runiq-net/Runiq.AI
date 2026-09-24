using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Runiq.AI.Agents.Runtime.Codex;

/// <summary>Keeps ordinary CLI descendants owned after their parent exits or the consumer abandons a run.</summary>
internal sealed class CodexProcessContainment : IDisposable
{
    private readonly SafeFileHandle? job;
    private readonly int? processGroup;

    internal static void Prepare(ProcessStartInfo start)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            throw new CodexException("CodexPlatformNotSupported");
        if (!OperatingSystem.IsLinux()) return;
        var setsid = new[] { "/usr/bin/setsid", "/bin/setsid" }.FirstOrDefault(File.Exists)
            ?? throw new CodexException("CodexConfigurationInvalid");
        var executable = start.FileName;
        var arguments = start.ArgumentList.ToArray();
        start.FileName = setsid;
        start.ArgumentList.Clear();
        start.ArgumentList.Add("--wait");
        start.ArgumentList.Add(executable);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
    }

    internal CodexProcessContainment(Process process) => processGroup = process.Id;

    // Windows membership is supplied to CreateProcessW, never assigned to an already running process.
    internal SafeFileHandle Job => job ?? throw new InvalidOperationException("No Windows job exists.");

    internal CodexProcessContainment()
    {
        job = CreateJobObject(IntPtr.Zero, null);
        try
        {
            if (job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var limits = new ExtendedLimitInformation();
            limits.BasicLimitInformation.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch { job.Dispose(); throw; }
    }

    internal void StopDescendants()
    {
        if (job is not null && !TerminateJobObject(job, 1))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (processGroup is { } group && Kill(-group, 9) != 0 && Marshal.GetLastWin32Error() != 3)
            throw new Win32Exception(Marshal.GetLastWin32Error()); // ESRCH means the group already exited.
    }

    public void Dispose() => job?.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        internal long PerProcessUserTimeLimit;
        internal long PerJobUserTimeLimit;
        internal uint LimitFlags;
        internal UIntPtr MinimumWorkingSetSize;
        internal UIntPtr MaximumWorkingSetSize;
        internal uint ActiveProcessLimit;
        internal UIntPtr Affinity;
        internal uint PriorityClass;
        internal uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        internal ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        internal ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        internal BasicLimitInformation BasicLimitInformation;
        internal IoCounters IoInfo;
        internal UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass,
        ref ExtendedLimitInformation information, uint length);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int processId, int signal);
}
