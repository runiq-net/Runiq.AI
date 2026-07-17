namespace Runiq.AI.Rag.Abstractions.Observability;

/// <summary>Redacts potentially sensitive values before they enter RAG observability payloads.</summary>
public interface IRagObservabilityRedactor
{
    /// <summary>Returns a safe replacement for a potentially sensitive value, or <see langword="null"/> to omit it.</summary>
    /// <param name="value">The raw value. Implementations must not retain it unless the application explicitly permits that use.</param>
    /// <param name="kind">The semantic kind of the supplied value.</param>
    /// <returns>The redacted value, or <see langword="null"/>.</returns>
    string? Redact(string value, RagObservabilityValueKind kind);
}

/// <summary>Identifies the semantic kind of a value supplied to an observability redactor.</summary>
public enum RagObservabilityValueKind
{
    /// <summary>An original or effective retrieval query.</summary>
    Query = 0,
    /// <summary>Retrieved document or chunk content used to create an opt-in preview.</summary>
    Content = 1,
    /// <summary>An explicitly allowlisted metadata value.</summary>
    Metadata = 2,
}
