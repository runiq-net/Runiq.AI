using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Produces execution events for a request while leaving run identity, cancellation,
/// and terminal lifecycle ownership to the runtime.
/// </summary>
internal interface IAgentExecutor
{
    /// <summary>Executes one request using identity owned by the runtime.</summary>
    /// <param name="request">The reusable definition and complete per-call query.</param>
    /// <param name="run">The runtime-owned context for this invocation.</param>
    /// <param name="toolInvoker">The existing tool invoker selected for this invocation.</param>
    /// <param name="cancellationToken">The token used to cancel provider and tool work.</param>
    /// <returns>Execution events to be normalized and correlated by the runtime.</returns>
    IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request,
        AgentRunContext run, AgentToolInvoker toolInvoker, CancellationToken cancellationToken);
}

/// <summary>
/// Selects the configured executor without starting a run or taking ownership of its lifecycle.
/// </summary>
internal sealed class AgentExecutorResolver(IAgentExecutor modelExecutor)
{
    internal IAgentExecutor Resolve(AgentExecutionRequest request) => request.Agent.Executor?.Kind switch
    {
        AgentExecutorKind.Model => modelExecutor,
        _ => throw new InvalidOperationException("The requested executor is unavailable.")
    };
}
