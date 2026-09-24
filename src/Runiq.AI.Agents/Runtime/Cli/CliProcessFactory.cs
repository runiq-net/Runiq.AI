using System.Diagnostics;
using System.ComponentModel;

namespace Runiq.AI.Agents.Runtime.Cli;

internal sealed class CliProcessFactory : ICliProcessFactory
{
    public ICliProcess Start(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CliProcessContainment.Prepare(startInfo);
        if (OperatingSystem.IsWindows())
        {
            var started = CliWindowsProcess.Start(startInfo);
            try { return new CliProcess(started, cancellationToken); }
            catch { started.Dispose(); throw; }
        }
        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("CLI process did not start.");
            return new CliProcess(process, cancellationToken);
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* No process was started. */ }
            process.Dispose();
            throw;
        }
    }

    internal sealed class CliProcess : ICliProcess
    {
        private readonly Process process;
        private readonly CancellationTokenRegistration cancellation;
        private readonly CliProcessContainment containment;
        private readonly Task exit;
        private readonly object processLock = new();
        private bool disposed;
        private readonly CliWindowsProcess? windows;
        private readonly StreamWriter input;
        private readonly TextReader output;
        private readonly TextReader error;

        internal CliProcess(CliWindowsProcess started, CancellationToken token)
        {
            windows = started;
            process = started.Process;
            containment = started.Containment;
            input = started.Input;
            output = started.Output;
            error = started.Error;
            exit = ObserveExitAsync();
            cancellation = token.Register(Terminate);
        }

        internal CliProcess(Process process, CancellationToken token)
        {
            this.process = process;
            input = process.StandardInput;
            output = process.StandardOutput;
            error = process.StandardError;
            containment = new CliProcessContainment(process);
            exit = ObserveExitAsync();
            // Cancellation must kill the process even while a stream consumer is not requesting another event.
            cancellation = token.Register(Terminate);
        }

        public TextReader StandardOutput => output;
        public TextReader StandardError => error;

        public async Task WriteInputAsync(string input, CancellationToken cancellationToken)
        {
            await this.input.WriteAsync(input.AsMemory(), cancellationToken);
            await this.input.FlushAsync(cancellationToken);
            this.input.Close();
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await exit.WaitAsync(cancellationToken);
            return process.ExitCode;
        }

        private async Task ObserveExitAsync()
        {
            await process.WaitForExitAsync();
            lock (processLock)
            {
                if (!disposed) containment.StopDescendants();
            }
        }

        private void Terminate()
        {
            lock (processLock)
            {
                if (disposed) return;
                try
                {
                    containment.StopDescendants();
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { /* The process exited between the check and kill. */ }
                catch (Win32Exception) { /* Disposal retries and reports cleanup failures outside the token callback. */ }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cancellation.DisposeAsync();
            try
            {
                Terminate();
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await exit.WaitAsync(cleanup.Token);
            }
            finally
            {
                lock (processLock)
                {
                    disposed = true;
                    if (windows is not null) windows.Dispose();
                    else
                    {
                        containment.Dispose();
                        process.Dispose();
                    }
                }
            }
        }
    }
}
