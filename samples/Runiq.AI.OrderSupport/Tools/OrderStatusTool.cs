using Runiq.AI.Agents.Tools;
using Runiq.AI.OrderSupport.Data;

namespace Runiq.AI.OrderSupport.Tools;

/// <summary>
/// Returns order status information from the deterministic sample data set.
/// </summary>
[RuniqTool(
    name: "order_status",
    description: "Returns the current status of an order from deterministic sample data.")]
public sealed class OrderStatusTool : IRuniqTool<OrderLookupInput, OrderStatusOutput>
{
    private readonly OrderSampleData _orderData;

    /// <summary>
    /// Initializes the tool with the shared deterministic order data source.
    /// </summary>
    /// <param name="orderData">The sample order data used for status lookups.</param>
    public OrderStatusTool(OrderSampleData orderData)
    {
        _orderData = orderData;
    }

    /// <inheritdoc />
    public Task<OrderStatusOutput> ExecuteAsync(
        OrderLookupInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var orderId = OrderIdNormalizer.Normalize(input.OrderId);
        if (orderId is null)
        {
            return Task.FromResult(new OrderStatusOutput(
                orderId: null,
                found: false,
                status: null,
                message: "Enter a valid order ID to check its status."));
        }

        var order = _orderData.Find(orderId);
        if (order is null)
        {
            return Task.FromResult(new OrderStatusOutput(
                orderId: orderId,
                found: false,
                status: null,
                message: $"Order {orderId} was not found in the sample data."));
        }

        return Task.FromResult(new OrderStatusOutput(
            orderId: order.OrderId,
            found: true,
            status: order.Status,
            message: $"Order {order.OrderId} is currently {order.Status}."));
    }
}
