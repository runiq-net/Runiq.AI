namespace Runiq.AI.Memory.Configuration;

/// <summary>Bounds the model-context projection without changing the durable transcript.</summary>
public sealed record MemoryHistoryOptions
{
    /// <summary>Creates immutable history limits.</summary>
    /// <param name="maximumMessages">Maximum retained messages; null disables this limit, zero excludes all history.</param>
    /// <param name="maximumTokens">Maximum retained token cost; null disables this limit, zero excludes all history.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is negative.</exception>
    public MemoryHistoryOptions(int? maximumMessages = null, int? maximumTokens = null)
    {
        if (maximumMessages < 0) throw new ArgumentOutOfRangeException(nameof(maximumMessages));
        if (maximumTokens < 0) throw new ArgumentOutOfRangeException(nameof(maximumTokens));
        MaximumMessages = maximumMessages;
        MaximumTokens = maximumTokens;
    }

    /// <summary>Gets the message limit, or null for no additional limit beyond the assigned budget.</summary>
    public int? MaximumMessages { get; }

    /// <summary>Gets the token limit, or null for no additional limit beyond the assigned budget.</summary>
    public int? MaximumTokens { get; }
}
