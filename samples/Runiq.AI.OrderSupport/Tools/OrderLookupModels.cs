namespace Runiq.AI.OrderSupport.Tools;

/// <summary>
/// Supplies an order identifier to an order support tool.
/// </summary>
public sealed class OrderLookupInput
{
    /// <summary>
    /// Initializes an empty order lookup input for model argument binding.
    /// </summary>
    public OrderLookupInput()
    {
    }

    /// <summary>
    /// Gets the order identifier supplied by the user.
    /// </summary>
    public string? OrderId { get; init; }
}

/// <summary>
/// Describes the deterministic result of an order status lookup.
/// </summary>
/// <param name="OrderId">The normalized order identifier, when one was supplied.</param>
/// <param name="Found">Whether the order exists in the sample data.</param>
/// <param name="Status">The known order status, or <see langword="null"/> when unavailable.</param>
/// <param name="Message">A user-facing explanation of the lookup result.</param>
public sealed class OrderStatusOutput
{
    /// <summary>
    /// Initializes an order status result.
    /// </summary>
    /// <param name="orderId">The normalized order identifier, when one was supplied.</param>
    /// <param name="found">Whether the order exists in the sample data.</param>
    /// <param name="status">The known order status, or <see langword="null"/> when unavailable.</param>
    /// <param name="message">A user-facing explanation of the lookup result.</param>
    public OrderStatusOutput(string? orderId, bool found, string? status, string message)
    {
        OrderId = orderId;
        Found = found;
        Status = status;
        Message = message;
    }

    /// <summary>
    /// Gets the normalized order identifier, when one was supplied.
    /// </summary>
    public string? OrderId { get; }

    /// <summary>
    /// Gets a value indicating whether the order exists in the sample data.
    /// </summary>
    public bool Found { get; }

    /// <summary>
    /// Gets the known order status, or <see langword="null"/> when unavailable.
    /// </summary>
    public string? Status { get; }

    /// <summary>
    /// Gets the user-facing explanation of the lookup result.
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// Describes the deterministic result of a return eligibility lookup.
/// </summary>
/// <param name="OrderId">The normalized order identifier, when one was supplied.</param>
/// <param name="Found">Whether the order exists in the sample data.</param>
/// <param name="IsEligible">The eligibility decision, or <see langword="null"/> when it cannot be determined.</param>
/// <param name="Reason">A user-facing explanation of the decision or lack of one.</param>
public sealed class ReturnEligibilityOutput
{
    /// <summary>
    /// Initializes a return eligibility result.
    /// </summary>
    /// <param name="orderId">The normalized order identifier, when one was supplied.</param>
    /// <param name="found">Whether the order exists in the sample data.</param>
    /// <param name="isEligible">The eligibility decision, or <see langword="null"/> when it cannot be determined.</param>
    /// <param name="reason">A user-facing explanation of the decision or lack of one.</param>
    public ReturnEligibilityOutput(string? orderId, bool found, bool? isEligible, string reason)
    {
        OrderId = orderId;
        Found = found;
        IsEligible = isEligible;
        Reason = reason;
    }

    /// <summary>
    /// Gets the normalized order identifier, when one was supplied.
    /// </summary>
    public string? OrderId { get; }

    /// <summary>
    /// Gets a value indicating whether the order exists in the sample data.
    /// </summary>
    public bool Found { get; }

    /// <summary>
    /// Gets the eligibility decision, or <see langword="null"/> when it cannot be determined.
    /// </summary>
    public bool? IsEligible { get; }

    /// <summary>
    /// Gets the user-facing explanation of the decision or lack of one.
    /// </summary>
    public string Reason { get; }
}
