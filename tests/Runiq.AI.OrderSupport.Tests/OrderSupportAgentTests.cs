using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.OrderSupport.Agents;
using Runiq.AI.OrderSupport.Data;
using Runiq.AI.OrderSupport.Tools;

namespace Runiq.AI.OrderSupport.Tests;

public sealed class OrderSupportAgentTests
{
    [Fact]
    // Verifies the sample defines exactly two agents and restricts each agent to its own single tool.
    public void AgentDefinitions_ShouldExposeExactlyTwoExclusiveTools()
    {
        var agents = new[]
        {
            OrderStatusAgent.Create(apiKey: null),
            ReturnEligibilityAgent.Create(apiKey: null),
        };

        Assert.Equal(2, agents.Length);
        Assert.Equal("order_status", Assert.Single(agents[0].Tools).Name);
        Assert.Equal(typeof(OrderStatusTool), agents[0].Tools[0].ToolType);
        Assert.Equal("return_eligibility", Assert.Single(agents[1].Tools).Name);
        Assert.Equal(typeof(ReturnEligibilityTool), agents[1].Tools[0].ToolType);
        Assert.Equal(2, agents.SelectMany(agent => agent.Tools).Select(tool => tool.ToolType).Distinct().Count());
    }

    [Theory]
    [InlineData("order-status-agent", "Siparişim ORD-1001 şu anda nerede?", "order_status", "Shipped")]
    [InlineData("return-eligibility-agent", "ORD-1002 siparişini iade edebilir miyim?", "return_eligibility", "within the sample return window")]
    // Verifies natural-language requests execute the agent's exclusive tool and carry its result into the final answer.
    public async Task ExecuteAsync_NaturalLanguageRequest_UsesExpectedToolAndReturnsItsResult(
        string agentId,
        string request,
        string expectedToolName,
        string expectedAnswerContent)
    {
        var agent = agentId == "order-status-agent"
            ? OrderStatusAgent.Create(apiKey: "test-key")
            : ReturnEligibilityAgent.Create(apiKey: "test-key");
        var provider = new ControlledOrderSupportChatClient(expectedToolName);
        var services = new ServiceCollection()
            .AddSingleton<OrderSampleData>()
            .BuildServiceProvider();
        var runtime = new AgentExecutionRuntime(
            [agent],
            provider,
            provider,
            new AgentToolInvoker(services));

        var result = await runtime.ExecuteAsync(agentId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedToolName, provider.RequestedToolName);
        Assert.Contains(expectedAnswerContent, result.Message, StringComparison.Ordinal);
        var toolStep = Assert.Single(result.Steps, step => step.Kind == AgentExecutionStepKind.ToolCall);
        Assert.Equal(expectedToolName, toolStep.ToolName);
        Assert.Contains(expectedAnswerContent, toolStep.OutputJson, StringComparison.Ordinal);
    }

    private sealed class ControlledOrderSupportChatClient(string expectedToolName) : IChatClient
    {
        public string? RequestedToolName { get; private set; }

        public Task<ChatResponse> CompleteAsync(
            ChatRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The runtime test exercises streaming provider execution.");
        }

        public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var toolMessage = request.Messages.LastOrDefault(message => message.Role == ChatRole.Tool);
            if (toolMessage is null)
            {
                var tool = Assert.Single(request.Tools!);
                Assert.Equal(expectedToolName, tool.Name);
                RequestedToolName = tool.Name;
                var orderId = expectedToolName == "order_status" ? "ORD-1001" : "ORD-1002";
                yield return new ChatStreamingUpdate(
                    ChatStreamingUpdateKind.ToolCallDelta,
                    ToolCall: new ChatToolCall(
                        "sample-call",
                        tool.Name,
                        JsonSerializer.Serialize(new { orderId })));
                yield return new ChatStreamingUpdate(
                    ChatStreamingUpdateKind.Completed,
                    FinishReason: ChatFinishReason.ToolCalls);
                yield break;
            }

            using var output = JsonDocument.Parse(toolMessage.Content);
            var root = output.RootElement;
            var finalAnswer = expectedToolName == "order_status"
                ? $"Order {root.GetProperty("orderId").GetString()} is {root.GetProperty("status").GetString()}."
                : $"Order {root.GetProperty("orderId").GetString()} is eligible for return: {root.GetProperty("reason").GetString()}";
            yield return new ChatStreamingUpdate(
                ChatStreamingUpdateKind.ContentDelta,
                ContentDelta: finalAnswer);
            yield return new ChatStreamingUpdate(
                ChatStreamingUpdateKind.Completed,
                FinishReason: ChatFinishReason.Stop);
            await Task.CompletedTask;
        }
    }
}
