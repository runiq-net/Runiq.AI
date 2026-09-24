namespace Runiq.AI.OrderSupport.Data;

/// <summary>
/// Provides the shared, deterministic order records used by the sample tools.
/// </summary>
public sealed class OrderSampleData
{
    private static readonly IReadOnlyDictionary<string, SampleOrder> Orders =
        new Dictionary<string, SampleOrder>(StringComparer.OrdinalIgnoreCase)
        {
            ["ORD-1001"] = new("ORD-1001", "Shipped", null, "Return eligibility cannot be determined until the order is delivered."),
            ["ORD-1002"] = new("ORD-1002", "Delivered", true, "The order is within the sample return window."),
            ["ORD-1003"] = new("ORD-1003", "Delivered", false, "The order contains a final-sale item."),
        };

    /// <summary>
    /// Initializes the deterministic in-memory order data source.
    /// </summary>
    public OrderSampleData()
    {
    }

    /// <summary>
    /// Finds an order by a normalized, case-insensitive identifier.
    /// </summary>
    /// <param name="orderId">The order identifier to find.</param>
    /// <returns>The matching sample order, or <see langword="null"/> when no order matches.</returns>
    internal SampleOrder? Find(string orderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        return Orders.GetValueOrDefault(orderId);
    }
}

/// <summary>
/// Represents one deterministic order and its return decision in the sample data set.
/// </summary>
/// <param name="OrderId">The normalized order identifier.</param>
/// <param name="Status">The current order status.</param>
/// <param name="IsReturnEligible">The return decision, or <see langword="null"/> when no decision is available.</param>
/// <param name="ReturnReason">The reason for the return decision or lack of one.</param>
internal sealed record SampleOrder(
    string OrderId,
    string Status,
    bool? IsReturnEligible,
    string ReturnReason);
