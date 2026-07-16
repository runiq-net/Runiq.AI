namespace Runiq.AI.Rag.Configuration;

/// <summary>Identifies when a registered RAG index is eligible to start ingestion.</summary>
public enum RagIngestionStrategyKind
{
    /// <summary>Requires an explicit ingestion request and performs no startup work.</summary>
    Manual,
    /// <summary>Represents blocking application-startup ingestion.</summary>
    OnStartup,
    /// <summary>Represents non-blocking ingestion started after application startup.</summary>
    BackgroundOnStartup,
    /// <summary>Represents ingestion triggered by a schedule.</summary>
    Scheduled
}

/// <summary>Describes immutable, provider-independent ingestion start semantics for one RAG index.</summary>
public sealed class RagIngestionStartStrategy
{
    /// <summary>Initializes and validates an ingestion start strategy.</summary>
    /// <param name="kind">The lifecycle behavior represented by the strategy.</param>
    /// <param name="scheduleExpression">The five-field schedule expression, required only for <see cref="RagIngestionStrategyKind.Scheduled"/>.</param>
    public RagIngestionStartStrategy(RagIngestionStrategyKind kind, string? scheduleExpression = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The ingestion strategy kind is undefined.");

        if (kind != RagIngestionStrategyKind.Scheduled)
        {
            if (scheduleExpression is not null)
                throw new ArgumentException("A schedule expression is valid only for the Scheduled strategy.", nameof(scheduleExpression));

            Kind = kind;
            return;
        }

        ScheduleExpression = ValidateSchedule(scheduleExpression);
        Kind = kind;
    }

    /// <summary>Gets the lifecycle behavior represented by the strategy.</summary>
    public RagIngestionStrategyKind Kind { get; }

    /// <summary>Gets the five-field schedule expression for a scheduled strategy; otherwise, <see langword="null"/>.</summary>
    public string? ScheduleExpression { get; }

    private static string ValidateSchedule(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("A non-empty schedule expression is required for the Scheduled strategy.", nameof(expression));

        var normalized = string.Join(' ', expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var fields = normalized.Split(' ');
        if (fields.Length != 5 || fields.Any(field => field.Length == 0 || field.Any(character => !char.IsDigit(character) && character is not '*' and not ',' and not '-' and not '/')))
            throw new ArgumentException("The schedule must be a five-field expression containing only numbers, '*', ',', '-', and '/'.", nameof(expression));

        int[] minimums = [0, 0, 1, 1, 0];
        int[] maximums = [59, 23, 31, 12, 7];
        for (var index = 0; index < fields.Length; index++)
        {
            if (int.TryParse(fields[index], out var literal) && (literal < minimums[index] || literal > maximums[index]))
                throw new ArgumentException("The schedule contains a literal outside the supported five-field range.", nameof(expression));
        }

        return normalized;
    }
}

/// <summary>Builds exactly one ingestion start strategy for an index.</summary>
public sealed class RagIngestionStrategyBuilder
{
    private RagIngestionStartStrategy? strategy;

    /// <summary>Selects explicit/manual ingestion.</summary>
    /// <returns>The same builder instance.</returns>
    public RagIngestionStrategyBuilder Manual() => Select(new(RagIngestionStrategyKind.Manual));

    /// <summary>Selects blocking startup ingestion.</summary>
    /// <returns>The same builder instance.</returns>
    public RagIngestionStrategyBuilder OnStartup() => Select(new(RagIngestionStrategyKind.OnStartup));

    /// <summary>Selects non-blocking startup ingestion.</summary>
    /// <returns>The same builder instance.</returns>
    public RagIngestionStrategyBuilder BackgroundOnStartup() => Select(new(RagIngestionStrategyKind.BackgroundOnStartup));

    /// <summary>Selects scheduled ingestion using the runtime's default time-zone policy.</summary>
    /// <param name="scheduleExpression">The validated five-field schedule expression.</param>
    /// <returns>The same builder instance.</returns>
    public RagIngestionStrategyBuilder Scheduled(string scheduleExpression) => Select(new(RagIngestionStrategyKind.Scheduled, scheduleExpression));

    internal RagIngestionStartStrategy Build() => strategy ?? new(RagIngestionStrategyKind.Manual);

    private RagIngestionStrategyBuilder Select(RagIngestionStartStrategy selected)
    {
        if (strategy is not null)
            throw new InvalidOperationException("An ingestion start strategy has already been selected for this index.");

        strategy = selected;
        return this;
    }
}
