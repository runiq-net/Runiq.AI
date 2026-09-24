namespace Runiq.AI.OrderSupport.Tools;

internal static class OrderIdNormalizer
{
    internal static string? Normalize(string? orderId)
    {
        return string.IsNullOrWhiteSpace(orderId)
            ? null
            : orderId.Trim().ToUpperInvariant();
    }
}
