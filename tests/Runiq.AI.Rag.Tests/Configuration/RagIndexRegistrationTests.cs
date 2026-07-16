using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Ingestion;
using Runiq.AI.Rag.Models.Ingestion;

namespace Runiq.AI.Rag.Tests.Configuration;

public sealed class RagIndexRegistrationTests
{
    // Verifies that indexes without an explicit strategy use safe manual ingestion semantics.
    [Fact]
    public void AddIndex_ShouldDefaultToManualIngestion()
    {
        var registration = Register(CompleteIndex);

        Assert.Equal(RagIngestionStrategyKind.Manual, registration.IngestionStrategy.Kind);
        Assert.Null(registration.IngestionStrategy.ScheduleExpression);
    }

    // Verifies that every explicit non-scheduled lifecycle strategy is retained without schedule metadata.
    [Theory]
    [InlineData(RagIngestionStrategyKind.Manual)]
    [InlineData(RagIngestionStrategyKind.OnStartup)]
    [InlineData(RagIngestionStrategyKind.BackgroundOnStartup)]
    public void ConfigureIngestion_ShouldRetainExplicitLifecycleStrategy(RagIngestionStrategyKind kind)
    {
        var registration = Register(index =>
        {
            CompleteIndex(index);
            index.ConfigureIngestion(ingestion =>
            {
                if (kind == RagIngestionStrategyKind.Manual) ingestion.Manual();
                else if (kind == RagIngestionStrategyKind.OnStartup) ingestion.OnStartup();
                else ingestion.BackgroundOnStartup();
            });
        });

        Assert.Equal(kind, registration.IngestionStrategy.Kind);
        Assert.Null(registration.IngestionStrategy.ScheduleExpression);
    }

    // Verifies that a valid scheduled strategy is normalized and projected as immutable configuration.
    [Fact]
    public void ConfigureIngestion_ShouldRetainScheduledExpression()
    {
        var registration = Register(index =>
        {
            CompleteIndex(index);
            index.ConfigureIngestion(ingestion => ingestion.Scheduled(" 0  2 * * * "));
        });

        Assert.Equal(RagIngestionStrategyKind.Scheduled, registration.IngestionStrategy.Kind);
        Assert.Equal("0 2 * * *", registration.IngestionStrategy.ScheduleExpression);
    }

    // Verifies that undefined strategy kinds fail before an index can be registered.
    [Fact]
    public void IngestionStrategy_ShouldRejectUndefinedKind() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RagIngestionStartStrategy((RagIngestionStrategyKind)99));

    // Verifies that scheduled ingestion cannot be created with an empty or clearly invalid expression.
    [Theory]
    [InlineData("")]
    [InlineData("not a schedule")]
    [InlineData("* * *")]
    public void ScheduledStrategy_ShouldRejectInvalidExpression(string expression) =>
        Assert.Throws<ArgumentException>(() => new RagIngestionStartStrategy(RagIngestionStrategyKind.Scheduled, expression));

    // Verifies that non-scheduled strategies reject schedule state.
    [Theory]
    [InlineData(RagIngestionStrategyKind.Manual)]
    [InlineData(RagIngestionStrategyKind.OnStartup)]
    [InlineData(RagIngestionStrategyKind.BackgroundOnStartup)]
    public void NonScheduledStrategy_ShouldRejectSchedule(RagIngestionStrategyKind kind) =>
        Assert.Throws<ArgumentException>(() => new RagIngestionStartStrategy(kind, "0 2 * * *"));

    // Verifies that repeated strategy selections fail instead of silently applying last-call-wins behavior.
    [Fact]
    public void ConfigureIngestion_ShouldRejectMultipleSelections() =>
        Assert.Throws<InvalidOperationException>(() => Register(index =>
        {
            CompleteIndex(index);
            index.ConfigureIngestion(ingestion => ingestion.Manual().OnStartup());
        }));

    // Verifies that an exception in the strategy callback does not leave a partially configured index.
    [Fact]
    public void ConfigureIngestion_ShouldLeaveCleanStateAfterCallbackException()
    {
        var builder = new RagIndexBuilderAccessor();

        Assert.Throws<InvalidOperationException>(() => builder.Index.ConfigureIngestion(_ => throw new InvalidOperationException("failure")));
        builder.Index.ConfigureIngestion(ingestion => ingestion.OnStartup());

        Assert.Equal(RagIngestionStrategyKind.OnStartup, builder.Register().IngestionStrategy.Kind);
    }

    // Verifies that typed and string embedding selections share one fail-fast source of truth.
    [Fact]
    public void UseEmbeddingModel_ShouldRejectMixedSelections() =>
        Assert.Throws<InvalidOperationException>(() => Register(index => index.UseDirectory("documents").UseVectorStore("store").UseEmbeddingModel("custom").UseEmbeddingModel(new RagEmbeddingModelReference("openai", "model", "Model"))));

    // Verifies that in-memory convenience selection uses the existing effective-reference path and safe metadata.
    [Fact]
    public void UseInMemoryVectorStore_ShouldProjectTypedMetadata()
    {
        var registration = Register(index => index.UseDirectory("documents").UseInMemoryVectorStore().UseEmbeddingModel("model"));

        Assert.Equal("in-memory", registration.VectorStoreReference);
        Assert.Equal("InMemory", registration.VectorStoreType);
    }

    // Verifies that multiple vector-store selections fail instead of silently replacing one another.
    [Fact]
    public void UseVectorStore_ShouldRejectMultipleSelections() =>
        Assert.Throws<InvalidOperationException>(() => Register(index => index.UseDirectory("documents").UseVectorStore("one").UseVectorStore("two").UseEmbeddingModel("model")));

    // Verifies that undefined typed provider references are rejected at composition time.
    [Fact]
    public void TypedReferences_ShouldRejectUndefinedValues()
    {
        Assert.Throws<ArgumentException>(() => Register(index => index.UseDirectory("documents").UseVectorStore("store").UseEmbeddingModel(default(RagEmbeddingModelReference))));
        Assert.Throws<ArgumentException>(() => Register(index => index.UseDirectory("documents").UseVectorStore(default(RagVectorStoreReference)).UseEmbeddingModel("model")));
    }

    // Verifies that a complete directory-backed index is available through the provider-independent registry.
    [Fact]
    public void AddIndex_ShouldRegisterDirectoryIndex()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("corporate-documents", index => index
            .UseDirectory("documents", "*.md", recursive: false)
            .UseVectorStore("default")
            .UseEmbeddingModel("text-embedding-3-small")));

        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().Registrations);

        Assert.Equal("corporate-documents", registration.Name);
        var source = Assert.IsType<DirectoryRagDocumentSource>(Assert.Single(registration.Sources));
        Assert.Equal("*.md", source.SearchPattern);
        Assert.False(source.Recursive);
    }

    // Verifies that multiple logical indexes remain distinct and deterministic.
    [Fact]
    public void AddIndex_ShouldRegisterMultipleIndexes()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag =>
        {
            rag.AddIndex("one", CompleteIndex);
            rag.AddIndex("two", CompleteIndex);
        });

        using var provider = services.BuildServiceProvider();
        Assert.Equal(["one", "two"], provider.GetRequiredService<IRagIndexRegistry>().Registrations.Select(index => index.Name));
    }

    // Verifies that duplicate logical index names fail during composition.
    [Fact]
    public void AddIndex_ShouldRejectDuplicateName()
    {
        var builder = new RagBuilder(new ServiceCollection());
        builder.AddIndex("documents", CompleteIndex);

        Assert.Throws<InvalidOperationException>(() => builder.AddIndex("documents", CompleteIndex));
    }

    // Verifies that empty and whitespace-only logical names fail during composition.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIndex_ShouldRejectEmptyName(string name)
    {
        Assert.Throws<ArgumentException>(() => new RagBuilder(new ServiceCollection()).AddIndex(name, CompleteIndex));
    }

    // Verifies that an index cannot be registered without a document source.
    [Fact]
    public void AddIndex_ShouldRejectMissingSource()
    {
        var builder = new RagBuilder(new ServiceCollection());
        Assert.Throws<InvalidOperationException>(() => builder.AddIndex("documents", index => index.UseVectorStore("default").UseEmbeddingModel("model")));
    }

    // Verifies that an index without an effective vector store reference fails during composition.
    [Fact]
    public void AddIndex_ShouldRejectMissingVectorStoreReference()
    {
        var builder = new RagBuilder(new ServiceCollection());
        Assert.Throws<InvalidOperationException>(() => builder.AddIndex("documents", index => index.UseDirectory("documents").UseEmbeddingModel("model")));
    }

    // Verifies that an index without an effective embedding reference fails during composition.
    [Fact]
    public void AddIndex_ShouldRejectMissingEmbeddingReference()
    {
        var builder = new RagBuilder(new ServiceCollection());
        Assert.Throws<InvalidOperationException>(() => builder.AddIndex("documents", index => index.UseDirectory("documents").UseVectorStore("default")));
    }

    // Verifies that source identity collisions fail during composition.
    [Fact]
    public void AddSource_ShouldRejectDuplicateIdentity()
    {
        var builder = new RagBuilder(new ServiceCollection());
        Assert.Throws<InvalidOperationException>(() => builder.AddIndex("documents", index => index
            .AddSource(new TestSource("same"))
            .AddSource(new TestSource("same"))));
    }

    // Verifies that directory paths are normalized without requiring the directory to exist.
    [Fact]
    public void UseDirectory_ShouldNormalizePathWithoutScanning()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "..", "documents");
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index.UseDirectory(missing).UseVectorStore("default").UseEmbeddingModel("model")));

        using var provider = services.BuildServiceProvider();
        var source = Assert.IsType<DirectoryRagDocumentSource>(Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().Registrations).Sources.Single());
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(missing)), source.RootPath);
    }

    // Verifies that invalid search patterns fail before runtime discovery.
    [Fact]
    public void UseDirectory_ShouldRejectInvalidSearchPattern()
    {
        Assert.Throws<ArgumentException>(() => new DirectoryRagDocumentSource("documents", searchPattern: "folder/*.md"));
    }

    // Verifies that registration neither scans a custom source nor starts ingestion.
    [Fact]
    public void AddIndex_ShouldNotDiscoverDocumentsOrStartIngestion()
    {
        var source = new TestSource("source");
        var builder = new RagBuilder(new ServiceCollection());

        builder.AddIndex("documents", index => index.AddSource(source).UseVectorStore("default").UseEmbeddingModel("model"));

        Assert.False(source.WasRead);
    }

    // Verifies that effective references and chunking settings are projected by the registry.
    [Fact]
    public void Registry_ShouldProjectEffectiveConfiguration()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index
            .UseDirectory("secret-parent/documents")
            .UseVectorStore("postgres")
            .UseEmbeddingModel("provider/model")
            .ConfigureChunking(500, 50)));
        using var provider = services.BuildServiceProvider();

        var metadata = Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().GetMetadata());

        Assert.Equal("postgres", metadata.VectorStoreReference);
        Assert.Equal("provider/model", metadata.EmbeddingReference);
        Assert.Equal("provider/model", metadata.EmbeddingDisplayName);
        Assert.Equal("Custom", metadata.VectorStoreType);
        Assert.Equal("postgres", metadata.VectorStoreDisplayName);
        Assert.Equal(RagIngestionStrategyKind.Manual, metadata.IngestionStrategyKind);
        Assert.Null(metadata.ScheduleExpression);
        Assert.Equal("max:500;overlap:50", metadata.ChunkingSummary);
        Assert.True(metadata.IsValid);
        Assert.Equal("documents", Assert.Single(metadata.Sources).DisplayValue);
        Assert.DoesNotContain(Path.GetFullPath("secret-parent"), metadata.Sources[0].DisplayValue, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that custom source implementations can participate without provider-specific registration APIs.
    [Fact]
    public void AddSource_ShouldAcceptCustomSourceImplementation()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index.AddSource(new TestSource("custom")).UseVectorStore("default").UseEmbeddingModel("model")));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<TestSource>(Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().Registrations).Sources.Single());
    }

    // Verifies that RAG without named indexes preserves the existing disabled-index registration behavior.
    [Fact]
    public void AddRuniqRag_ShouldExposeEmptyRegistry_WhenNoIndexesAreConfigured()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();
        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetRequiredService<IRagIndexRegistry>().Registrations);
    }

    private static void CompleteIndex(RagIndexBuilder index) => index.UseDirectory("documents").UseVectorStore("default").UseEmbeddingModel("model");

    private static RagIndexRegistration Register(Action<RagIndexBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", configure));
        using var provider = services.BuildServiceProvider();
        return Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().Registrations);
    }

    private sealed class RagIndexBuilderAccessor
    {
        private readonly RagBuilder builder = new(new ServiceCollection());
        public RagIndexBuilderAccessor()
        {
            Index = (RagIndexBuilder)Activator.CreateInstance(typeof(RagIndexBuilder), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, ["documents"], null)!;
            Index.UseDirectory("documents").UseVectorStore("store").UseEmbeddingModel("model");
        }
        public RagIndexBuilder Index { get; }
        public RagIndexRegistration Register()
        {
            var method = typeof(RagIndexBuilder).GetMethod("Build", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            return (RagIndexRegistration)method.Invoke(Index, null)!;
        }
    }

    private sealed class TestSource(string identity) : IRagDocumentSource
    {
        public string Identity => identity;
        public string SourceType => "Custom";
        public string DisplayValue => "custom";
        public bool WasRead { get; private set; }
        public Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return Task.FromResult<IReadOnlyList<RagSourceDocument>>([]);
        }
    }
}
