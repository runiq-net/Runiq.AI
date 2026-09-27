using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Agents.Runtime;

/// <summary>Owns the single model-window allocation: mandatory prompt, accepted evidence, then neutral history.</summary>
internal static class AgentContextAssembler
{
    internal static AgentContextAssembly Assemble(Agent agent, IReadOnlyList<ChatMessage> history,
        IReadOnlyList<ChatMessage> activeTurn, IReadOnlyList<ChatToolDefinition> tools,
        AgentRuntimeContext retrieval, int invocation,
        IReadOnlyDictionary<(string Document, string Chunk), int>? citationNumbers = null)
    {
        var rag = agent.Rag is { Enabled: true } enabled ? enabled : null;
        var limits = agent.ContextBudget ?? (rag is null ? new AgentContextBudgetOptions() :
            new AgentContextBudgetOptions(rag.ContextBudget.MaximumContextTokens, rag.ContextBudget.ResponseTokenReserve));
        var instructions = new ChatMessage(ChatRole.System, agent.Instructions);
        var instructionsCost = ContextTokenEstimator.EstimateMessage(instructions);
        var userCost = ContextTokenEstimator.EstimateMessage(activeTurn[0]);
        var continuationCost = activeTurn.Skip(1).Sum(ContextTokenEstimator.EstimateMessage);
        var toolsCost = ContextTokenEstimator.EstimateTools(tools);
        // Reranking/answerability failures must not become evidence merely because retrieval accepted candidates.
        var candidates = rag is null ? [] : retrieval.RetrievedRagContext;
        var policy = rag is null ? null : new ChatMessage(ChatRole.System,
            AgentInstructionsBuilder.BuildPolicy(rag.Mode, candidates.Count > 0));
        var otherCost = checked(continuationCost + toolsCost + (policy is null ? 0 : ContextTokenEstimator.EstimateMessage(policy)));
        var evidence = RagContextAssembler.Assemble(candidates, rag?.ContextBudget ?? new(),
            limits.MaximumContextTokens, limits.ResponseTokenReserve, instructionsCost, 0, userCost, otherCost, citationNumbers);
        if (rag is not null && evidence.SelectedResults.Count == 0 && candidates.Count > 0)
        {
            // Grounded no-context instructions may be longer. Recount the actual fallback policy before allowing a call.
            policy = new(ChatRole.System, AgentInstructionsBuilder.BuildPolicy(rag.Mode, false));
            otherCost = checked(continuationCost + toolsCost + ContextTokenEstimator.EstimateMessage(policy));
            evidence = RagContextAssembler.Assemble(candidates, rag.ContextBudget,
                limits.MaximumContextTokens, limits.ResponseTokenReserve, instructionsCost, 0, userCost, otherCost, citationNumbers);
        }
        var mandatoryCost = checked(instructionsCost + userCost + otherCost);
        var evidenceCost = evidence.Budget!.SelectedRagContextTokens;
        var remaining = Math.Max(0, limits.MaximumContextTokens - limits.ResponseTokenReserve - mandatoryCost - evidenceCost);
        var selectedHistory = agent.Memory is null || evidence.MandatoryPromptOverflow
            ? [] : MemoryHistorySelector.Select(history, agent.Memory.History, remaining,
                history.Select(ContextTokenEstimator.EstimateMessage).ToArray());
        var historyCost = selectedHistory.Sum(ContextTokenEstimator.EstimateMessage);
        var budget = new RagContextBudgetMetadata(limits.MaximumContextTokens, limits.ResponseTokenReserve,
            instructionsCost, historyCost, userCost, otherCost, evidence.Budget.AvailableRagContextTokens, evidenceCost);
        var context = new AgentRuntimeContext(evidence.SelectedResults, retrieval.RetrievedRagCandidates,
            retrieval.RejectedRagCandidates,
            candidates.Count > 0 && evidence.SelectedResults.Count == 0 ? RagNoContextReason.ContextBudgetExhausted : retrieval.NoContextReason,
            retrieval.RetrievalStatistics, retrieval.AcceptedRagResults,
            retrieval.ContextExcludedResults.Concat(evidence.ExcludedResults).ToArray(), budget, retrieval.Reranking)
        {
            RetrievalCorrelationId = retrieval.RetrievalCorrelationId,
            CitationNumbers = citationNumbers
        };
        var messages = new List<ChatMessage> { instructions };
        if (policy is not null) messages.Add(policy);
        if (AgentInstructionsBuilder.BuildExternalContext(context) is { } external) messages.Add(new(ChatRole.User, external));
        messages.AddRange(selectedHistory);
        messages.AddRange(activeTurn);
        var diagnostics = new AgentContextBudgetMetadata(invocation, limits.MaximumContextTokens, limits.ResponseTokenReserve,
            mandatoryCost, evidenceCost, historyCost, selectedHistory.Count,
            agent.Memory is null ? 0 : history.Count - selectedHistory.Count, context.ContextExcludedResults.Count, evidence.MandatoryPromptOverflow);
        return new(messages.ToArray(), context, diagnostics);
    }
}

internal sealed record AgentContextAssembly(IReadOnlyList<ChatMessage> Messages, AgentRuntimeContext Context,
    AgentContextBudgetMetadata Budget);
