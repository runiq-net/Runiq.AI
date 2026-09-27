using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;

namespace Runiq.AI.Agents.Validation;

internal static class MemoryExecutorCompatibility
{
    internal static AgentExecutionResult? ValidateConfiguration(Agent agent) =>
        agent.Memory is not null && agent.Executor?.Kind != AgentExecutorKind.Model
            ? AgentExecutionResult.Failure("MemoryExecutorNotSupported", "Framework Memory foundations require a supported model executor.")
            : null;

    internal static AgentExecutionResult? ValidateInvocation(Agent agent, AgentQuery query, IAgentExecutor executor)
    {
        if (agent.Memory is null)
            return query.Memory is null ? null : AgentExecutionResult.Failure("MemoryNotEnabled", "Memory is not enabled for this agent.");
        if (!executor.SupportsMemoryFoundation)
            return AgentExecutionResult.Failure("MemoryExecutorNotSupported", "The executor has not declared Memory foundation support.");
        if (query.ProviderSessionId is not null)
            return AgentExecutionResult.Failure("MemoryContinuationConflict", "Framework Memory cannot be combined with provider-session continuation.");
        return null;
    }
}
