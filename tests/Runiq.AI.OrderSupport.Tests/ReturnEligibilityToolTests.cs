using Runiq.AI.OrderSupport.Data;
using Runiq.AI.OrderSupport.Tools;

namespace Runiq.AI.OrderSupport.Tests;

public sealed class ReturnEligibilityToolTests
{
    [Theory]
    [InlineData("ORD-1002", true, "within the sample return window")]
    [InlineData("ORD-1003", false, "final-sale item")]
    // Verifies eligible and ineligible orders return deterministic decisions with understandable reasons.
    public async Task ExecuteAsync_KnownOrder_ReturnsDecisionAndReason(
        string orderId,
        bool expectedEligibility,
        string expectedReason)
    {
        var tool = new ReturnEligibilityTool(new OrderSampleData());

        var result = await tool.ExecuteAsync(new OrderLookupInput { OrderId = orderId });

        Assert.True(result.Found);
        Assert.Equal(expectedEligibility, result.IsEligible);
        Assert.Contains(expectedReason, result.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "Enter a valid order ID")]
    [InlineData("invalid", "cannot be determined")]
    // Verifies invalid and unknown identifiers produce controlled eligibility results without a fabricated decision.
    public async Task ExecuteAsync_InvalidOrUnknownOrder_ReturnsControlledResult(string? orderId, string expectedReason)
    {
        var tool = new ReturnEligibilityTool(new OrderSampleData());

        var result = await tool.ExecuteAsync(new OrderLookupInput { OrderId = orderId });

        Assert.False(result.Found);
        Assert.Null(result.IsEligible);
        Assert.Contains(expectedReason, result.Reason, StringComparison.Ordinal);
    }
}
