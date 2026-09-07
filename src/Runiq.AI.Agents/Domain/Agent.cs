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
    /// Ajanin kullanacagi modeli provider/model biçiminde alir.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string Model => RequiredModel.Model;

    /// <summary>
    /// Model tanimindan çözümlenen provider adini alir.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string ProviderName => ModelReference.ProviderName;

    /// <summary>
    /// Model tanimindan çözümlenen model adini alir.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string ModelName => ModelReference.ModelName;

    /// <summary>
    /// Provider çagrilarinda kullanilacak opsiyonel API anahtarini alir.
    /// </summary>
    /// <remarks>Returns null when no model executor is selected.</remarks>
    public string? ApiKey => Executor?.Model?.ApiKey;

    /// <summary>
    /// Modelin yanit üretirken kullanacagi akil yürütme yogunlugunu alir.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string ReasoningEffort => RequiredModel.ReasoningEffort;

    /// <summary>
    /// Model yanitinin ayrinti seviyesini alir.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public string Verbosity => RequiredModel.Verbosity;

    /// <summary>
    /// Provider için tanimlanan opsiyonel çalisma zamani ayarlarini alir.
    /// </summary>
    /// <remarks>Returns null when no model executor is selected.</remarks>
    public ProviderOptions? Provider => Executor?.Model?.Provider;

    /// <summary>
    /// Agent RAG sorgulari için opsiyonel çalisma zamani ayarlarini alir.
    /// </summary>
    public AgentRagOptions? Rag { get; private set; }

    /// <summary>
    /// Provider ve model adini ayristirilmis biçimde temsil eden model referansini alir.
    /// </summary>
    /// <exception cref="InvalidOperationException">No model executor is selected.</exception>
    public ModelReference ModelReference => RequiredModel.ModelReference;

    /// <summary>
    /// Yeni bir agent tanimi olusturur.
    /// </summary>
    /// <param name="id">Ajanin sistem içindeki benzersiz kimligidir.</param>
    /// <param name="name">Ajanin gösterilecek adidir.</param>
    /// <param name="instructions">Ajanin model çagrilarinda kullanilacak sistem yönergeleridir.</param>
    /// <param name="model">Kullanilacak modelin provider/model biçimindeki adidir.</param>
    /// <param name="apiKey">Provider çagrilarinda kullanilacak opsiyonel API anahtaridir.</param>
    /// <param name="provider">Provider için opsiyonel çalisma zamani ayarlaridir.</param>
    /// <param name="reasoningEffort">Modelin akil yürütme yogunlugudur.</param>
    /// <param name="verbosity">Model yanitinin ayrinti seviyesidir.</param>
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

    /// <summary>Selects the Codex execution preference. Codex execution is not implemented.</summary>
    /// <remarks>
    /// Does not require a CLI installation and does not start processes, send network requests,
    /// or authenticate. No timeout, sandbox, or session behavior is configured.
    /// </remarks>
    /// <returns>The same agent instance.</returns>
    /// <exception cref="InvalidOperationException">An executor has already been selected.</exception>
    public Agent UseCodex() => SelectExecutor(() => new AgentExecutorConfiguration(AgentExecutorKind.Codex));

    /// <summary>Selects the Claude execution preference. Claude execution is not implemented.</summary>
    /// <remarks>
    /// Does not require a CLI installation and does not start processes, send network requests,
    /// or authenticate. No timeout, sandbox, or session behavior is configured.
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
