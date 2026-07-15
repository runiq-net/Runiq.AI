namespace Runiq.AI.Core.AI.Capabilities;

/// <summary>
/// Identifies an operation that a model can safely perform.
/// </summary>
[Flags]
public enum ModelCapability
{
    /// <summary>No model operation is declared.</summary>
    None = 0,
    /// <summary>The model accepts chat input and returns a response.</summary>
    Chat = 1,
    /// <summary>The model returns incremental chat response updates.</summary>
    Streaming = 2,
    /// <summary>The model accepts tool definitions and can return tool calls.</summary>
    ToolCalling = 4,
    /// <summary>The model supports the configured structured-output mechanism.</summary>
    StructuredOutput = 8,
    /// <summary>The model generates text embedding vectors.</summary>
    Embeddings = 16
}
