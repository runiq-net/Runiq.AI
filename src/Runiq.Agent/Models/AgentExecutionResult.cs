namespace Runiq.Agents
{
    /// <summary>
    /// Agent çalıştırma sonucunu final cevap, hata bilgisi ve görünür execution adımlarıyla temsil eder.
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
        /// Agent çalıştırma işleminin başarılı olup olmadığını belirtir.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Başarılı çalıştırma sonucunda modelden dönen final cevaptır.
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Başarısız çalıştırma durumunda hata kodudur.
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// Başarısız çalıştırma durumunda kullanıcıya veya geliştiriciye gösterilebilecek hata açıklamasıdır.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Agent çalıştırması sırasında oluşan görünür execution adımlarını döner.
        /// </summary>
        public IReadOnlyList<AgentExecutionStep> Steps { get; }

        /// <summary>
        /// Başarılı agent çalıştırma sonucu oluşturur.
        /// </summary>
        public static AgentExecutionResult Success(string message)
        {
            return Success(message, []);
        }

        /// <summary>
        /// Execution adımlarıyla birlikte başarılı agent çalıştırma sonucu oluşturur.
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
        /// Başarısız agent çalıştırma sonucu oluşturur.
        /// </summary>
        public static AgentExecutionResult Failure(string errorCode, string errorMessage)
        {
            return Failure(errorCode, errorMessage, []);
        }

        /// <summary>
        /// Execution adımlarıyla birlikte başarısız agent çalıştırma sonucu oluşturur.
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
    /// Agent çalıştırması sırasında oluşan tek bir görünür execution adımını temsil eder.
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
    /// Agent execution adım tiplerini belirtir.
    /// </summary>
    public enum AgentExecutionStepKind
    {
        /// <summary>
        /// Model tarafından istenen tool çağrısını belirtir.
        /// </summary>
        ToolCall = 0,

        /// <summary>
        /// Model tarafından üretilen final cevabı belirtir.
        /// </summary>
        FinalAnswer = 1,

        /// <summary>
        /// Agent veya tool çalışması sırasında oluşan hatayı belirtir.
        /// </summary>
        Error = 2
    }

    /// <summary>
    /// Agent execution adımının çalışma durumunu belirtir.
    /// </summary>
    public enum AgentExecutionStepStatus
    {
        /// <summary>
        /// Adımın çalışmakta olduğunu belirtir.
        /// </summary>
        Running = 0,

        /// <summary>
        /// Adımın başarıyla tamamlandığını belirtir.
        /// </summary>
        Completed = 1,

        /// <summary>
        /// Adımın hata ile tamamlandığını belirtir.
        /// </summary>
        Failed = 2
    }
}