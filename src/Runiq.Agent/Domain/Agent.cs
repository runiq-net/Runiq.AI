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
    private readonly List<string> contextSpaceIds = [];

    /// <summary>
    /// Agent'a code-first olarak eklenmiş tool kayıtlarını döner.
    /// </summary>
    public IReadOnlyList<AgentToolRegistration> Tools => tools;

    /// <summary>
    /// Ajanın sistem içindeki benzersiz kimliğini alır.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Ajanın kullanıcı arayüzünde veya metadata çıktılarında gösterilecek adını alır.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Ajanın model çağrılarında kullanılacak sistem yönergelerini alır.
    /// </summary>
    public string Instructions { get; }

    /// <summary>
    /// Ajanın kullanacağı modeli provider/model biçiminde alır.
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// Model tanımından çözümlenen provider adını alır.
    /// </summary>
    public string ProviderName => ModelReference.ProviderName;

    /// <summary>
    /// Model tanımından çözümlenen model adını alır.
    /// </summary>
    public string ModelName => ModelReference.ModelName;

    /// <summary>
    /// Provider çağrılarında kullanılacak opsiyonel API anahtarını alır.
    /// </summary>
    public string? ApiKey { get; }

    /// <summary>
    /// Modelin yanıt üretirken kullanacağı akıl yürütme yoğunluğunu alır.
    /// </summary>
    public string ReasoningEffort { get; }

    /// <summary>
    /// Model yanıtının ayrıntı seviyesini alır.
    /// </summary>
    public string Verbosity { get; }

    /// <summary>
    /// Provider için tanımlanan opsiyonel çalışma zamanı ayarlarını alır.
    /// </summary>
    public ProviderOptions? Provider { get; }

    /// <summary>
    /// Agent RAG sorguları için opsiyonel çalışma zamanı ayarlarını alır.
    /// </summary>
    public AgentRagOptions? Rag { get; private set; }

    /// <summary>
    /// Provider ve model adını ayrıştırılmış biçimde temsil eden model referansını alır.
    /// </summary>
    public ModelReference ModelReference { get; }

    /// <summary>
    /// Agent'a bağlanmış context space teknik kimliklerini döner.
    /// </summary>
    public IReadOnlyList<string> ContextSpaceIds => contextSpaceIds;

    /// <summary>
    /// Yeni bir agent tanımı oluşturur.
    /// </summary>
    /// <param name="id">Ajanın sistem içindeki benzersiz kimliğidir.</param>
    /// <param name="name">Ajanın gösterilecek adıdır.</param>
    /// <param name="instructions">Ajanın model çağrılarında kullanılacak sistem yönergeleridir.</param>
    /// <param name="model">Kullanılacak modelin provider/model biçimindeki adıdır.</param>
    /// <param name="apiKey">Provider çağrılarında kullanılacak opsiyonel API anahtarıdır.</param>
    /// <param name="provider">Provider için opsiyonel çalışma zamanı ayarlarıdır.</param>
    /// <param name="reasoningEffort">Modelin akıl yürütme yoğunluğudur.</param>
    /// <param name="verbosity">Model yanıtının ayrıntı seviyesidir.</param>
    public Agent(
        string id,
        string name,
        string instructions,
        string model,
        string? apiKey = null,
        ProviderOptions? provider = null,
        string reasoningEffort = "minimal",
        string verbosity = "low")
        : this(
            id,
            name,
            instructions,
            model,
            apiKey,
            provider,
            reasoningEffort,
            verbosity,
            rag: null)
    {
    }

    /// <summary>
    /// Yeni bir agent tanımı ve RAG çalışma zamanı ayarları oluşturur.
    /// </summary>
    /// <param name="id">Ajanın sistem içindeki benzersiz kimliğidir.</param>
    /// <param name="name">Ajanın gösterilecek adıdır.</param>
    /// <param name="instructions">Ajanın model çağrılarında kullanılacak sistem yönergeleridir.</param>
    /// <param name="model">Kullanılacak modelin provider/model biçimindeki adıdır.</param>
    /// <param name="rag">Agent RAG sorguları için opsiyonel çalışma zamanı ayarlarıdır.</param>
    /// <param name="apiKey">Provider çağrılarında kullanılacak opsiyonel API anahtarıdır.</param>
    /// <param name="provider">Provider için opsiyonel çalışma zamanı ayarlarıdır.</param>
    /// <param name="reasoningEffort">Modelin akıl yürütme yoğunluğudur.</param>
    /// <param name="verbosity">Model yanıtının ayrıntı seviyesidir.</param>
    public Agent(
        string id,
        string name,
        string instructions,
        string model,
        AgentRagOptions? rag,
        string? apiKey = null,
        ProviderOptions? provider = null,
        string reasoningEffort = "minimal",
        string verbosity = "low")
        : this(
            id,
            name,
            instructions,
            model,
            apiKey,
            provider,
            reasoningEffort,
            verbosity,
            rag)
    {
    }

    /// <summary>
    /// Yeni bir agent tanımı oluşturur.
    /// </summary>
    /// <param name="id">Ajanın sistem içindeki benzersiz kimliğidir.</param>
    /// <param name="name">Ajanın gösterilecek adıdır.</param>
    /// <param name="instructions">Ajanın model çağrılarında kullanılacak sistem yönergeleridir.</param>
    /// <param name="model">Kullanılacak modelin provider/model biçimindeki adıdır.</param>
    /// <param name="apiKey">Provider çağrılarında kullanılacak opsiyonel API anahtarıdır.</param>
    /// <param name="provider">Provider için opsiyonel çalışma zamanı ayarlarıdır.</param>
    /// <param name="reasoningEffort">Modelin akıl yürütme yoğunluğudur.</param>
    /// <param name="verbosity">Model yanıtının ayrıntı seviyesidir.</param>
    /// <param name="rag">Agent RAG sorguları için opsiyonel çalışma zamanı ayarlarıdır.</param>
    public Agent(
        string id,
        string name,
        string instructions,
        string model,
        string? apiKey,
        ProviderOptions? provider,
        string reasoningEffort,
        string verbosity,
        AgentRagOptions? rag)
    {
        Id = ValidateRequired(id, nameof(id));
        Name = ValidateRequired(name, nameof(name));
        Instructions = instructions ?? string.Empty;
        Model = ValidateRequired(model, nameof(model));
        ModelReference = ModelReference.Parse(Model);
        ApiKey = apiKey;
        Provider = provider;
        Rag = rag;
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
    /// Agent'a kullanılacak bir context space bağlantısı ekler.
    /// </summary>
    /// <param name="contextSpaceId">Bağlanacak context space teknik kimliğidir.</param>
    /// <returns>Akıcı yapılandırma için mevcut agent örneği.</returns>
    public Agent UseContextSpace(string contextSpaceId)
    {
        var normalizedContextSpaceId = ValidateRequired(contextSpaceId, nameof(contextSpaceId));

        if (contextSpaceIds.Any(existing =>
                string.Equals(existing, normalizedContextSpaceId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Agent '{Id}' already uses context space '{normalizedContextSpaceId}'.");
        }

        contextSpaceIds.Add(normalizedContextSpaceId);

        return this;
    }

    /// <summary>
    /// Agent RAG sorguları için kullanılacak vector index adını yapılandırır.
    /// </summary>
    /// <param name="indexName">Kullanılacak vector index adıdır.</param>
    /// <returns>Akıcı yapılandırma için mevcut agent örneği.</returns>
    public Agent UseRagIndex(string indexName)
    {
        Rag = new AgentRagOptions
        {
            IndexName = ValidateRequired(indexName, nameof(indexName)),
        };

        return this;
    }

    /// <summary>
    /// Associates the agent with a Vector Query Tool definition by configuring the vector store name, index
    /// name, and optional embedding model identifier used by agent RAG queries. The values are carried as
    /// configuration only: this method does not resolve a provider, select a vector store, or invoke the tool.
    /// It reuses the existing <see cref="AgentRagOptions"/> surface and, like <see cref="UseRagIndex"/>,
    /// replaces any previously configured RAG options.
    /// </summary>
    /// <param name="vectorStoreName">The vector store name to associate with the agent.</param>
    /// <param name="indexName">The vector index name used by agent RAG queries.</param>
    /// <param name="embeddingModel">
    /// The optional embedding model identifier. A null or whitespace value associates no embedding model.
    /// </param>
    /// <returns>The same agent instance so calls can be chained.</returns>
    public Agent UseVectorQueryTool(
        string vectorStoreName,
        string indexName,
        string? embeddingModel = null)
    {
        var normalizedVectorStoreName = ValidateRequired(vectorStoreName, nameof(vectorStoreName));
        var normalizedIndexName = ValidateRequired(indexName, nameof(indexName));
        var normalizedEmbeddingModel = string.IsNullOrWhiteSpace(embeddingModel)
            ? null
            : embeddingModel.Trim();

        Rag = new AgentRagOptions
        {
            VectorStoreName = normalizedVectorStoreName,
            IndexName = normalizedIndexName,
            EmbeddingModel = normalizedEmbeddingModel,
        };

        return this;
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
