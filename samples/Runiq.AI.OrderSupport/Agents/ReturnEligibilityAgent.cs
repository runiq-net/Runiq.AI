using Runiq.AI.Agents;
using Runiq.AI.Agents.Tools;
using Runiq.AI.OrderSupport.Tools;

namespace Runiq.AI.OrderSupport.Agents;

/// <summary>
/// Defines the agent that explains deterministic return eligibility decisions.
/// </summary>
public sealed class ReturnEligibilityAgent : Agent
{
    private ReturnEligibilityAgent(string? apiKey)
        : base(
            id: "return-eligibility-agent",
            name: "Return Eligibility Agent",
            instructions: """
            You answer questions about whether an order is eligible for return.

            Always use the return_eligibility tool with the order ID supplied by the user.
            Base the answer only on the tool result and include its reason.
            Never invent an eligibility decision when the tool cannot determine one.
            Write the final answer in the same language as the user.
            """,
            model: "openai/gpt-5",
            apiKey: apiKey)
    {
    }

    /// <summary>
    /// Creates the return eligibility agent with its exclusive eligibility tool.
    /// </summary>
    /// <param name="apiKey">The optional OpenAI API key used by the sample host.</param>
    /// <returns>The configured return eligibility agent.</returns>
    public static Agent Create(string? apiKey)
    {
        return new ReturnEligibilityAgent(apiKey)
            .AddTool<ReturnEligibilityTool>();
    }
}
