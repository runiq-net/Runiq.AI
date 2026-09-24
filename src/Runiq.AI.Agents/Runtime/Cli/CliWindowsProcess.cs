using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Runiq.AI.Agents.Runtime.Cli;

/// <summary>Owns a native Windows launch, including its job, redirected pipes and managed process handle.</summary>
internal sealed class CliWindowsProcess : IDisposable
{
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateNoWindow = 0x08000000;
    private const uint StartUseStdHandles = 0x00000100;
    private const nuint HandleListAttribute = 0x00020002;
    private const nuint JobListAttribute = 0x0002000D;

    private AnonymousPipeServerStream? stdin, stdout, stderr;
    internal Process Process { get; private set; } = null!;
    internal CliProcessContainment Containment { get; private set; } = null!;
    internal StreamWriter Input { get; private set; } = null!;
    internal StreamReader Output { get; private set; } = null!;
    internal StreamReader Error { get; private set; } = null!;

    private CliWindowsProcess() { }

    internal static CliWindowsProcess Start(ProcessStartInfo start)
    {
        var launch = new CliWindowsProcess();
        try
        {
            launch.Containment = new CliProcessContainment();
            launch.stdin = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            launch.stdout = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            launch.stderr = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            launch.Input = new StreamWriter(launch.stdin, start.StandardInputEncoding ?? new UTF8Encoding(false));
            launch.Output = new StreamReader(launch.stdout, start.StandardOutputEncoding ?? Encoding.UTF8);
            launch.Error = new StreamReader(launch.stderr, start.StandardErrorEncoding ?? Encoding.UTF8);
            launch.Create(start);
            return launch;
        }
        catch
        {
            // Closing the job also kills a suspended process if any setup step after CreateProcessW fails.
            launch.Dispose();
            throw;
        }
    }

    private void Create(ProcessStartInfo start)
    {
        using var attributes = new StartupAttributes();
        attributes.Add(JobListAttribute, [Containment.Job.DangerousGetHandle()]);
        attributes.Add(HandleListAttribute,
            [stdin!.ClientSafePipeHandle.DangerousGetHandle(), stdout!.ClientSafePipeHandle.DangerousGetHandle(),
                stderr!.ClientSafePipeHandle.DangerousGetHandle()]);
        var startup = new StartupInfoEx
        {
            StartupInfo = new StartupInfo
            {
                Size = Marshal.SizeOf<StartupInfoEx>(), Flags = StartUseStdHandles,
                Input = stdin.ClientSafePipeHandle.DangerousGetHandle(),
                Output = stdout.ClientSafePipeHandle.DangerousGetHandle(),
                Error = stderr.ClientSafePipeHandle.DangerousGetHandle()
            },
            AttributeList = attributes.Pointer
        };
        var commandLine = new StringBuilder(QuoteArgument(start.FileName));
        foreach (var argument in start.ArgumentList) commandLine.Append(' ').Append(QuoteArgument(argument));
        var environment = string.Join('\0', start.Environment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Where(pair => pair.Value is not null).Select(pair => $"{pair.Key}={pair.Value}")) + "\0\0";
        var environmentBlock = Marshal.StringToHGlobalUni(environment);
        try
        {
            // JOB_LIST attaches atomically at creation. Suspension only protects managed handle acquisition
            // against a fast exit/PID reuse; it is not the mechanism that guarantees job membership.
            if (!CreateProcessW(start.FileName, commandLine, IntPtr.Zero, IntPtr.Zero, true,
                    CreateSuspended | CreateUnicodeEnvironment | ExtendedStartupInfoPresent | CreateNoWindow,
                    environmentBlock, start.WorkingDirectory, ref startup, out var information))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            using var nativeProcess = new SafeProcessHandle(information.Process, ownsHandle: true);
            using var thread = new SafeFileHandle(information.Thread, ownsHandle: true);
            Process = Process.GetProcessById(information.ProcessId);
            _ = Process.Handle; // Retain a handle before allowing even an immediately exiting executable to run.
            stdin.DisposeLocalCopyOfClientHandle();
            stdout.DisposeLocalCopyOfClientHandle();
            stderr.DisposeLocalCopyOfClientHandle();
            if (ResumeThread(thread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.FreeHGlobal(environmentBlock);
            // Attribute values borrow these SafeHandles only for the duration of CreateProcessW.
            GC.KeepAlive(Containment);
            GC.KeepAlive(stdin);
            GC.KeepAlive(stdout);
            GC.KeepAlive(stderr);
        }
    }

    // Windows argv quoting: double backslashes before quotes and before the closing quote.
    internal static string QuoteArgument(string argument)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Containment?.Dispose();
        // Own the pipes directly: do not flush a buffered StreamWriter into a terminated child at disposal.
        stdin?.Dispose();
        stdout?.Dispose();
        stderr?.Dispose();
        Process?.Dispose();
    }

    /// <summary>Owns the native attribute list and its borrowed-handle arrays until CreateProcessW returns.</summary>
    private sealed class StartupAttributes : IDisposable
    {
        private readonly List<IntPtr> values = [];
        internal IntPtr Pointer { get; }

        internal StartupAttributes()
        {
            nuint size = 0;
            InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            var sizingError = Marshal.GetLastWin32Error();
            if (sizingError != 122) throw new Win32Exception(sizingError); // ERROR_INSUFFICIENT_BUFFER is expected.
            Pointer = Marshal.AllocHGlobal(checked((nint)size));
            if (InitializeProcThreadAttributeList(Pointer, 2, 0, ref size)) return;
            var error = Marshal.GetLastWin32Error();
            Marshal.FreeHGlobal(Pointer);
            throw new Win32Exception(error);
        }

        internal void Add(nuint attribute, IntPtr[] handles)
        {
            var size = checked(handles.Length * IntPtr.Size);
            var value = Marshal.AllocHGlobal(size);
            values.Add(value);
            Marshal.Copy(handles, 0, value, handles.Length);
            if (!UpdateProcThreadAttribute(Pointer, 0, attribute, value, (nuint)size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            DeleteProcThreadAttributeList(Pointer);
            foreach (var value in values) Marshal.FreeHGlobal(value);
            Marshal.FreeHGlobal(Pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        internal int Size;
        internal IntPtr Reserved, Desktop, Title;
        internal uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        internal ushort ShowWindow, ReservedSize;
        internal IntPtr ReservedBytes, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        internal StartupInfo StartupInfo;
        internal IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        internal IntPtr Process, Thread;
        internal int ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string applicationName, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation information);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(SafeFileHandle thread);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute,
        IntPtr value, nuint size, IntPtr previousValue, IntPtr returnSize);
    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);
}
