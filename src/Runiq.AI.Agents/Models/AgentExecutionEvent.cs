namespace Runiq.AI.Agents;

/// <summary>
/// Agent çalismasi sirasinda üretilen stream olayini temsil eder.
/// </summary>
public sealed record AgentExecutionEvent(
    AgentExecutionEventKind Kind,
    string? Content,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ArgumentsJson = null,
    string? OutputJson = null,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Gets or initializes the structured RAG policy outcome carried by a terminal event.
    /// </summary>
    public AgentRagExecutionMetadata? Rag { get; init; }

    /// <summary>
    /// Assistant yanitindan gelen parça metin olayini olusturur.
    /// </summary>
    /// <param name="content">Assistant yanitina eklenecek parça metindir.</param>
    /// <returns>Assistant delta olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent AssistantDelta(string content)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.AssistantDelta,
            Content: content);
    }

    /// <summary>
    /// Bir tool çagrisinin basladigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="toolCallId">Model tarafindan üretilen tool çagrisi kimligidir.</param>
    /// <param name="toolName">Çalistirilacak tool adidir.</param>
    /// <param name="argumentsJson">Tool çagrisi için üretilen JSON argümanlaridir.</param>
    /// <returns>Tool çagrisi baslangiç olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent ToolCallStarted(
        string toolCallId,
        string toolName,
        string argumentsJson)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallStarted,
            Content: toolName,
            ToolCallId: toolCallId,
            ToolName: toolName,
            ArgumentsJson: argumentsJson);
    }

    /// <summary>
    /// Bir tool çagrisinin basariyla tamamlandigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="toolCallId">Tamamlanan tool çagrisi kimligidir.</param>
    /// <param name="toolName">Tamamlanan tool adidir.</param>
    /// <param name="outputJson">Tool çalismasi sonucunda üretilen JSON çiktidir.</param>
    /// <returns>Tool çagrisi tamamlanma olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent ToolCallCompleted(
        string toolCallId,
        string toolName,
        string outputJson)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallCompleted,
            Content: outputJson,
            ToolCallId: toolCallId,
            ToolName: toolName,
            OutputJson: outputJson);
    }

    /// <summary>
    /// Bir tool çagrisinin hata ile sonuçlandigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="toolCallId">Hata alan tool çagrisi kimligidir.</param>
    /// <param name="toolName">Hata alan tool adidir.</param>
    /// <param name="errorMessage">Tool çalismasi sirasinda olusan hata mesajidir.</param>
    /// <param name="errorCode">Varsa hata kodudur.</param>
    /// <returns>Tool çagrisi hata olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent ToolCallFailed(
        string toolCallId,
        string toolName,
        string errorMessage,
        string? errorCode = null)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallFailed,
            Content: errorMessage,
            ToolCallId: toolCallId,
            ToolName: toolName,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    /// Agent çalismasinin basariyla tamamlandigini bildiren stream olayini olusturur.
    /// </summary>
    /// <returns>Tamamlanma olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent Completed()
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Completed,
            Content: null);
    }

    /// <summary>
    /// Creates a successful completion event with a structured RAG policy outcome.
    /// </summary>
    /// <param name="rag">The RAG policy outcome observed by the framework.</param>
    /// <returns>The completed stream event.</returns>
    public static AgentExecutionEvent Completed(AgentRagExecutionMetadata? rag)
    {
        return Completed() with { Rag = rag };
    }

    /// <summary>
    /// Agent çalismasinin hata ile sonlandigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="errorMessage">Agent çalismasi sirasinda olusan hata mesajidir.</param>
    /// <param name="errorCode">Varsa hata kodudur.</param>
    /// <returns>Hata olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent Failed(
        string errorMessage,
        string? errorCode = null)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Failed,
            Content: errorMessage,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    /// Creates a failed completion event with a structured RAG policy outcome.
    /// </summary>
    /// <param name="errorMessage">The agent execution failure message.</param>
    /// <param name="errorCode">The optional agent execution failure code.</param>
    /// <param name="rag">The RAG policy outcome observed by the framework.</param>
    /// <returns>The failed stream event.</returns>
    public static AgentExecutionEvent Failed(
        string errorMessage,
        string? errorCode,
        AgentRagExecutionMetadata? rag)
    {
        return Failed(errorMessage, errorCode) with { Rag = rag };
    }
}

/// <summary>
/// Agent stream olay tiplerini belirtir.
/// </summary>
public enum AgentExecutionEventKind
{
    /// <summary>
    /// Assistant yanitindan gelen parça metin olayini belirtir.
    /// </summary>
    AssistantDelta = 0,

    /// <summary>
    /// Tool çagrisinin basladigini belirtir.
    /// </summary>
    ToolCallStarted = 1,

    /// <summary>
    /// Tool çagrisinin basariyla tamamlandigini belirtir.
    /// </summary>
    ToolCallCompleted = 2,

    /// <summary>
    /// Tool çagrisinin hata ile sonuçlandigini belirtir.
    /// </summary>
    ToolCallFailed = 3,

    /// <summary>
    /// Agent çalismasinin basariyla tamamlandigini belirtir.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Agent çalismasinin hata ile sonlandigini belirtir.
    /// </summary>
    Failed = 5
}
