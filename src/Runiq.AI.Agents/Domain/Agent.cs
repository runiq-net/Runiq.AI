using System.Runtime.CompilerServices;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Models;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents;

/// <summary>
/// Runiq runtime içinde çalistirilabilir bir AI agent tanimini temsil eder.
/// </summary>
public class Agent
{
    private readonly List<AgentToolRegistration> tools = [];
    private AgentExecutorConfiguration? executor;

    /// <summary>Gets the selected executor, or null until a Use method selects one.</summary>
    public AgentExecutorConfiguration? Executor => Volatile.Read(ref executor);

    private AgentModelConfiguration RequiredModel => Executor?.Model
        ?? throw new InvalidOperationException($"Agent '{Id}' does not have a model executor.");

    /// <summary>
    /// Agent'a code-first olarak eklenmis tool kayitlarini döner.
    /// </summary>
    public IReadOnlyList<AgentToolRegistration> Tools => tools;

    /// <summary>
    /// Ajanin sistem içindeki benzersiz kimligini alir.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Ajanin kullanici arayüzünde veya metadata çiktilarinda gösterilecek adini alir.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Ajanin model çagrilarinda kullanilacak sistem yönergelerini alir.
    /// </summary>
    public string Instructions { get; }

    /// <summary>
    /// Gets the configured, trimmed provider/model identifier for the selected model executor.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string Model => RequiredModel.Model;

    /// <summary>
    /// Gets the normalized provider name parsed from the selected model reference.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string ProviderName => ModelReference.ProviderName;

    /// <summary>
    /// Gets the model name parsed from the selected model reference.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string ModelName => ModelReference.ModelName;

    /// <summary>
    /// Gets the supplied API key, or null when absent or no model executor is selected.
    /// </summary>
    /// <remarks>Returns null when no model executor is selected.</remarks>
    public string? ApiKey => Executor?.Model?.ApiKey;

    /// <summary>
    /// Gets the normalized reasoning effort for the selected model executor.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string ReasoningEffort => RequiredModel.ReasoningEffort;

    /// <summary>
    /// Gets the normalized verbosity for the selected model executor.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string Verbosity => RequiredModel.Verbosity;

    /// <summary>
    /// Gets the original provider options instance, or null when absent or no model executor is selected.
    /// </summary>
    /// <remarks>Returns null when no model executor is selected.</remarks>
    public ProviderOptions? Provider => Executor?.Model?.Provider;

    /// <summary>
    /// Agent RAG sorgulari için opsiyonel çalisma zamani ayarlarini alir.
    /// </summary>
    public AgentRagOptions? Rag { get; private set; }

    /// <summary>
    /// Gets the parsed model reference owned by the selected model configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public ModelReference ModelReference => RequiredModel.ModelReference;

    /// <summary>
    /// Creates an agent and selects model execution through the same configuration path as UseModel.
    /// </summary>
    /// <param name="id">The non-empty identifier, trimmed before storage.</param>
    /// <param name="name">The non-empty display name, trimmed before storage.</param>
    /// <param name="instructions">Instructions preserved verbatim; null becomes an empty string.</param>
    /// <param name="model">The supported provider/model reference.</param>
    /// <param name="apiKey">The optional API key, retained without authentication.</param>
    /// <param name="provider">Optional provider settings retained by reference and validated at registration.</param>
    /// <param name="reasoningEffort">Minimal, low, medium, or high; normalized to lowercase.</param>
    /// <param name="verbosity">Low, medium, or high; normalized to lowercase.</param>
    /// <remarks>Remains supported for positional, named and derived-class base calls. Performs no external I/O.</remarks>
    /// <exception cref="ArgumentException">Identity, model reference or generation settings are invalid.</exception>
    public Agent(
        string id,
        string name,
        string instructions,
        string model,
        string? apiKey = null,
        ProviderOptions? provider = null,
        string reasoningEffort = "minimal",
        string verbosity = "low")
        : this(id, name, instructions)
    {
        UseModel(model, apiKey, provider, reasoningEffort, verbosity);
    }

    /// <summary>Creates an agent definition whose executor must be selected before registration or execution.</summary>
    /// <param name="id">The non-empty unique agent identifier.</param>
    /// <param name="name">The non-empty display name.</param>
    /// <param name="instructions">The agent instructions; null is normalized to an empty string.</param>
    /// <remarks>Trims the identifier and display name, preserves instruction whitespace, and performs no external I/O.</remarks>
    /// <exception cref="ArgumentException">The identifier or name is empty.</exception>
    public Agent(string id, string name, string instructions)
    {
        Id = ValidateRequired(id, nameof(id));
        Name = ValidateRequired(name, nameof(name));
        Instructions = instructions ?? string.Empty;
    }

    /// <summary>Selects model execution and validates the model-specific configuration.</summary>
    /// <remarks>
    /// Uses the existing Core model-reference parser. This method only configures the definition;
    /// it does not send network requests, start processes, or authenticate the supplied API key.
    /// An existing selection is rejected before new model options are validated. A failed first
    /// validation leaves selection available. Atomic selection does not make other configuration thread-safe.
    /// </remarks>
    /// <param name="model">The supported provider/model reference.</param>
    /// <param name="apiKey">The optional provider API key.</param>
    /// <param name="provider">The optional provider runtime settings.</param>
    /// <param name="reasoningEffort">One of minimal, low, medium, or high.</param>
    /// <param name="verbosity">One of low, medium, or high.</param>
    /// <returns>The same agent instance.</returns>
    /// <exception cref="ArgumentException">A model or generation setting is invalid.</exception>
    /// <exception cref="InvalidOperationException">An executor has already been selected.</exception>
    public Agent UseModel(string model, string? apiKey = null, ProviderOptions? provider = null,
        string reasoningEffort = "minimal", string verbosity = "low") =>
        SelectExecutor(() => new AgentExecutorConfiguration(AgentExecutorKind.Model,
            new AgentModelConfiguration(model, apiKey, provider, reasoningEffort, verbosity)));

    /// <summary>Selects Codex execution, which requires a host-registered executor implementation.</summary>
    /// <remarks>
    /// Does not require a CLI installation and does not start processes, send network requests,
    /// or authenticate. No timeout, sandbox, or session behavior is configured.
    /// This records an execution preference only; no built-in Codex adapter or tool bridge is supplied.
    /// </remarks>
    /// <returns>The same agent instance.</returns>
    /// <exception cref="InvalidOperationException">An executor has already been selected.</exception>
    public Agent UseCodex() => SelectExecutor(() => new AgentExecutorConfiguration(AgentExecutorKind.Codex));

    /// <summary>Selects Claude execution, which requires a host-registered executor implementation.</summary>
    /// <remarks>
    /// Does not require a CLI installation and does not start processes, send network requests,
    /// or authenticate. No timeout, sandbox, or session behavior is configured.
    /// This records an execution preference only; no built-in Claude adapter or tool bridge is supplied.
    /// </remarks>
    /// <returns>The same agent instance.</returns>
    /// <exception cref="InvalidOperationException">An executor has already been selected.</exception>
    public Agent UseClaude() => SelectExecutor(() => new AgentExecutorConfiguration(AgentExecutorKind.Claude));

    private Agent SelectExecutor(Func<AgentExecutorConfiguration> createConfiguration)
    {
        if (Executor is { } selected)
        {
            throw DuplicateExecutor(selected);
        }

        AgentExecutorConfiguration configuration;
        try
        {
            configuration = createConfiguration();
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                $"Agent '{Id}' has invalid executor configuration. {exception.Message}",
                exception.ParamName, exception);
        }

        if (Interlocked.CompareExchange(ref executor, configuration, null) is { } existing)
        {
            throw DuplicateExecutor(existing);
        }

        return this;
    }

    private InvalidOperationException DuplicateExecutor(AgentExecutorConfiguration selected) =>
        new($"Agent '{Id}' already has an executor ({selected.Kind}). Only one executor can be selected.");

    /// <summary>
    /// Agent cevabini tek seferlik tamamlanmis çikti olarak üretir.
    /// </summary>
    public Task<AgentExecutionResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AgentExecutionResult.Failure(
            errorCode: "DirectAgentExecutionNotSupported",
            errorMessage:
            "Direct Agent.ExecuteAsync is no longer responsible for provider execution. " +
            "Use AgentExecutionRuntime.ExecuteAsync through dependency injection."));
    }

    /// <summary>
    /// Agent cevabini parça parça üretir.
    /// </summary>
    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteStreamAsync(
        Agent agent,
        string input,
        AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        yield return AgentExecutionEvent.Failed(
            "Direct Agent.ExecuteStreamAsync is no longer responsible for provider execution. " +
            "Use AgentExecutionRuntime.ExecuteStreamAsync through dependency injection.",
            "DirectAgentExecutionNotSupported");
    }

    /// <summary>
    /// Configures framework-owned retrieval before the agent's initial model invocation.
    /// </summary>
    /// <param name="configure">Configures the index, execution mode, no-context behavior, and acceptance threshold.</param>
    /// <returns>The same agent instance so calls can be chained.</returns>
    public Agent UseRag(Action<AgentRagOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new AgentRagOptions();
        configure(options);

        AgentRagPolicyValidator.Validate(options, requireIndex: options.Enabled);

        if (options.Enabled)
        {
            options.IndexName = ValidateRequired(options.IndexName!, nameof(options.IndexName));
        }

        Rag = options;

        return this;
    }

    /// <summary>
    /// Agent'a yeni bir tool kaydi ekler.
    /// </summary>
    /// <param name="tool">Eklenecek tool kaydidir.</param>
    internal void AddToolRegistration(AgentToolRegistration tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (tools.Any(existing =>
                existing.Name.Equals(tool.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Agent '{Id}' already has a tool named '{tool.Name}'.");
        }

        tools.Add(tool);
    }

    private static string ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        return value.Trim();
    }

}
