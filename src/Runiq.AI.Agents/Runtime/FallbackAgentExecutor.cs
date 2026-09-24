using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Runtime;

/// <summary>Marks framework defaults that yield to user-provided executors of the same kind.</summary>
internal interface IFallbackAgentExecutor : IAgentExecutor;

/// <summary>Preserves the scoped executor lifecycle while identifying a framework fallback registration.</summary>
internal sealed class FallbackAgentExecutor<TExecutor>(TExecutor executor) : IFallbackAgentExecutor
    where TExecutor : class, IAgentExecutor
{
    /// <inheritdoc />
    public AgentExecutorKind Kind => executor.Kind;

    /// <inheritdoc />
    public IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request,
        AgentRunContext run, AgentToolInvoker toolInvoker, CancellationToken cancellationToken)
        => executor.ExecuteAsync(request, run, toolInvoker, cancellationToken);
}
