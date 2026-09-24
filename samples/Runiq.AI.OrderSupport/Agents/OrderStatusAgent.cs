using Runiq.AI.Agents;
using Runiq.AI.Agents.Tools;
using Runiq.AI.OrderSupport.Tools;

namespace Runiq.AI.OrderSupport.Agents;

/// <summary>
/// Defines the agent that answers order status questions from deterministic sample data.
/// </summary>
public sealed class OrderStatusAgent : Agent
{
    private OrderStatusAgent(string? apiKey)
        : base(
            id: "order-status-agent",
            name: "Order Status Agent",
            instructions: """
            You answer questions about the current status of an order.

            Always use the order_status tool with the order ID supplied by the user.
            Base the answer only on the tool result and never invent an order or status.
            If the tool cannot find the order, explain that result clearly and briefly.
            Write the final answer in the same language as the user.
            """,
            model: "openai/gpt-5",
            apiKey: apiKey)
    {
    }

    /// <summary>
    /// Creates the order status agent with its exclusive status lookup tool.
    /// </summary>
    /// <param name="apiKey">The optional OpenAI API key used by the sample host.</param>
    /// <returns>The configured order status agent.</returns>
    public static Agent Create(string? apiKey)
    {
        return new OrderStatusAgent(apiKey)
            .AddTool<OrderStatusTool>();
    }
}
