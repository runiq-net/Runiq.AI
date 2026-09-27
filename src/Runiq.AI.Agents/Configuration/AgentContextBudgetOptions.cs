namespace Runiq.AI.Agents.Configuration;

/// <summary>Defines one immutable prompt and response window for built-in model execution, with or without RAG.</summary>
public sealed record AgentContextBudgetOptions
{
    /// <summary>Creates a shared context window validated before model execution.</summary>
    /// <param name="maximumContextTokens">Positive total window; defaults to 32,768 estimated tokens.</param>
    /// <param name="responseTokenReserve">Nonnegative response allowance smaller than the window; defaults to 4,096.</param>
    /// <exception cref="ArgumentOutOfRangeException">The window or reserve is invalid.</exception>
    public AgentContextBudgetOptions(int maximumContextTokens = 32_768, int responseTokenReserve = 4_096)
    {
        if (maximumContextTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maximumContextTokens));
        if (responseTokenReserve < 0 || responseTokenReserve >= maximumContextTokens)
            throw new ArgumentOutOfRangeException(nameof(responseTokenReserve));
        MaximumContextTokens = maximumContextTokens;
        ResponseTokenReserve = responseTokenReserve;
    }

    /// <summary>Gets the total window shared by all prompt components and the response reserve.</summary>
    public int MaximumContextTokens { get; }

    /// <summary>Gets the response allowance deducted before selecting optional context.</summary>
    public int ResponseTokenReserve { get; }
}
