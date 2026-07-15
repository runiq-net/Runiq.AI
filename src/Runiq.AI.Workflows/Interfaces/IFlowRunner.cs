using Runiq.AI.Workflows.Models;
using Runiq.AI.Workflows.Domain;

namespace Runiq.AI.Workflows.Interfaces;

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

