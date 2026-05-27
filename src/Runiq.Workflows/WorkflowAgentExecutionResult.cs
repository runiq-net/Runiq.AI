namespace Runiq.Workflows;

/// <summary>
/// Workflow agent adımı çalıştırmasının çıktısını ve alt tool trace bilgisini temsil eder.
/// </summary>
public sealed class WorkflowAgentExecutionResult
{
    public WorkflowAgentExecutionResult(
        bool isSuccess,
        string? output,
        IReadOnlyList<WorkflowToolCallExecutionResult> toolCalls,
        string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        Output = output;
        ToolCalls = toolCalls ?? throw new ArgumentNullException(nameof(toolCalls));
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Agent adımının başarılı tamamlanıp tamamlanmadığını döner.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Agent tarafından üretilen metinsel çıktıyı döner.
    /// </summary>
    public string? Output { get; }

    /// <summary>
    /// Agent adımı içinde çalıştırılan tool çağrılarını döner.
    /// </summary>
    public IReadOnlyList<WorkflowToolCallExecutionResult> ToolCalls { get; }

    /// <summary>
    /// Agent adımı başarısız olduysa hata mesajını döner.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Başarılı workflow agent çalıştırma sonucu oluşturur.
    /// </summary>
    public static WorkflowAgentExecutionResult Success(
        string output,
        IReadOnlyList<WorkflowToolCallExecutionResult> toolCalls)
    {
        return new WorkflowAgentExecutionResult(
            isSuccess: true,
            output: output,
            toolCalls: toolCalls);
    }

    /// <summary>
    /// Başarısız workflow agent çalıştırma sonucu oluşturur.
    /// </summary>
    public static WorkflowAgentExecutionResult Failure(
        string errorMessage,
        IReadOnlyList<WorkflowToolCallExecutionResult> toolCalls)
    {
        return new WorkflowAgentExecutionResult(
            isSuccess: false,
            output: null,
            toolCalls: toolCalls,
            errorMessage: errorMessage);
    }
}
