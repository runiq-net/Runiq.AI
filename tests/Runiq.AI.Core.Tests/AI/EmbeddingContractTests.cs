using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.Tests.AI;

/// <summary>
/// Verifies provider-neutral embedding contract behavior.
/// </summary>
public sealed class EmbeddingContractTests
{
    // Verifies that embedding request validation accepts ordered batch inputs and requested dimensions.
    [Fact]
    public void Validate_ShouldAcceptBatchInputsAndDimensions()
    {
        var request = new EmbeddingRequest(
            ModelReference.Parse("openai/text-embedding-3-small"),
            ["first", "second"],
            Dimensions: 256);

        var validated = request.Validate();

        Assert.Same(request, validated);
        Assert.Equal(["first", "second"], request.Inputs);
    }

    // Verifies that embedding request validation rejects empty input batches.
    [Fact]
    public void Validate_ShouldRejectEmptyInputBatch()
    {
        var request = new EmbeddingRequest(
            ModelReference.Parse("openai/text-embedding-3-small"),
            []);

        Assert.Throws<ArgumentException>(() => request.Validate());
    }

    // Verifies that embedding request validation rejects invalid requested dimensions.
    [Fact]
    public void Validate_ShouldRejectInvalidDimensions()
    {
        var request = new EmbeddingRequest(
            ModelReference.Parse("openai/text-embedding-3-small"),
            ["content"],
            Dimensions: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => request.Validate());
    }

    // Verifies that embedding results can preserve the request input order through explicit indexes.
    [Fact]
    public void EmbeddingResponse_ShouldPreserveInputOrderWithIndexes()
    {
        var response = new EmbeddingResponse(
            [
                new EmbeddingResult(0, [0.1f, 0.2f], Dimensions: 2),
                new EmbeddingResult(1, [0.3f, 0.4f], Dimensions: 2)
            ],
            new EmbeddingUsage(InputTokens: 4, TotalTokens: 4));

        Assert.Equal([0, 1], response.Results.Select(result => result.Index));
        Assert.All(response.Results, result => Assert.Equal(2, result.Dimensions));
    }
}
