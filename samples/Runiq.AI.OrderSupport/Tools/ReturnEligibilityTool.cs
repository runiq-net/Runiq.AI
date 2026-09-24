using Runiq.AI.Agents.Tools;
using Runiq.AI.OrderSupport.Data;

namespace Runiq.AI.OrderSupport.Tools;

/// <summary>
/// Returns deterministic return eligibility decisions and their reasons.
/// </summary>
[RuniqTool(
    name: "return_eligibility",
    description: "Determines return eligibility for an order from deterministic sample data.")]
public sealed class ReturnEligibilityTool : IRuniqTool<OrderLookupInput, ReturnEligibilityOutput>
{
    private readonly OrderSampleData _orderData;

    /// <summary>
    /// Initializes the tool with the shared deterministic order data source.
    /// </summary>
    /// <param name="orderData">The sample order data used for eligibility decisions.</param>
    public ReturnEligibilityTool(OrderSampleData orderData)
    {
        _orderData = orderData;
    }

    /// <inheritdoc />
    public Task<ReturnEligibilityOutput> ExecuteAsync(
        OrderLookupInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var orderId = OrderIdNormalizer.Normalize(input.OrderId);
        if (orderId is null)
        {
            return Task.FromResult(new ReturnEligibilityOutput(
                orderId: null,
                found: false,
                isEligible: null,
                reason: "Enter a valid order ID to check return eligibility."));
        }

        var order = _orderData.Find(orderId);
        if (order is null)
        {
            return Task.FromResult(new ReturnEligibilityOutput(
                orderId: orderId,
                found: false,
                isEligible: null,
                reason: $"Return eligibility for order {orderId} cannot be determined because the order was not found."));
        }

        return Task.FromResult(new ReturnEligibilityOutput(
            orderId: order.OrderId,
            found: true,
            isEligible: order.IsReturnEligible,
            reason: order.ReturnReason));
    }
}
