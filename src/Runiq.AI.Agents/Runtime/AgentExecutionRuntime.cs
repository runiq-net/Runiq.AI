using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Validation;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Reranking;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Owns execution lifecycle and dispatches configured agents to their executor.
/// </summary>
/// <remarks>
/// Each invocation creates a fresh run identity. Streams start when enumerated.
/// Both execution APIs throw <see cref="AgentRunCanceledException"/> on caller cancellation;
/// no terminal event or result is returned for a cancelled run. Unexpected executor failures
/// and empty responses are reported as failed events or results. Disposing an unfinished
/// stream cancels its run and releases the executor.
/// </remarks>
public sealed class AgentExecutionRuntime
{
    private readonly IEnumerable<Agent> agents;
    private readonly AgentToolInvoker toolInvoker;
    private readonly AgentExecutorResolver executorResolver;
    private readonly ILogger<AgentExecutionRuntime> logger = NullLogger<AgentExecutionRuntime>.Instance;

    /// <summary>
    /// Initializes the runtime with two provider-neutral clients for compatibility with existing manual construction.
    /// </summary>
    /// <param name="agents">The registered agents available to the runtime.</param>
    /// <param name="openAIResponsesClient">The client used for native OpenAI requests.</param>
    /// <param name="openAICompatibleClient">The client used for OpenAI-compatible and Ollama requests.</param>
    /// <param name="toolInvoker">The agent-owned tool invoker.</param>
    /// <param name="ragRetriever">Optional RAG retriever.</param>
    /// <param name="ragReranker">Optional provider-neutral RAG reranker.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        IChatClient openAIResponsesClient,
        IChatClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IRagRetriever? ragRetriever = null,
        IRagReranker? ragReranker = null)
        : this(
            agents,
            new FixedChatClientResolver(openAIResponsesClient, openAICompatibleClient),
            toolInvoker,
            ragRetriever,
            ragReranker)
    {
    }


    /// <summary>
    /// Initializes the agent runtime with provider-neutral model resolution and agent-owned orchestration services.
    /// </summary>
    /// <param name="agents">Runtime tarafindan Ã§alistirilabilecek kayitli agent koleksiyonudur.</param>
    /// <param name="chatClientResolver">Resolves the shared chat client for each agent model.</param>
    /// <param name="toolInvoker">Agent tool Ã§agrilarini Ã§alistiran invoker Ã¶rnegidir.</param>
    /// <param name="ragRetriever">Agent RAG sorgularini Ã§alistiracak opsiyonel retriever servisidir.</param>
    /// <param name="ragReranker">Optional provider-neutral RAG reranker.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        IChatClientResolver chatClientResolver,
        AgentToolInvoker toolInvoker,
        IRagRetriever? ragRetriever = null,
        IRagReranker? ragReranker = null)
    {
        this.agents = agents ?? throw new ArgumentNullException(nameof(agents));
        ArgumentNullException.ThrowIfNull(chatClientResolver);
        this.toolInvoker = toolInvoker ?? throw new ArgumentNullException(nameof(toolInvoker));
        var observability = new RagObservabilityProjection(Options.Create(new RagObservabilityOptions()), null, null,
            NullLogger<RagObservabilityProjection>.Instance);
        executorResolver = new([new ModelAgentExecutor(chatClientResolver, observability, ragRetriever,
            null, null, ragReranker)]);
    }

    /// <summary>Initializes the hosted runtime with scoped executor resolution and server-side diagnostics.</summary>
    /// <param name="agents">The configured agent definitions available to this runtime.</param>
    /// <param name="executorResolver">The executor resolver supplied by the host container.</param>
    /// <param name="toolInvoker">The tool invoker used unless a streaming call supplies an override.</param>
    /// <param name="logger">The server logger that records original execution and cleanup exceptions.</param>
    internal AgentExecutionRuntime(IEnumerable<Agent> agents, AgentExecutorResolver executorResolver,
        AgentToolInvoker toolInvoker, ILogger<AgentExecutionRuntime> logger)
    {
        this.agents = agents ?? throw new ArgumentNullException(nameof(agents));
        this.executorResolver = executorResolver ?? throw new ArgumentNullException(nameof(executorResolver));
        this.toolInvoker = toolInvoker ?? throw new ArgumentNullException(nameof(toolInvoker));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Agent cevabini agent kimligine gÃ¶re tek seferlik sonuÃ§ olarak Ã¼retir.
    /// </summary>
    /// <param name="agentId">Ã‡alistirilacak agent kimligidir.</param>
    /// <param name="input">Agent'a gÃ¶nderilecek kullanici girdisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    public async Task<AgentExecutionResult> ExecuteAsync(
        string agentId,
        string input,
        CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        return await ExecuteAgentAsync(
            agent,
            new AgentQuery(input),
            cancellationToken, agentId);
    }

    /// <summary>
    /// Agent cevabini agent kimligine gÃ¶re runtime query bilgisiyle tek seferlik sonuÃ§ olarak Ã¼retir.
    /// </summary>
    /// <param name="agentId">Ã‡alistirilacak agent kimligidir.</param>
    /// <param name="query">Agent'a gÃ¶nderilecek runtime query bilgisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    public async Task<AgentExecutionResult> ExecuteAsync(
        string agentId,
        AgentQuery query,
        CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        return await ExecuteAgentAsync(
            agent,
            query,
            cancellationToken, agentId);
    }

    /// <summary>
    /// Kayit listesine bagli olmayan geÃ§ici bir agent tanimiyla tek seferlik sonuÃ§ Ã¼retir.
    /// </summary>
    /// <param name="agent">Ã‡alistirilacak agent tanimidir.</param>
    /// <param name="input">Agent'a gÃ¶nderilecek kullanici girdisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    public Task<AgentExecutionResult> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAgentAsync(
            agent,
            new AgentQuery(input),
            cancellationToken);
    }

    /// <summary>
    /// Kayit listesine bagli olmayan geÃ§ici bir agent tanimiyla runtime query bilgisiyle tek seferlik sonuÃ§ Ã¼retir.
    /// </summary>
    /// <param name="agent">Ã‡alistirilacak agent tanimidir.</param>
    /// <param name="query">Agent'a gÃ¶nderilecek runtime query bilgisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    public Task<AgentExecutionResult> ExecuteAsync(
        Agent agent,
        AgentQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAgentAsync(
            agent,
            query,
            cancellationToken);
    }

    /// <summary>
    /// Agent cevabini agent kimligine gÃ¶re event stream olarak Ã¼retir.
    /// </summary>
    /// <param name="agentId">Ã‡alistirilacak agent kimligidir.</param>
    /// <param name="input">Agent'a gÃ¶nderilecek kullanici girdisidir.</param>
    /// <param name="toolInvoker">Varsa bu Ã§agri iÃ§in kullanilacak tool invoker Ã¶rnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alismasi sirasinda Ã¼retilen olay stream'idir.</returns>
    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteStreamAsync(
        string agentId,
        string input,
        AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        await foreach (var executionEvent in ExecuteAgentStreamAsync(
                           agent,
                           new AgentQuery(input),
                           toolInvoker ?? this.toolInvoker,
                           cancellationToken, agentId))
        {
            yield return executionEvent;
        }
    }

    /// <summary>
    /// Agent cevabini agent kimligine gÃ¶re runtime query bilgisiyle event stream olarak Ã¼retir.
    /// </summary>
    /// <param name="agentId">Ã‡alistirilacak agent kimligidir.</param>
    /// <param name="query">Agent'a gÃ¶nderilecek runtime query bilgisidir.</param>
    /// <param name="toolInvoker">Varsa bu Ã§agri iÃ§in kullanilacak tool invoker Ã¶rnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alismasi sirasinda Ã¼retilen olay stream'idir.</returns>
    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteStreamAsync(
        string agentId,
        AgentQuery query,
        AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        await foreach (var executionEvent in ExecuteAgentStreamAsync(
                           agent,
                           query,
                           toolInvoker ?? this.toolInvoker,
                           cancellationToken, agentId))
        {
            yield return executionEvent;
        }
    }

    /// <summary>
    /// Kayitli agent koleksiyonu iÃ§inde agent kimligine gÃ¶re arama yapar.
    /// </summary>
    /// <param name="agentId">Aranacak agent kimligidir.</param>
    /// <returns>Bulunan agent tanimidir; bulunamazsa null dÃ¶ner.</returns>
    private Agent? FindAgent(string agentId)
    {
        return agents.FirstOrDefault(agent =>
            string.Equals(agent.Id, agentId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<AgentExecutionResult> ExecuteAgentAsync(Agent? agent, AgentQuery query,
        CancellationToken cancellationToken, string? requestedAgentId = null)
    {
        var builder = new AgentExecutionResultBuilder();
        await foreach (var executionEvent in ExecuteAgentStreamAsync(agent, query, toolInvoker,
                           cancellationToken, requestedAgentId))
            builder.Apply(executionEvent);
        return builder.Build();
    }

    private async IAsyncEnumerable<AgentExecutionEvent> ExecuteAgentStreamAsync(
        Agent? agent, AgentQuery query, AgentToolInvoker invocationToolInvoker,
        [EnumeratorCancellation] CancellationToken cancellationToken, string? requestedAgentId = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (requestedAgentId is null) ArgumentNullException.ThrowIfNull(agent);
        var run = new AgentRunContext(agent?.Id ?? requestedAgentId!);
        IAsyncEnumerator<AgentExecutionEvent>? enumerator = null;
        var disposed = false;
        var message = new StringBuilder();
        try
        {
            ThrowIfCancelled(run, cancellationToken);
            AgentExecutionEvent? initialFailure = agent is null
                ? AgentExecutionEvent.Failed($"Agent '{requestedAgentId}' was not found.", "AgentNotFound")
                : string.IsNullOrWhiteSpace(query.Message)
                    ? AgentExecutionEvent.Failed("Agent input cannot be empty.", "InputRequired")
                    : null;
            IAgentExecutor? executor = null;
            if (initialFailure is null)
            {
                var executorFailure = AgentValidator.ValidateExecutor(agent!);
                if (executorFailure is not null)
                    initialFailure = AgentExecutionEvent.Failed(executorFailure.ErrorMessage!, executorFailure.ErrorCode);
                else
                {
                    executor = executorResolver.Resolve(agent!.Executor!.Kind);
                    if (executor is null)
                        initialFailure = AgentExecutionEvent.Failed(
                            $"Agent '{agent.Id}' selects {agent.Executor.Kind}, but no implementation is registered for this executor.",
                            "AgentExecutorNotSupported");
                }
            }
            if (initialFailure is not null)
            {
                ThrowIfCancelled(run, cancellationToken);
                run.Finish(AgentRunStatus.Failed);
                yield return Stamp(initialFailure, run);
                yield break;
            }

            var request = new AgentExecutionRequest(agent!, query);
            while (true)
            {
                AgentExecutionEvent current;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    enumerator ??= executor!.ExecuteAsync(request, run, invocationToolInvoker, cancellationToken)
                        .GetAsyncEnumerator(cancellationToken);
                    current = await enumerator.MoveNextAsync()
                        ? enumerator.Current
                        : AgentExecutionEvent.Failed("The executor ended without a terminal event.", "AgentExecutionProtocolError");
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
                {
                    run.Finish(AgentRunStatus.Cancelled);
                    throw new AgentRunCanceledException(run, cancellationToken, exception);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Agent execution failed for run {RunId} and agent {AgentId}.",
                        run.RunId, run.AgentId);
                    current = AgentExecutionEvent.Failed("Agent execution failed.", "AgentExecutionFailed");
                }
                ThrowIfCancelled(run, cancellationToken);
                if (current.Kind == AgentExecutionEventKind.AssistantDelta) message.Append(current.Content);
                if (current.Kind == AgentExecutionEventKind.Completed && current.StructuredOutput is null &&
                    string.IsNullOrWhiteSpace(message.ToString()))
                    current = AgentExecutionEvent.Failed("Agent execution completed without producing a message.",
                        "AgentExecutionEmptyMessage", current.Rag);

                if (current.Status != AgentRunStatus.Running)
                {
                    // Dispose before publishing a terminal outcome so cleanup cannot fail after completion.
                    disposed = true;
                    try
                    {
                        if (enumerator is not null) await enumerator.DisposeAsync();
                    }
                    catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
                    {
                        logger.LogWarning(exception, "Agent executor cleanup was cancelled for run {RunId} and agent {AgentId}.",
                            run.RunId, run.AgentId);
                        run.Finish(AgentRunStatus.Cancelled);
                        throw new AgentRunCanceledException(run, cancellationToken, exception);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Agent executor cleanup failed for run {RunId} and agent {AgentId}.",
                            run.RunId, run.AgentId);
                        current = AgentExecutionEvent.Failed("Agent executor cleanup failed.", "AgentExecutionFailed", current.Rag);
                    }
                    ThrowIfCancelled(run, cancellationToken);
                    if (current.Kind == AgentExecutionEventKind.Completed)
                        current = current with { Message = message.ToString() };
                    else if (current.Kind == AgentExecutionEventKind.Failed && current.ErrorCode is null)
                        current = AgentExecutionEvent.Failed(
                            current.ErrorMessage ?? current.Content ?? "Agent execution failed.",
                            "AgentExecutionFailed", current.Rag);
                    run.Finish(current.Status);
                    yield return Stamp(current, run);
                    yield break;
                }
                yield return Stamp(current, run);
            }
        }
        finally
        {
            // An abandoned stream is cancelled even though its consumer cannot receive a final event.
            run.Finish(AgentRunStatus.Cancelled);
            if (enumerator is not null && !disposed)
            {
                try { await enumerator.DisposeAsync(); }
                catch (Exception exception) when (run.Status == AgentRunStatus.Cancelled)
                {
                    // Preserve cancellation instead of replacing it with a secondary cleanup failure.
                    logger.LogWarning(exception, "Agent executor cleanup failed for cancelled run {RunId} and agent {AgentId}.",
                        run.RunId, run.AgentId);
                }
            }
        }
    }

    private static AgentExecutionEvent Stamp(AgentExecutionEvent executionEvent, AgentRunContext run) =>
        executionEvent with
        {
            RunId = run.RunId,
            AgentId = run.AgentId,
            StartedAt = run.StartedAt,
            EndedAt = run.EndedAt,
            SequenceNumber = run.NextEventSequence(),
            Timestamp = DateTimeOffset.UtcNow
        };

    private static void ThrowIfCancelled(AgentRunContext run, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested) return;
        run.Finish(AgentRunStatus.Cancelled);
        throw new AgentRunCanceledException(run, cancellationToken);
    }

    /// <summary>
    /// Resolves manually supplied Core clients while preserving the runtime's provider-neutral boundary.
    /// </summary>
    private sealed class FixedChatClientResolver : IChatClientResolver
    {
        private readonly IChatClient responsesClient;
        private readonly IChatClient compatibleClient;

        /// <summary>
        /// Initializes a resolver for the two supported chat protocol families.
        /// </summary>
        public FixedChatClientResolver(IChatClient responsesClient, IChatClient compatibleClient)
        {
            this.responsesClient = responsesClient ?? throw new ArgumentNullException(nameof(responsesClient));
            this.compatibleClient = compatibleClient ?? throw new ArgumentNullException(nameof(compatibleClient));
        }

        /// <inheritdoc />
        public IChatClient Resolve(ChatRequest request) =>
            string.Equals(request.Model.ProviderName, "openai", StringComparison.OrdinalIgnoreCase)
                ? responsesClient
                : compatibleClient;
    }


}
