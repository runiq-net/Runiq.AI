using Runiq.AI.OrderSupport.Data;
using Runiq.AI.OrderSupport.Tools;

namespace Runiq.AI.OrderSupport.Tests;

public sealed class OrderStatusToolTests
{
    [Fact]
    // Verifies a known order returns its deterministic status through the status tool.
    public async Task ExecuteAsync_KnownOrder_ReturnsStatus()
    {
        var tool = new OrderStatusTool(new OrderSampleData());

        var result = await tool.ExecuteAsync(new OrderLookupInput { OrderId = " ord-1001 " });

        Assert.True(result.Found);
        Assert.Equal("ORD-1001", result.OrderId);
        Assert.Equal("Shipped", result.Status);
        Assert.Contains("currently Shipped", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "Enter a valid order ID")]
    [InlineData("   ", "Enter a valid order ID")]
    [InlineData("ORD-9999", "was not found")]
    // Verifies invalid and unknown identifiers produce controlled, user-facing status results.
    public async Task ExecuteAsync_InvalidOrUnknownOrder_ReturnsControlledResult(string? orderId, string expectedMessage)
    {
        var tool = new OrderStatusTool(new OrderSampleData());

        var result = await tool.ExecuteAsync(new OrderLookupInput { OrderId = orderId });

        Assert.False(result.Found);
        Assert.Null(result.Status);
        Assert.Contains(expectedMessage, result.Message, StringComparison.Ordinal);
    }
}
