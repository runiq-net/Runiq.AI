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
            IReadOnlyList<AgentExecutionStep> steps,
            AgentRagExecutionMetadata? rag,
            IReadOnlyList<AgentCitation>? citations = null,
            RagSearchBlocked? ragReadiness = null)
        {
            IsSuccess = isSuccess;
            Message = message;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Steps = steps;
            Rag = rag;
            Citations = citations?.ToArray() ?? [];
            RagReadiness = ragReadiness;
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
        /// Gets the structured RAG policy outcome, or null when RAG was not configured for the agent.
        /// </summary>
        public AgentRagExecutionMetadata? Rag { get; }
        /// <summary>Gets citations validated against selected context.</summary>
        public IReadOnlyList<AgentCitation> Citations { get; }

        /// <summary>Gets the structured readiness outcome when RAG execution was blocked before retrieval.</summary>
        public RagSearchBlocked? RagReadiness { get; }

        /// <summary>
        /// Basarili agent çalistirma sonucu olusturur.
        /// </summary>
        public static AgentExecutionResult Success(string message)
        {
            return Success(message, [], rag: null);
        }

        /// <summary>
        /// Execution adimlariyla birlikte basarili agent çalistirma sonucu olusturur.
        /// </summary>
        public static AgentExecutionResult Success(
            string message,
            IReadOnlyList<AgentExecutionStep> steps)
        {
            return Success(message, steps, rag: null);
        }

        /// <summary>
        /// Creates a successful agent execution result with execution steps and a structured RAG policy outcome.
        /// </summary>
        /// <param name="message">The final agent response.</param>
        /// <param name="steps">The visible execution steps.</param>
        /// <param name="rag">The RAG policy outcome, or null when RAG was not configured.</param>
        /// <returns>The successful agent execution result.</returns>
        public static AgentExecutionResult Success(
            string message,
            IReadOnlyList<AgentExecutionStep> steps,
            AgentRagExecutionMetadata? rag)
            => Success(message, steps, rag, []);

        /// <summary>Creates a successful result with validated citations.</summary>
        /// <param name="message">The final agent response.</param><param name="steps">The visible steps.</param>
        /// <param name="rag">The RAG outcome.</param><param name="citations">Validated citations.</param>
        /// <returns>The successful result.</returns>
        public static AgentExecutionResult Success(string message, IReadOnlyList<AgentExecutionStep> steps, AgentRagExecutionMetadata? rag, IReadOnlyList<AgentCitation> citations)
        {
            return new AgentExecutionResult(
                isSuccess: true,
                message: message,
                errorCode: null,
                errorMessage: null,
                steps: steps,
                rag: rag,
                citations: citations);
        }

        /// <summary>
        /// Basarisiz agent çalistirma sonucu olusturur.
        /// </summary>
        public static AgentExecutionResult Failure(string errorCode, string errorMessage)
        {
            return Failure(errorCode, errorMessage, [], rag: null);
        }

        /// <summary>
        /// Execution adimlariyla birlikte basarisiz agent çalistirma sonucu olusturur.
        /// </summary>
        public static AgentExecutionResult Failure(
            string errorCode,
            string errorMessage,
            IReadOnlyList<AgentExecutionStep> steps)
        {
            return Failure(errorCode, errorMessage, steps, rag: null);
        }

        /// <summary>
        /// Creates a failed agent execution result with execution steps and a structured RAG policy outcome.
        /// </summary>
        /// <param name="errorCode">The agent execution failure code.</param>
        /// <param name="errorMessage">The agent execution failure message.</param>
        /// <param name="steps">The visible execution steps.</param>
        /// <param name="rag">The RAG policy outcome, or null when RAG was not configured.</param>
        /// <returns>The failed agent execution result.</returns>
        public static AgentExecutionResult Failure(
            string errorCode,
            string errorMessage,
            IReadOnlyList<AgentExecutionStep> steps,
            AgentRagExecutionMetadata? rag)
        {
            return new AgentExecutionResult(
                isSuccess: false,
                message: null,
                errorCode: errorCode,
                errorMessage: errorMessage,
                steps: steps,
                rag: rag);
        }

        internal static AgentExecutionResult ReadinessFailure(string errorCode, string errorMessage,
            IReadOnlyList<AgentExecutionStep> steps, AgentRagExecutionMetadata? rag, RagSearchBlocked readiness) =>
            new(false, null, errorCode, errorMessage, steps, rag, ragReadiness: readiness);
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
