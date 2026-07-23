namespace Runiq.AI.Agents.Configuration;

internal static class AgentRagPolicyValidator
{
    public static void Validate(AgentRagOptions options, bool requireIndex)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Enum.IsDefined(options.Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(options.Mode), options.Mode, "The RAG execution mode is not defined.");
        }

        if (!Enum.IsDefined(options.RetrievalMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.RetrievalMode),
                options.RetrievalMode,
                "The RAG retrieval mode is not defined.");
        }

        if (!Enum.IsDefined(options.NoContextBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.NoContextBehavior),
                options.NoContextBehavior,
                "The RAG no-context behavior is not defined.");
        }

        var acceptance = options.Acceptance;

        if (acceptance.MinimumRelevance is double minimumRelevance &&
            (!double.IsFinite(minimumRelevance) || minimumRelevance is < 0.0 or > 1.0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(acceptance.MinimumRelevance),
                minimumRelevance,
                "The minimum relevance must be finite and in the inclusive range from zero to one.");
        }

        if (acceptance.CandidateCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(acceptance.CandidateCount),
                acceptance.CandidateCount,
                "The candidate count must be greater than zero.");
        }

        if (acceptance.MaximumAcceptedResults <= 0 ||
            acceptance.MaximumAcceptedResults > acceptance.CandidateCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(acceptance.MaximumAcceptedResults),
                acceptance.MaximumAcceptedResults,
                "The maximum accepted result count must be greater than zero and cannot exceed the candidate count.");
        }

        if (options.Mode == RagExecutionMode.Required &&
            options.NoContextBehavior == RagNoContextBehavior.AnswerNormally)
        {
            throw new ArgumentException(
                "Required RAG execution cannot use AnswerNormally because the response could use information outside accepted context.",
                nameof(options.NoContextBehavior));
        }

        if (options.Enabled && requireIndex && string.IsNullOrWhiteSpace(options.IndexName))
        {
            throw new ArgumentException("IndexName cannot be empty.", nameof(options.IndexName));
        }
    }
}
