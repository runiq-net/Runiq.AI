using System.Diagnostics;

namespace Runiq.AI.Agents.Runtime.Cli;

// The only extra abstraction is the OS process boundary, so contract tests need no CLI or credentials.
internal interface ICliProcessFactory
{
    ICliProcess Start(ProcessStartInfo startInfo, CancellationToken cancellationToken);
}

internal interface ICliProcess : IAsyncDisposable
{
    TextReader StandardOutput { get; }
    TextReader StandardError { get; }
    Task WriteInputAsync(string input, CancellationToken cancellationToken);
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);
}
