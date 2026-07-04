using Runiq.Agents.Configuration;

namespace Runiq.Agents.Tests.Configuration;

public sealed class AgentRagOptionsTests
{
    // Verifies that the Vector Query Tool association fields are optional and default to unset while retrieval stays enabled by default.
    [Fact]
    public void Defaults_ShouldLeaveVectorQueryToolAssociationUnset()
    {
        var options = new AgentRagOptions();

        Assert.True(options.Enabled);
        Assert.Null(options.IndexName);
        Assert.Null(options.VectorStoreName);
        Assert.Null(options.EmbeddingModel);
    }

    // Verifies that the Vector Query Tool association fields carry the assigned vector store name and embedding model.
    [Fact]
    public void VectorQueryToolAssociationFields_ShouldCarryAssignedValues()
    {
        var options = new AgentRagOptions
        {
            IndexName = "documents",
            VectorStoreName = "documents-store",
            EmbeddingModel = "text-embedding-3-small",
        };

        Assert.Equal("documents", options.IndexName);
        Assert.Equal("documents-store", options.VectorStoreName);
        Assert.Equal("text-embedding-3-small", options.EmbeddingModel);
    }
}
