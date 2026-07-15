namespace Runiq.AI.Agents
{
    /// <summary>
    /// Agent çalistirma sonucunu final cevap, hata bilgisi ve görünür execution adimlariyla temsil eder.
    /// </summary>
    public sealed class AgentExecutionResult
    {
        private AgentExecutionResult(
            bool isSuccess,
            string? message,
            string? errorCode,
            string? errorMessage,
            IReadOnlyList<AgentExecutionStep> steps)
        {
            IsSuccess = isSuccess;
            Message = message;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Steps = steps;
        }

        /// <summary>
        /// Agent çalistirma isleminin basarili olup olmadigini belirtir.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Basarili çalistirma sonucunda modelden dönen final cevaptir.
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Basarisiz çalistirma durumunda hata kodudur.
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// Basarisiz çalistirma durumunda kullaniciya veya gelistiriciye gösterilebilecek hata açiklamasidir.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Agent çalistirmasi sirasinda olusan görünür execution adimlarini döner.
        /// </summary>
        public IReadOnlyList<AgentExecutionStep> Steps { get; }

        /// <summary>
        /// Basarili agent çalistirma sonucu olusturur.
        /// </summary>
        public static AgentExecutionResult Success(string message)
        {
            return Success(message, []);
        }

        /// <summary>
        /// Execution adimlariyla birlikte basarili agent çalistirma sonucu olusturur.
        /// </summary>
        public static AgentExecutionResult Success(
            string message,
            IReadOnlyList<AgentExecutionStep> steps)
        {
            return new AgentExecutionResult(
                isSuccess: true,
                message: message,
                errorCode: null,
                errorMessage: null,
                steps: steps);
        }

        /// <summary>
        /// Basarisiz agent çalistirma sonucu olusturur.
        /// </summary>
        public static AgentExecutionResult Failure(string errorCode, string errorMessage)
        {
            return Failure(errorCode, errorMessage, []);
        }

        /// <summary>
        /// Execution adimlariyla birlikte basarisiz agent çalistirma sonucu olusturur.
        /// </summary>
        public static AgentExecutionResult Failure(
            string errorCode,
            string errorMessage,
            IReadOnlyList<AgentExecutionStep> steps)
        {
            return new AgentExecutionResult(
                isSuccess: false,
                message: null,
                errorCode: errorCode,
                errorMessage: errorMessage,
                steps: steps);
        }
    }

    /// <summary>
    /// Agent çalistirmasi sirasinda olusan tek bir görünür execution adimini temsil eder.
    /// </summary>
    public sealed record AgentExecutionStep(
        int Index,
        AgentExecutionStepKind Kind,
        string? Content,
        string? ToolCallId,
        string? ToolName,
        string? ArgumentsJson,
        string? OutputJson,
        string? ErrorCode,
        string? ErrorMessage,
        AgentExecutionStepStatus Status,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt);

    /// <summary>
    /// Agent execution adim tiplerini belirtir.
    /// </summary>
    public enum AgentExecutionStepKind
    {
        /// <summary>
        /// Model tarafindan istenen tool çagrisini belirtir.
        /// </summary>
        ToolCall = 0,

        /// <summary>
        /// Model tarafindan üretilen final cevabi belirtir.
        /// </summary>
        FinalAnswer = 1,

        /// <summary>
        /// Agent veya tool çalismasi sirasinda olusan hatayi belirtir.
        /// </summary>
        Error = 2
    }

    /// <summary>
    /// Agent execution adiminin çalisma durumunu belirtir.
    /// </summary>
    public enum AgentExecutionStepStatus
    {
        /// <summary>
        /// Adimin çalismakta oldugunu belirtir.
        /// </summary>
        Running = 0,

        /// <summary>
        /// Adimin basariyla tamamlandigini belirtir.
        /// </summary>
        Completed = 1,

        /// <summary>
        /// Adimin hata ile tamamlandigini belirtir.
        /// </summary>
        Failed = 2
    }
}
