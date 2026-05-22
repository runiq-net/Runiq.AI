namespace Runiq.Agents;

/// <summary>
/// Agent çalışması sırasında üretilen stream olayını temsil eder.
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
    /// Assistant yanıtından gelen parça metin olayını oluşturur.
    /// </summary>
    /// <param name="content">Assistant yanıtına eklenecek parça metindir.</param>
    /// <returns>Assistant delta olayını temsil eden stream olayıdır.</returns>
    public static AgentExecutionEvent AssistantDelta(string content)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.AssistantDelta,
            Content: content);
    }

    /// <summary>
    /// Bir tool çağrısının başladığını bildiren stream olayını oluşturur.
    /// </summary>
    /// <param name="toolCallId">Model tarafından üretilen tool çağrısı kimliğidir.</param>
    /// <param name="toolName">Çalıştırılacak tool adıdır.</param>
    /// <param name="argumentsJson">Tool çağrısı için üretilen JSON argümanlarıdır.</param>
    /// <returns>Tool çağrısı başlangıç olayını temsil eden stream olayıdır.</returns>
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
    /// Bir tool çağrısının başarıyla tamamlandığını bildiren stream olayını oluşturur.
    /// </summary>
    /// <param name="toolCallId">Tamamlanan tool çağrısı kimliğidir.</param>
    /// <param name="toolName">Tamamlanan tool adıdır.</param>
    /// <param name="outputJson">Tool çalışması sonucunda üretilen JSON çıktıdır.</param>
    /// <returns>Tool çağrısı tamamlanma olayını temsil eden stream olayıdır.</returns>
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
    /// Bir tool çağrısının hata ile sonuçlandığını bildiren stream olayını oluşturur.
    /// </summary>
    /// <param name="toolCallId">Hata alan tool çağrısı kimliğidir.</param>
    /// <param name="toolName">Hata alan tool adıdır.</param>
    /// <param name="errorMessage">Tool çalışması sırasında oluşan hata mesajıdır.</param>
    /// <param name="errorCode">Varsa hata kodudur.</param>
    /// <returns>Tool çağrısı hata olayını temsil eden stream olayıdır.</returns>
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
    /// Agent çalışmasının başarıyla tamamlandığını bildiren stream olayını oluşturur.
    /// </summary>
    /// <returns>Tamamlanma olayını temsil eden stream olayıdır.</returns>
    public static AgentExecutionEvent Completed()
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Completed,
            Content: null);
    }

    /// <summary>
    /// Agent çalışmasının hata ile sonlandığını bildiren stream olayını oluşturur.
    /// </summary>
    /// <param name="errorMessage">Agent çalışması sırasında oluşan hata mesajıdır.</param>
    /// <param name="errorCode">Varsa hata kodudur.</param>
    /// <returns>Hata olayını temsil eden stream olayıdır.</returns>
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
}

/// <summary>
/// Agent stream olay tiplerini belirtir.
/// </summary>
public enum AgentExecutionEventKind
{
    /// <summary>
    /// Assistant yanıtından gelen parça metin olayını belirtir.
    /// </summary>
    AssistantDelta = 0,

    /// <summary>
    /// Tool çağrısının başladığını belirtir.
    /// </summary>
    ToolCallStarted = 1,

    /// <summary>
    /// Tool çağrısının başarıyla tamamlandığını belirtir.
    /// </summary>
    ToolCallCompleted = 2,

    /// <summary>
    /// Tool çağrısının hata ile sonuçlandığını belirtir.
    /// </summary>
    ToolCallFailed = 3,

    /// <summary>
    /// Agent çalışmasının başarıyla tamamlandığını belirtir.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Agent çalışmasının hata ile sonlandığını belirtir.
    /// </summary>
    Failed = 5
}