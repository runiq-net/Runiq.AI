using Runiq.Workflows.Models;
using Runiq.Workflows.Domain;

namespace Runiq.Workflows.Interfaces;

/// <summary>
/// Runs flow definitions.
/// </summary>
public interface IFlowRunner
{
    Task<RunResult> ExecuteAsync(
        Flow flow,
        string input,
        CancellationToken cancellationToken = default);
}
