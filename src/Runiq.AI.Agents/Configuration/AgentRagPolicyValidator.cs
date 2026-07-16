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

        if (!Enum.IsDefined(options.NoContextBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.NoContextBehavior),
                options.NoContextBehavior,
                "The RAG no-context behavior is not defined.");
        }

        if (options.MinimumRelevanceScore is double minimumScore &&
            (double.IsNaN(minimumScore) || double.IsInfinity(minimumScore)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MinimumRelevanceScore),
                minimumScore,
                "The minimum relevance score must be finite.");
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
