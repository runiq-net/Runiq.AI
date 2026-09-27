using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents.Validation;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class MemoryExecutorCompatibilityTests
{
    [Theory]
    [InlineData(AgentExecutorKind.Model, true)]
    [InlineData(AgentExecutorKind.Codex, false)]
    [InlineData(AgentExecutorKind.Claude, false)]
    // Registration rejects unsupported framework Memory before resolving or starting any executor.
    public void Configuration_EnforcesBuiltInMatrix(AgentExecutorKind kind, bool allowed)
    {
        var agent = CreateAgent(kind).UseMemory();
        if (allowed) AgentValidator.ValidateRegisteredAgents([agent]);
        else Assert.Throws<InvalidOperationException>(() => AgentValidator.ValidateRegisteredAgents([agent]));
        AgentValidator.ValidateRegisteredAgents([CreateAgent(kind)]);
    }

    [Fact]
    // A custom model kind is insufficient; foundation support must be declared by its implementation.
    public void CustomExecutor_IsConservativeByDefault()
    {
        var agent = CreateAgent(AgentExecutorKind.Model).UseMemory();
        Assert.Equal("MemoryExecutorNotSupported", MemoryExecutorCompatibility.ValidateInvocation(agent, new("message"), new CustomExecutor())!.ErrorCode);
        Assert.Null(MemoryExecutorCompatibility.ValidateInvocation(agent, new("message"), new SupportingExecutor()));
    }

    [Fact]
    // Native sessions remain unchanged while mixed or silently ignored framework references fail explicitly.
    public void ContinuationInputs_AreNeverReinterpreted()
    {
        var query = new AgentQuery("message") { ProviderSessionId = "native" };
        var agent = CreateAgent(AgentExecutorKind.Model);
        Assert.Null(MemoryExecutorCompatibility.ValidateInvocation(agent, query, new CustomExecutor()));
        Assert.Equal("MemoryNotEnabled", MemoryExecutorCompatibility.ValidateInvocation(agent,
            new("message") { Memory = new("project", "thread") }, new CustomExecutor())!.ErrorCode);
        agent.UseMemory();
        Assert.Equal("MemoryContinuationConflict", MemoryExecutorCompatibility.ValidateInvocation(agent, query, new SupportingExecutor())!.ErrorCode);
    }

    private static Agent CreateAgent(AgentExecutorKind kind)
    {
        var agent = new Agent("agent", "Agent", "instructions");
        return kind switch
        {
            AgentExecutorKind.Model => agent.UseModel("openai/model"),
            AgentExecutorKind.Codex => agent.UseCodex(x => x.Model = "model"),
            _ => agent.UseClaude(x => x.Model = "model")
        };
    }
    private class CustomExecutor : IAgentExecutor
    {
        public AgentExecutorKind Kind => AgentExecutorKind.Model;
        public IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request, AgentRunContext run,
            AgentToolInvoker toolInvoker, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class SupportingExecutor : CustomExecutor, IAgentExecutor
    {
        public bool SupportsMemoryFoundation => true;
    }
}
