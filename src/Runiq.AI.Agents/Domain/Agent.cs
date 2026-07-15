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
    private readonly List<string> contextSpaceIds = [];

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
    public string Model { get; }

    /// <summary>
    /// Model tanimindan çözümlenen provider adini alir.
    /// </summary>
    public string ProviderName => ModelReference.ProviderName;

    /// <summary>
    /// Model tanimindan çözümlenen model adini alir.
    /// </summary>
    public string ModelName => ModelReference.ModelName;

    /// <summary>
    /// Provider çagrilarinda kullanilacak opsiyonel API anahtarini alir.
    /// </summary>
    public string? ApiKey { get; }

    /// <summary>
    /// Modelin yanit üretirken kullanacagi akil yürütme yogunlugunu alir.
    /// </summary>
    public string ReasoningEffort { get; }

    /// <summary>
    /// Model yanitinin ayrinti seviyesini alir.
    /// </summary>
    public string Verbosity { get; }

    /// <summary>
    /// Provider için tanimlanan opsiyonel çalisma zamani ayarlarini alir.
    /// </summary>
    public ProviderOptions? Provider { get; }

    /// <summary>
    /// Agent RAG sorgulari için opsiyonel çalisma zamani ayarlarini alir.
    /// </summary>
    public AgentRagOptions? Rag { get; private set; }

    /// <summary>
    /// Provider ve model adini ayristirilmis biçimde temsil eden model referansini alir.
    /// </summary>
    public ModelReference ModelReference { get; }

    /// <summary>
    /// Agent'a baglanmis context space teknik kimliklerini döner.
    /// </summary>
    public IReadOnlyList<string> ContextSpaceIds => contextSpaceIds;

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
    /// Yeni bir agent tanimi ve RAG çalisma zamani ayarlari olusturur.
    /// </summary>
    /// <param name="id">Ajanin sistem içindeki benzersiz kimligidir.</param>
    /// <param name="name">Ajanin gösterilecek adidir.</param>
    /// <param name="instructions">Ajanin model çagrilarinda kullanilacak sistem yönergeleridir.</param>
    /// <param name="model">Kullanilacak modelin provider/model biçimindeki adidir.</param>
    /// <param name="rag">Agent RAG sorgulari için opsiyonel çalisma zamani ayarlaridir.</param>
    /// <param name="apiKey">Provider çagrilarinda kullanilacak opsiyonel API anahtaridir.</param>
    /// <param name="provider">Provider için opsiyonel çalisma zamani ayarlaridir.</param>
    /// <param name="reasoningEffort">Modelin akil yürütme yogunlugudur.</param>
    /// <param name="verbosity">Model yanitinin ayrinti seviyesidir.</param>
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
    /// <param name="rag">Agent RAG sorgulari için opsiyonel çalisma zamani ayarlaridir.</param>
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
    /// Agent'a kullanilacak bir context space baglantisi ekler.
    /// </summary>
    /// <param name="contextSpaceId">Baglanacak context space teknik kimligidir.</param>
    /// <returns>Akici yapilandirma için mevcut agent örnegi.</returns>
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
    /// Agent RAG sorgulari için kullanilacak vector index adini yapilandirir.
    /// </summary>
    /// <param name="indexName">Kullanilacak vector index adidir.</param>
    /// <returns>Akici yapilandirma için mevcut agent örnegi.</returns>
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

