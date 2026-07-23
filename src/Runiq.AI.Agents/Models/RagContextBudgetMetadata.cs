namespace Runiq.AI.Agents;

/// <summary>
/// Provides safe token counts for one deterministic RAG context assembly operation.
/// </summary>
public sealed record RagContextBudgetMetadata
{
    /// <summary>Initializes validated context-budget metadata.</summary>
    /// <param name="maximumContextTokens">The configured maximum context tokens.</param>
    /// <param name="responseTokenReserve">The reserved response tokens.</param>
    /// <param name="estimatedInstructionsTokens">The estimated instruction tokens.</param>
    /// <param name="estimatedConversationHistoryTokens">The estimated conversation-history tokens.</param>
    /// <param name="estimatedUserQueryTokens">The estimated current user-query tokens.</param>
    /// <param name="estimatedOtherRequiredPromptTokens">The estimated other required prompt tokens.</param>
    /// <param name="availableRagContextTokens">The tokens available for assembled RAG context.</param>
    /// <param name="selectedRagContextTokens">The tokens used by selected RAG context.</param>
    public RagContextBudgetMetadata(
        int maximumContextTokens,
        int responseTokenReserve,
        int estimatedInstructionsTokens,
        int estimatedConversationHistoryTokens,
        int estimatedUserQueryTokens,
        int estimatedOtherRequiredPromptTokens,
        int availableRagContextTokens,
        int selectedRagContextTokens)
    {
        MaximumContextTokens = RequireNonNegative(maximumContextTokens);
        ResponseTokenReserve = RequireNonNegative(responseTokenReserve);
        EstimatedInstructionsTokens = RequireNonNegative(estimatedInstructionsTokens);
        EstimatedConversationHistoryTokens = RequireNonNegative(estimatedConversationHistoryTokens);
        EstimatedUserQueryTokens = RequireNonNegative(estimatedUserQueryTokens);
        EstimatedOtherRequiredPromptTokens = RequireNonNegative(estimatedOtherRequiredPromptTokens);
        AvailableRagContextTokens = RequireNonNegative(availableRagContextTokens);
        SelectedRagContextTokens = RequireNonNegative(selectedRagContextTokens);
        if (SelectedRagContextTokens > AvailableRagContextTokens)
            throw new ArgumentException("Selected RAG context tokens cannot exceed the available RAG context tokens.");
    }

    /// <summary>Gets the configured maximum context tokens.</summary>
    public int MaximumContextTokens { get; }
    /// <summary>Gets the response token reserve deducted before RAG selection.</summary>
    public int ResponseTokenReserve { get; }
    /// <summary>Gets the estimated agent-instruction tokens.</summary>
    public int EstimatedInstructionsTokens { get; }
    /// <summary>Gets the estimated conversation-history tokens.</summary>
    public int EstimatedConversationHistoryTokens { get; }
    /// <summary>Gets the estimated current user-query tokens.</summary>
    public int EstimatedUserQueryTokens { get; }
    /// <summary>Gets the estimated non-RAG framework and tool prompt tokens.</summary>
    public int EstimatedOtherRequiredPromptTokens { get; }
    /// <summary>Gets the tokens available to the final assembled RAG context.</summary>
    public int AvailableRagContextTokens { get; }
    /// <summary>Gets the estimated tokens used by the final assembled RAG context.</summary>
    public int SelectedRagContextTokens { get; }

    private static int RequireNonNegative(int value) =>
        value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
}
