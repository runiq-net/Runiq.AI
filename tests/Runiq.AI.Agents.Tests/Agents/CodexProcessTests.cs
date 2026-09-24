using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Runiq.AI.Agents.Runtime.Codex;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class CodexProcessTests
{
    [Fact]
    // Verifies actual redirected pipes support stdin, concurrent stderr draining and exit-code propagation without Codex.
    public async Task NativeProcess_DrainsBothPipesAndReceivesInput()
    {
        var start = Command(
            "$line = [Console]::ReadLine(); [Console]::Out.WriteLine($line); [Console]::Error.Write(('x' * 100000)); exit 7",
            "read line; printf '%s\\n' \"$line\"; head -c 100000 /dev/zero >&2; exit 7");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var process = new CodexProcessFactory().Start(start, deadline.Token);
        var error = CodexOutputReader.DrainErrorAsync(process.StandardError, deadline.Token);
        await process.WriteInputAsync("literal $() ; & <input>\n", deadline.Token);
        Assert.Equal("literal $() ; & <input>", await process.StandardOutput.ReadLineAsync(deadline.Token));
        Assert.Equal(7, await process.WaitForExitAsync(deadline.Token));
        Assert.Equal(16_384, (await error).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Verifies cancelling or abandoning the actual process kills its child and reaps the parent.
    public async Task NativeProcess_CancellationAndDisposalKillDescendants(bool cancel)
    {
        var start = Command(
            "$child = Start-Process powershell.exe -ArgumentList '-NoProfile -NonInteractive -Command Start-Sleep 120' -WindowStyle Hidden -PassThru; [Console]::Out.WriteLine($child.Id); Start-Sleep 120",
            "sleep 120 & echo $!; wait");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var process = new CodexProcessFactory().Start(start, deadline.Token);
        Process? child = null;
        try
        {
            var childId = int.Parse((await process.StandardOutput.ReadLineAsync(deadline.Token))!);
            child = Process.GetProcessById(childId);
            Assert.False(child.HasExited);
            if (cancel)
            {
                await deadline.CancelAsync();
                using var exitDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(exitDeadline.Token);
            }
        }
        finally { await process.DisposeAsync(); }
        if (child is not null)
        {
            using (child)
            {
                using var childDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await child.WaitForExitAsync(childDeadline.Token);
                Assert.True(child.HasExited);
            }
        }
    }

    [Fact]
    // Verifies descendants are terminated even when the parent exits normally before the consumer disposes it.
    public async Task NativeProcess_ParentExitStopsRemainingChildren()
    {
        var start = Command(
            "$child = Start-Process powershell.exe -ArgumentList '-NoProfile -NonInteractive -Command Start-Sleep 120' -WindowStyle Hidden -PassThru; [Console]::Out.WriteLine($child.Id); Start-Sleep 1; exit 0",
            "sleep 120 & echo $!; sleep 1; exit 0");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var process = new CodexProcessFactory().Start(start, deadline.Token);
        var id = int.Parse((await process.StandardOutput.ReadLineAsync(deadline.Token))!);
        using var child = Process.GetProcessById(id);
        Assert.Equal(0, await process.WaitForExitAsync(deadline.Token));
        await child.WaitForExitAsync(deadline.Token);
        Assert.True(child.HasExited);
    }

    [WindowsTheory]
    [InlineData("normal")]
    [InlineData("cancel")]
    [InlineData("timeout")]
    [InlineData("dispose")]
    // Forces child creation before lifecycle attachment; no scheduler delay can hide a late job assignment.
    public async Task WindowsProcess_ChildCreatedBeforeWrapperIsContained(string completion)
    {
        var start = Command(
            "$child = Start-Process powershell.exe -ArgumentList '-NoProfile -NonInteractive -Command Start-Sleep 120' -WindowStyle Hidden -PassThru; [Console]::Out.WriteLine($child.Id); [Console]::ReadLine() | Out-Null; exit 0",
            "");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var lifetime = new CancellationTokenSource();
        using var started = CodexWindowsProcess.Start(start);
        using var child = Process.GetProcessById(int.Parse((await started.Output.ReadLineAsync(deadline.Token))!));
        try
        {
            Assert.False(child.HasExited);
            Assert.True(IsProcessInJob(child.Handle, started.Containment.Job, out var contained));
            Assert.True(contained, "The immediate child must already belong to this launch's job before wrapper attachment.");
            if (completion == "normal")
            {
                await started.Input.WriteLineAsync("exit".AsMemory(), deadline.Token);
                await started.Input.FlushAsync(deadline.Token);
                await started.Process.WaitForExitAsync(deadline.Token);
                Assert.Equal(0, started.Process.ExitCode);
                Assert.False(child.HasExited);
            }

            await using (var wrapper = new CodexProcessFactory.CodexProcess(started, lifetime.Token))
            {
                if (completion == "cancel") await lifetime.CancelAsync();
                if (completion == "timeout") lifetime.CancelAfter(TimeSpan.FromMilliseconds(1));
                if (completion != "dispose") await wrapper.WaitForExitAsync(deadline.Token);
            }
            await child.WaitForExitAsync(deadline.Token);
            Assert.True(child.HasExited);
        }
        finally
        {
            // A failing regression must not itself leak the deliberately long-lived helper.
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(deadline.Token);
        }
    }

    [WindowsTheory]
    [InlineData(false, 2)]
    [InlineData(true, 267)]
    // Verifies native startup failures preserve Win32 codes used by the executor's existing error mapping.
    public void WindowsProcess_StartupFailurePreservesNativeError(bool invalidDirectory, int expectedCode)
    {
        var start = Command("exit 0", "");
        if (invalidDirectory) start.WorkingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        else start.FileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
        var error = Assert.Throws<Win32Exception>(() => new CodexProcessFactory().Start(start, CancellationToken.None));
        Assert.Equal(expectedCode, error.NativeErrorCode);
    }

    [WindowsTheory]
    [InlineData("a \"quoted\" value \\ and trailing\\")]
    // Verifies Unicode environment, command quoting, working directory and live output survive native creation.
    public async Task WindowsProcess_PreservesLaunchConfigurationAndIncrementalOutput(string value)
    {
        var start = Command(
            "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); [Console]::Out.WriteLine(\"quoted \\\" ); [Console]::Out.WriteLine($env:RUNIQ_NATIVE_TEST); [Console]::Out.WriteLine([Environment]::CurrentDirectory); [Console]::ReadLine() | Out-Null; exit 0", "");
        start.StandardOutputEncoding = Encoding.UTF8;
        start.Environment["RUNIQ_NATIVE_TEST"] = "Türkçe 日本語 " + value;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var process = new CodexProcessFactory().Start(start, deadline.Token);
        Assert.Equal("quoted \\", await process.StandardOutput.ReadLineAsync(deadline.Token));
        Assert.Equal("Türkçe 日本語 " + value, await process.StandardOutput.ReadLineAsync(deadline.Token));
        Assert.Equal(Path.TrimEndingDirectorySeparator(start.WorkingDirectory),
            Path.TrimEndingDirectorySeparator((await process.StandardOutput.ReadLineAsync(deadline.Token))!));
        await process.WriteInputAsync("exit\n", deadline.Token);
        Assert.Equal(0, await process.WaitForExitAsync(deadline.Token));
    }

    [Theory]
    [InlineData("", "\"\"")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("a\\", "\"a\\\\\"")]
    [InlineData("a\\\"b", "\"a\\\\\\\"b\"")]
    // Protects the Windows argv escaping rules required when bypassing Process.Start.
    public void WindowsArguments_EscapeQuotesAndBackslashes(string argument, string expected)
        => Assert.Equal(expected, CodexWindowsProcess.QuoteArgument(argument));

    private sealed class WindowsTheoryAttribute : TheoryAttribute
    {
        public WindowsTheoryAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "Requires Windows native process and Job Object APIs.";
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool result);

    private static ProcessStartInfo Command(string windows, string unix)
    {
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")
                : "/bin/sh",
            WorkingDirectory = Path.GetTempPath(), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in OperatingSystem.IsWindows()
                     ? new[] { "-NoProfile", "-NonInteractive", "-Command", windows }
                     : new[] { "-c", unix }) start.ArgumentList.Add(argument);
        return start;
    }
}
