
namespace Runiq.AI.Core.Rag;

/// <summary>
/// Reads read-only RAG visibility information from the RAG services and options registered in the host
/// dependency injection container. The reader never executes a RAG operation and never mutates RAG data.
/// </summary>
public sealed class RuniqRagInfoReader : IRuniqRagInfoProvider
{
    private const string RagNotRegisteredDiagnostic =
        "RAG services are not registered in the host application.";

    private const string NullVectorStoreDiagnostic =
        "No RAG vector store provider is configured. The default null vector store does not persist or retrieve vectors.";

    private const string OptionsUnavailableDiagnostic =
        "RAG options could not be resolved from the host services.";

    private const string ProviderRegistrationTypeName =
        "Runiq.AI.Rag.VectorStores.RagVectorStoreProviderRegistration";
    private const string VectorStoreTypeName = "Runiq.AI.Rag.Abstractions.VectorStores.IRagVectorStore";
    private const string RagOptionsTypeName = "Runiq.AI.Rag.Configuration.RagOptions";
    private const string TelemetryReaderTypeName = "Runiq.AI.Rag.Abstractions.Telemetry.IRagOperationTelemetryReader";

    private readonly IServiceProvider services;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuniqRagInfoReader"/> class.
    /// </summary>
    /// <param name="services">The service provider used to resolve registered RAG services and options.</param>
    public RuniqRagInfoReader(IServiceProvider services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public Task<RuniqRagInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Read());
    }

    private RuniqRagInfo Read()
    {
        try
        {
            var vectorStoreType = FindRagType(VectorStoreTypeName);
            var vectorStore = vectorStoreType is null ? null : services.GetService(vectorStoreType);

            if (vectorStore is null)
            {
                return new RuniqRagInfo
                {
                    Enabled = false,
                    Diagnostics = RagNotRegisteredDiagnostic
                };
            }

            var diagnostics = new List<string>();
            var providerVectorStore = ResolveProviderVectorStore(vectorStore);

            if (string.Equals(providerVectorStore.GetType().Name, "NullVectorStore", StringComparison.Ordinal))
            {
                diagnostics.Add(NullVectorStoreDiagnostic);
            }

            var options = ResolveOptions();

            if (options is null)
            {
                diagnostics.Add(OptionsUnavailableDiagnostic);
            }

            return new RuniqRagInfo
            {
                Enabled = true,
                VectorStore = providerVectorStore.GetType().Name,
                IndexName = options?.GetType().GetProperty("DefaultIndexName")?.GetValue(options) as string,
                DefaultTopK = options?.GetType().GetProperty("DefaultTopK")?.GetValue(options) as int?,
                EmbeddingDimension = null,
                LastUpsert = MapLastUpsertTelemetry(),
                LastRetrieval = MapLastRetrievalTelemetry(),
                Diagnostics = diagnostics.Count == 0 ? null : string.Join(" ", diagnostics)
            };
        }
        catch (Exception exception)
        {
            // The dashboard must surface configuration problems as data instead of failing the request.
            return new RuniqRagInfo
            {
                Enabled = false,
                Diagnostics = $"Failed to read RAG configuration: {exception.Message}"
            };
        }
    }

    private object ResolveProviderVectorStore(object vectorStore)
    {
        if (!string.Equals(vectorStore.GetType().Name, "ValidatingRagVectorStore", StringComparison.Ordinal))
        {
            return vectorStore;
        }

        // The configured provider store is held by an internal registration behind the validating
        // decorator; unwrap it so the dashboard shows the provider label instead of the decorator.
        var registrationType = vectorStore.GetType().Assembly
            .GetType(ProviderRegistrationTypeName);

        if (registrationType is null)
        {
            return vectorStore;
        }

        var registration = services.GetService(registrationType);

        if (registration is null)
        {
            return vectorStore;
        }

        var providerVectorStore = registrationType
            .GetProperty("VectorStore")
            ?.GetValue(registration);

        return providerVectorStore ?? vectorStore;
    }

    private object? ResolveOptions()
    {
        var optionsType = FindRagType(RagOptionsTypeName);
        if (optionsType is null) return null;
        var optionsInterface = typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(optionsType);
        return optionsInterface.GetProperty("Value")?.GetValue(services.GetService(optionsInterface));
    }

    private RuniqRagLastUpsertInfo? MapLastUpsertTelemetry()
    {
        var telemetry = ResolveTelemetry("LastUpsert");
        if (telemetry is null) return null;
        return new RuniqRagLastUpsertInfo
        {
            Succeeded = (bool)(telemetry.GetType().GetProperty("Succeeded")?.GetValue(telemetry) ?? false),
            ErrorCode = telemetry.GetType().GetProperty("ErrorCode")?.GetValue(telemetry)?.ToString() ?? string.Empty,
            Reason = telemetry.GetType().GetProperty("Reason")?.GetValue(telemetry)?.ToString() ?? string.Empty,
            ChunkCount = (int)(telemetry.GetType().GetProperty("ChunkCount")?.GetValue(telemetry) ?? 0),
            Timestamp = (DateTimeOffset)(telemetry.GetType().GetProperty("Timestamp")?.GetValue(telemetry) ?? default(DateTimeOffset))
        };
    }

    private RuniqRagLastRetrievalInfo? MapLastRetrievalTelemetry()
    {
        var telemetry = ResolveTelemetry("LastRetrieval");
        if (telemetry is null) return null;
        return new RuniqRagLastRetrievalInfo
        {
            Succeeded = (bool)(telemetry.GetType().GetProperty("Succeeded")?.GetValue(telemetry) ?? false),
            ErrorCode = telemetry.GetType().GetProperty("ErrorCode")?.GetValue(telemetry)?.ToString() ?? string.Empty,
            Reason = telemetry.GetType().GetProperty("Reason")?.GetValue(telemetry)?.ToString() ?? string.Empty,
            ResultCount = (int)(telemetry.GetType().GetProperty("ResultCount")?.GetValue(telemetry) ?? 0),
            DurationMilliseconds = ((TimeSpan)(telemetry.GetType().GetProperty("Duration")?.GetValue(telemetry) ?? TimeSpan.Zero)).TotalMilliseconds,
            Timestamp = (DateTimeOffset)(telemetry.GetType().GetProperty("Timestamp")?.GetValue(telemetry) ?? default(DateTimeOffset))
        };
    }

    private object? ResolveTelemetry(string property) => FindRagType(TelemetryReaderTypeName) is { } type ? type.GetProperty(property)?.GetValue(services.GetService(type)) : null;
    private static Type? FindRagType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).FirstOrDefault(type => type is not null);
}

