namespace Runiq.AI.Agents;

/// <summary>
/// Agent çalismasi sirasinda üretilen stream olayini temsil eder.
/// </summary>
public sealed record AgentExecutionEvent
{
    private AgentExecutionEvent(
        AgentExecutionEventKind Kind,
        string? Content,
        string? ToolCallId = null,
        string? ToolName = null,
        string? ArgumentsJson = null,
        string? OutputJson = null,
        string? ErrorCode = null,
        string? ErrorMessage = null,
        AgentRagExecutionMetadata? Rag = null,
        RagSearchEvent? RagSearch = null)
    {
        if ((Kind == AgentExecutionEventKind.RagSearch) != (RagSearch is not null))
        {
            throw new ArgumentException("The RAG search event kind and payload must be supplied together.", nameof(RagSearch));
        }

        this.Kind = Kind;
        this.Content = Content;
        this.ToolCallId = ToolCallId;
        this.ToolName = ToolName;
        this.ArgumentsJson = ArgumentsJson;
        this.OutputJson = OutputJson;
        this.ErrorCode = ErrorCode;
        this.ErrorMessage = ErrorMessage;
        this.Rag = Rag;
        this.RagSearch = RagSearch;
    }

    /// <summary>Gets the execution event kind.</summary>
    public AgentExecutionEventKind Kind { get; }

    /// <summary>Gets the event content, when applicable.</summary>
    public string? Content { get; }

    /// <summary>Gets the tool call identifier, when applicable.</summary>
    public string? ToolCallId { get; }

    /// <summary>Gets the tool name, when applicable.</summary>
    public string? ToolName { get; }

    /// <summary>Gets the serialized tool arguments, when applicable.</summary>
    public string? ArgumentsJson { get; }

    /// <summary>Gets the serialized tool output, when applicable.</summary>
    public string? OutputJson { get; }

    /// <summary>Gets the error code, when applicable.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets the error message, when applicable.</summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the structured RAG policy outcome carried by a terminal event.
    /// </summary>
    public AgentRagExecutionMetadata? Rag { get; }

    /// <summary>
    /// Gets the type-safe RAG search lifecycle payload carried by this execution event.
    /// </summary>
    public RagSearchEvent? RagSearch { get; }

    /// <summary>Gets citations validated against this execution's selected model context.</summary>
    public IReadOnlyList<AgentCitation> Citations { get; private init; } = [];

    /// <summary>
    /// Creates an execution-stream event that carries a RAG search lifecycle payload.
    /// </summary>
    /// <param name="ragSearch">The RAG search lifecycle payload.</param>
    /// <returns>The execution event that can be published through the existing runtime stream.</returns>
    public static AgentExecutionEvent FromRagSearch(RagSearchEvent ragSearch)
    {
        ArgumentNullException.ThrowIfNull(ragSearch);

        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.RagSearch,
            Content: null,
            RagSearch: ragSearch);
    }

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
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Completed,
            Content: null,
            Rag: rag);
    }

    /// <summary>Creates a successful completion event with RAG outcome and validated citations.</summary>
    /// <param name="rag">The RAG policy outcome observed by the framework.</param>
    /// <param name="citations">Citations validated against selected context.</param>
    /// <returns>The completed stream event.</returns>
    public static AgentExecutionEvent Completed(AgentRagExecutionMetadata? rag, IReadOnlyList<AgentCitation> citations)
    {
        ArgumentNullException.ThrowIfNull(citations);
        return new AgentExecutionEvent(Kind: AgentExecutionEventKind.Completed, Content: null, Rag: rag)
        {
            Citations = citations.ToArray(),
        };
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
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Failed,
            Content: errorMessage,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            Rag: rag);
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
    Failed = 5,

    /// <summary>
    /// A RAG search lifecycle event with a structured <see cref="AgentExecutionEvent.RagSearch"/> payload.
    /// </summary>
    RagSearch = 6
}
