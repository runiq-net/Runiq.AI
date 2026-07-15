using Runiq.AI.Agents;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentTests
{
    [Fact]
    public void Constructor_ShouldExposeLegacyPublicOverload()
    {
        var constructor = typeof(Agent).GetConstructor([
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Runiq.AI.Core.Configuration.ProviderOptions),
            typeof(string),
            typeof(string)
        ]);

        Assert.NotNull(constructor);
    }

    [Fact]
    public void Constructor_ShouldExposeRagOptionsOverload()
    {
        var constructor = typeof(Agent).GetConstructor([
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Runiq.AI.Core.Configuration.ProviderOptions),
            typeof(string),
            typeof(string),
            typeof(Runiq.AI.Agents.Configuration.AgentRagOptions)
        ]);

        Assert.NotNull(constructor);

        var agent = new Agent(
            id: "travel-agent",
            name: "Travel Agent",
            instructions: "Plan short travel routes.",
            model: "ollama/llama3",
            rag: new Runiq.AI.Agents.Configuration.AgentRagOptions { IndexName = "documents" });

        Assert.NotNull(agent.Rag);
        Assert.Equal("documents", agent.Rag.IndexName);
    }

    // Verifies that an agent can attach a context space id and return itself for chaining.
    [Fact]
    public void UseContextSpace_ShouldAttachContextSpaceId()
    {
        var agent = CreateAgent();

        var result = agent.UseContextSpace("travel-planning");

        Assert.Same(agent, result);

        var contextSpaceId = Assert.Single(agent.ContextSpaceIds);
        Assert.Equal("travel-planning", contextSpaceId);
    }

    // Verifies that context space ids are trimmed before being stored on the agent.
    [Fact]
    public void UseContextSpace_ShouldTrimContextSpaceId()
    {
        var agent = CreateAgent();

        agent.UseContextSpace(" travel-planning ");

        var contextSpaceId = Assert.Single(agent.ContextSpaceIds);
        Assert.Equal("travel-planning", contextSpaceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UseContextSpace_ShouldThrow_WhenContextSpaceIdIsEmpty(string contextSpaceId)
    {
        // Verifies that empty context space ids cannot be attached to an agent.
        var agent = CreateAgent();

        var exception = Assert.Throws<ArgumentException>(() =>
            agent.UseContextSpace(contextSpaceId));

        Assert.Equal("contextSpaceId", exception.ParamName);
    }

    // Verifies that duplicate context space ids are rejected with case-insensitive comparison.
    [Fact]
    public void UseContextSpace_ShouldThrow_WhenContextSpaceIdAlreadyExistsIgnoringCase()
    {
        var agent = CreateAgent();

        agent.UseContextSpace("travel-planning");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            agent.UseContextSpace("TRAVEL-PLANNING"));

        Assert.Contains("travel-planning", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UseRagIndex_ShouldConfigureAgentRagIndexName()
    {
        var agent = CreateAgent();

        var result = agent.UseRagIndex(" documents ");

        Assert.Same(agent, result);
        Assert.NotNull(agent.Rag);
        Assert.Equal("documents", agent.Rag.IndexName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UseRagIndex_ShouldThrow_WhenIndexNameIsEmpty(string indexName)
    {
        var agent = CreateAgent();

        var exception = Assert.Throws<ArgumentException>(() =>
            agent.UseRagIndex(indexName));

        Assert.Equal("indexName", exception.ParamName);
    }

    // Verifies that associating a Vector Query Tool configures the vector store name, index name, and embedding model and returns the agent for chaining.
    [Fact]
    public void UseVectorQueryTool_ShouldConfigureAgentRagAssociation()
    {
        var agent = CreateAgent();

        var result = agent.UseVectorQueryTool("documents-store", "documents", "text-embedding-3-small");

        Assert.Same(agent, result);
        Assert.NotNull(agent.Rag);
        Assert.True(agent.Rag.Enabled);
        Assert.Equal("documents-store", agent.Rag.VectorStoreName);
        Assert.Equal("documents", agent.Rag.IndexName);
        Assert.Equal("text-embedding-3-small", agent.Rag.EmbeddingModel);
    }

    // Verifies that the Vector Query Tool association trims the vector store name, index name, and embedding model.
    [Fact]
    public void UseVectorQueryTool_ShouldTrimValues()
    {
        var agent = CreateAgent();

        agent.UseVectorQueryTool(" documents-store ", " documents ", " text-embedding-3-small ");

        Assert.NotNull(agent.Rag);
        Assert.Equal("documents-store", agent.Rag.VectorStoreName);
        Assert.Equal("documents", agent.Rag.IndexName);
        Assert.Equal("text-embedding-3-small", agent.Rag.EmbeddingModel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UseVectorQueryTool_ShouldAssociateNoEmbeddingModel_WhenEmbeddingModelIsNullOrWhitespace(
        string? embeddingModel)
    {
        // Verifies that the optional embedding model is treated as unset when null or whitespace, since it is boundary-optional configuration.
        var agent = CreateAgent();

        agent.UseVectorQueryTool("documents-store", "documents", embeddingModel);

        Assert.NotNull(agent.Rag);
        Assert.Null(agent.Rag.EmbeddingModel);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UseVectorQueryTool_ShouldThrow_WhenVectorStoreNameIsEmpty(string vectorStoreName)
    {
        // Verifies that the required vector store name boundary condition is validated by the association.
        var agent = CreateAgent();

        var exception = Assert.Throws<ArgumentException>(() =>
            agent.UseVectorQueryTool(vectorStoreName, "documents"));

        Assert.Equal("vectorStoreName", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UseVectorQueryTool_ShouldThrow_WhenIndexNameIsEmpty(string indexName)
    {
        // Verifies that the required index name boundary condition is validated by the association.
        var agent = CreateAgent();

        var exception = Assert.Throws<ArgumentException>(() =>
            agent.UseVectorQueryTool("documents-store", indexName));

        Assert.Equal("indexName", exception.ParamName);
    }

    // Verifies that the Vector Query Tool association replaces previously configured RAG options, staying compatible with UseRagIndex.
    [Fact]
    public void UseVectorQueryTool_ShouldReplacePreviousRagOptions()
    {
        var agent = CreateAgent();
        agent.UseRagIndex("legacy-index");

        agent.UseVectorQueryTool("documents-store", "documents");

        Assert.NotNull(agent.Rag);
        Assert.Equal("documents-store", agent.Rag.VectorStoreName);
        Assert.Equal("documents", agent.Rag.IndexName);
        Assert.Null(agent.Rag.EmbeddingModel);
    }

    // Verifies that UseRagIndex remains compatible and leaves the Vector Query Tool association fields unset.
    [Fact]
    public void UseRagIndex_ShouldLeaveVectorQueryToolAssociationUnset()
    {
        var agent = CreateAgent();

        agent.UseRagIndex("documents");

        Assert.NotNull(agent.Rag);
        Assert.Null(agent.Rag.VectorStoreName);
        Assert.Null(agent.Rag.EmbeddingModel);
    }

    private static Agent CreateAgent()
    {
        return new Agent(
            id: "travel-agent",
            name: "Travel Agent",
            instructions: "Plan short travel routes.",
            model: "openai/gpt-5");
    }
}

