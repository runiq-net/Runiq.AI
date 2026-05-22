using System.Runtime.CompilerServices;
using Runiq.Agents.Configuration;
using Runiq.Agents.Models;
using Runiq.Agents.Tools;



namespace Runiq.Agents;

/// <summary>
/// Runiq runtime içinde çalıştırılabilir bir AI agent tanımını temsil eder.
/// </summary>
public class Agent
{
    private readonly List<AgentToolRegistration> tools = [];

    /// <summary>
    /// Agent'a code-first olarak eklenmiş tool kayıtlarını döner.
    /// </summary>
    public IReadOnlyList<AgentToolRegistration> Tools => tools;

    public string Id { get; }

    public string Name { get; }

    public string Instructions { get; }

    public string Model { get; }

    public string ProviderName => ModelReference.ProviderName;

    public string ModelName => ModelReference.ModelName;

    public string? ApiKey { get; }

    public string ReasoningEffort { get; }

    public string Verbosity { get; }

    public ProviderOptions? Provider { get; }

    public ModelReference ModelReference { get; }

    public Agent(
        string id,
        string name,
        string instructions,
        string model,
        string? apiKey = null,
        ProviderOptions? provider = null,
        string reasoningEffort = "minimal",
        string verbosity = "low")
    {
        Id = ValidateRequired(id, nameof(id));
        Name = ValidateRequired(name, nameof(name));
        Instructions = instructions ?? string.Empty;
        Model = ValidateRequired(model, nameof(model));
        ModelReference = ModelReference.Parse(Model);
        ApiKey = apiKey;
        Provider = provider;
        ReasoningEffort = ValidateReasoningEffort(reasoningEffort);
        Verbosity = ValidateVerbosity(verbosity);
    }

    /// <summary>
    /// Agent cevabını tek seferlik tamamlanmış çıktı olarak üretir.
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
    /// Agent cevabını parça parça üretir.
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
    /// Agent'a yeni bir tool kaydı ekler.
    /// </summary>
    /// <param name="tool">Eklenecek tool kaydıdır.</param>
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

    private static string ValidateReasoningEffort(string value)
    {
        var normalized = ValidateRequired(value, nameof(ReasoningEffort)).ToLowerInvariant();

        return normalized switch
        {
            "minimal" => normalized,
            "low" => normalized,
            "medium" => normalized,
            "high" => normalized,
            _ => throw new ArgumentException(
                "Reasoning effort must be one of: minimal, low, medium, high.",
                nameof(ReasoningEffort))
        };
    }

    private static string ValidateVerbosity(string value)
    {
        var normalized = ValidateRequired(value, nameof(Verbosity)).ToLowerInvariant();

        return normalized switch
        {
            "low" => normalized,
            "medium" => normalized,
            "high" => normalized,
            _ => throw new ArgumentException(
                "Verbosity must be one of: low, medium, high.",
                nameof(Verbosity))
        };
    }
}