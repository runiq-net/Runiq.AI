using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.Rag.Abstractions.Telemetry;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Configuration;
using Runiq.Rag.Models.Telemetry;
using Runiq.Rag.VectorStores;

namespace Runiq.Core.Rag;

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
        "Runiq.Rag.VectorStores.RagVectorStoreProviderRegistration";

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
            var vectorStore = services.GetService<IRagVectorStore>();

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

            if (providerVectorStore is NullVectorStore)
            {
                diagnostics.Add(NullVectorStoreDiagnostic);
            }

            var options = services.GetService<IOptions<RagOptions>>()?.Value;

            if (options is null)
            {
                diagnostics.Add(OptionsUnavailableDiagnostic);
            }

            return new RuniqRagInfo
            {
                Enabled = true,
                VectorStore = providerVectorStore.GetType().Name,
                IndexName = options?.DefaultIndexName,
                DefaultTopK = options?.DefaultTopK,
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

    private IRagVectorStore ResolveProviderVectorStore(IRagVectorStore vectorStore)
    {
        if (vectorStore is not ValidatingRagVectorStore)
        {
            return vectorStore;
        }

        // The configured provider store is held by an internal registration behind the validating
        // decorator; unwrap it so the dashboard shows the provider label instead of the decorator.
        var registrationType = typeof(ValidatingRagVectorStore).Assembly
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
            ?.GetValue(registration) as IRagVectorStore;

        return providerVectorStore ?? vectorStore;
    }

    private RuniqRagLastUpsertInfo? MapLastUpsertTelemetry()
    {
        var telemetry = services.GetService<IRagOperationTelemetryReader>()?.LastUpsert;

        return telemetry is null ? null : MapLastUpsertTelemetry(telemetry);
    }

    private static RuniqRagLastUpsertInfo MapLastUpsertTelemetry(RagLastUpsertTelemetry telemetry)
    {
        return new RuniqRagLastUpsertInfo
        {
            Succeeded = telemetry.Succeeded,
            ErrorCode = telemetry.ErrorCode.ToString(),
            Reason = telemetry.Reason,
            ChunkCount = telemetry.ChunkCount,
            Timestamp = telemetry.Timestamp
        };
    }

    private RuniqRagLastRetrievalInfo? MapLastRetrievalTelemetry()
    {
        var telemetry = services.GetService<IRagOperationTelemetryReader>()?.LastRetrieval;

        return telemetry is null ? null : MapLastRetrievalTelemetry(telemetry);
    }

    private static RuniqRagLastRetrievalInfo MapLastRetrievalTelemetry(RagLastRetrievalTelemetry telemetry)
    {
        return new RuniqRagLastRetrievalInfo
        {
            Succeeded = telemetry.Succeeded,
            ErrorCode = telemetry.ErrorCode.ToString(),
            Reason = telemetry.Reason,
            ResultCount = telemetry.ResultCount,
            DurationMilliseconds = telemetry.Duration.TotalMilliseconds,
            Timestamp = telemetry.Timestamp
        };
    }
}
