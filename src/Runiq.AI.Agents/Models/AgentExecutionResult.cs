using System.Text.Json;

namespace Runiq.AI.Agents
{
    /// <summary>
    /// Represents a terminal execution result with text, explicit JSON, errors, and visible execution steps.
    /// </summary>
    public sealed class AgentExecutionResult
    {
        /// <summary>Gets explicitly supplied JSON with ownership independent of its original document.</summary>
        /// <remarks>Presence does not imply schema validation; response text is never parsed to infer JSON.</remarks>
        public JsonElement? StructuredOutput { get; }

        /// <summary>Gets the runtime run identifier, or null for a standalone factory result.</summary>
        public string? RunId { get; private init; }

        /// <summary>Gets the agent definition identifier, or null for a standalone factory result.</summary>
        public string? AgentId { get; private init; }

        /// <summary>Gets the UTC run start time, or null for a standalone factory result.</summary>
        public DateTimeOffset? StartedAt { get; private init; }

        /// <summary>Gets the UTC terminal transition time, or null for a standalone factory result.</summary>
        public DateTimeOffset? EndedAt { get; private init; }

        /// <summary>Gets the reserved provider session identifier; always null in this version.</summary>
        public string? ProviderSessionId => null;

        /// <summary>Gets the terminal state; caller cancellation is reported by an exception.</summary>
        public Runtime.AgentRunStatus Status { get; }

        internal AgentExecutionResult WithIdentity(string? runId, string? agentId,
            DateTimeOffset? startedAt = null, DateTimeOffset? endedAt = null) =>
            new(Status, Message, ErrorCode, ErrorMessage, Steps, Rag, Citations, RagReadiness, StructuredOutput)
            { RunId = runId, AgentId = agentId, StartedAt = startedAt, EndedAt = endedAt };

        private AgentExecutionResult(
            Runtime.AgentRunStatus status,
            string? message,
            string? errorCode,
            string? errorMessage,
            IReadOnlyList<AgentExecutionStep> steps,
            AgentRagExecutionMetadata? rag,
            IReadOnlyList<AgentCitation>? citations = null,
            RagSearchBlocked? ragReadiness = null,
            JsonElement? structuredOutput = null)
        {
            if (structuredOutput is { ValueKind: JsonValueKind.Undefined })
                throw new ArgumentException("Structured output must be a defined JSON value.", nameof(structuredOutput));
            StructuredOutput = structuredOutput?.Clone();
            Status = status;
            if (Status is not (Runtime.AgentRunStatus.Completed or Runtime.AgentRunStatus.Failed or Runtime.AgentRunStatus.Cancelled))
                throw new ArgumentException("A result must have a terminal status.", nameof(status));
            Message = message;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Steps = steps;
            Rag = rag;
            Citations = citations?.ToArray() ?? [];
            RagReadiness = ragReadiness;
        }

        /// <summary>
        /// Gets whether the terminal status is Completed.
        /// </summary>
        public bool IsSuccess => Status == Runtime.AgentRunStatus.Completed;

        /// <summary>
        /// Gets the successful response text, empty for JSON-only success and null for failure or cancellation.
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
                status: Runtime.AgentRunStatus.Completed,
                message: message,
                errorCode: null,
                errorMessage: null,
                steps: steps,
                rag: rag,
                citations: citations);
        }

        /// <summary>Creates a successful result retaining explicit JSON independently of its source document.</summary>
        /// <param name="message">The final response text; empty for a JSON-only response.</param>
        /// <param name="steps">The visible execution steps.</param>
        /// <param name="rag">The optional RAG policy outcome.</param>
        /// <param name="citations">The validated citations.</param>
        /// <param name="structuredOutput">Optional JSON to clone; no output is inferred from the message.</param>
        /// <returns>The successful result containing an owned JSON value.</returns>
        /// <exception cref="ArgumentException">The supplied JSON element is undefined.</exception>
        public static AgentExecutionResult Success(string message, IReadOnlyList<AgentExecutionStep> steps,
            AgentRagExecutionMetadata? rag, IReadOnlyList<AgentCitation> citations, JsonElement? structuredOutput) =>
            new(Runtime.AgentRunStatus.Completed, message, null, null, steps, rag, citations, structuredOutput: structuredOutput);

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
                status: Runtime.AgentRunStatus.Failed,
                message: null,
                errorCode: errorCode,
                errorMessage: errorMessage,
                steps: steps,
                rag: rag);
        }

        /// <summary>Creates a standalone cancelled result without inventing run identity or timestamps.</summary>
        /// <param name="steps">Optional visible steps produced before cancellation.</param>
        /// <param name="rag">Optional RAG policy information already available.</param>
        /// <returns>A Cancelled result with no successful message or structured output.</returns>
        /// <remarks>Runtime caller cancellation still throws AgentRunCanceledException; this factory does not change that behavior.</remarks>
        public static AgentExecutionResult Cancelled(IReadOnlyList<AgentExecutionStep>? steps = null,
            AgentRagExecutionMetadata? rag = null) =>
            new(Runtime.AgentRunStatus.Cancelled, null, "AgentExecutionCancelled", "Agent execution was cancelled.", steps ?? [], rag);

        internal static AgentExecutionResult ReadinessFailure(string errorCode, string errorMessage,
            IReadOnlyList<AgentExecutionStep> steps, AgentRagExecutionMetadata? rag, RagSearchBlocked readiness) =>
            new(Runtime.AgentRunStatus.Failed, null, errorCode, errorMessage, steps, rag, ragReadiness: readiness);
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
