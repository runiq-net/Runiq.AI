using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Agents.Tests.Providers;

public sealed class OpenAIReasoningContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Actual serialized requests retain interleaved reasoning and calls across two tool rounds without a response chain.
    public async Task ReasoningToolRounds_PreserveOutputAndAccountForIt(bool completedOutput)
    {
        var handler = new Handler(completedOutput, 700);
        using var http = new HttpClient(handler);
        var resolver = new Resolver(new OpenAIResponsesClient(http));
        var counter = new Counter();
        using var services = new ServiceCollection().AddSingleton(counter).BuildServiceProvider();
        var agent = Create().UseContextBudget(new(4000, 10));
        var runtime = new AgentExecutionRuntime([agent], resolver, new AgentToolInvoker(services));
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in runtime.ExecuteStreamAsync("agent", "question")) events.Add(item);
        Assert.Equal(AgentExecutionEventKind.Completed, events[^1].Kind);
        Assert.Equal(3, handler.Bodies.Count);
        Assert.Equal(4, counter.Calls);

        for (var round = 1; round <= 2; round++)
        {
            using var document = JsonDocument.Parse(handler.Bodies[round]);
            var root = document.RootElement;
            Assert.False(root.TryGetProperty("previous_response_id", out _));
            Assert.Contains(root.GetProperty("include").EnumerateArray(), item => item.GetString() == "reasoning.encrypted_content");
            var input = root.GetProperty("input").EnumerateArray().ToArray();
            Assert.Equal("question", input[0].GetProperty("content").GetString());
            Assert.Equal(1 + round * 7, input.Length);
            for (var prior = 0; prior < round; prior++)
            {
                var original = handler.Output(prior + 1).EnumerateArray().ToArray();
                for (var index = 0; index < original.Length; index++)
                    Assert.True(JsonElement.DeepEquals(original[index], input[1 + prior * 7 + index]));
                var results = input.Skip(1 + prior * 7 + 5).Take(2).ToArray();
                Assert.All(results, result => Assert.Equal("function_call_output", result.GetProperty("type").GetString()));
                Assert.Equal($"call-{prior + 1}-a", results[0].GetProperty("call_id").GetString());
                Assert.Equal($"call-{prior + 1}-b", results[1].GetProperty("call_id").GetString());
            }

            var request = resolver.Requests[round];
            var budget = events.First(item => item.ContextBudget?.Invocation == round + 1).ContextBudget!;
            var opaque = request.Messages.Where(message => message.Continuation is not null).ToArray();
            Assert.Equal(round, opaque.Length);
            Assert.All(opaque, message => Assert.Equal(705, ContextTokenEstimator.EstimateMessage(message)));
            var ordinaryCost = request.Messages.Where(message => message.Continuation is null).Sum(ContextTokenEstimator.EstimateMessage)
                + ContextTokenEstimator.EstimateTools(request.Tools!);
            Assert.Equal(ordinaryCost + 705 * round, budget.MandatoryPromptTokens);
            Assert.True(budget.EstimatedPromptTokens + budget.ResponseTokenReserve <= budget.MaximumContextTokens);
            Assert.DoesNotContain("cipher-secret", JsonSerializer.Serialize(budget));
        }
        // State belongs to this invocation, not to the reused provider client or its next independent request.
        var next = await runtime.ExecuteAsync("agent", "another question");
        Assert.True(next.IsSuccess);
        Assert.All(resolver.Requests[^1].Messages, message => Assert.Null(message.Continuation));
        Assert.DoesNotContain("cipher-secret", handler.Bodies[^1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Both reported hidden-token usage and serialized continuation content can block the next HTTP call.
    public async Task ReasoningOverflow_StopsBeforeContinuationHttpRequest(bool reportedUsage)
    {
        var handler = new Handler(true, reportedUsage ? 5000 : null,
            reportedUsage ? "cipher-secret" : string.Join(' ', Enumerable.Repeat("cipher-secret", 1000)));
        using var http = new HttpClient(handler);
        var resolver = new Resolver(new OpenAIResponsesClient(http));
        var counter = new Counter();
        using var services = new ServiceCollection().AddSingleton(counter).BuildServiceProvider();
        var runtime = new AgentExecutionRuntime([Create().UseContextBudget(new(1000, 10))], resolver, new AgentToolInvoker(services));
        var result = await runtime.ExecuteAsync("agent", "question");
        Assert.Equal("ContextBudgetExceeded", result.ErrorCode);
        Assert.Single(handler.Bodies);
        Assert.Equal(2, counter.Calls);
        Assert.Equal(2, result.ContextBudget!.Invocation);
        Assert.True(result.ContextBudget.MandatoryPromptOverflow);
        Assert.True(result.ContextBudget.MandatoryPromptTokens > 1000);
        Assert.DoesNotContain("cipher-secret", result.ErrorMessage);
    }

    [Fact]
    // Non-streaming provider responses own the same continuation representation after their JSON document is disposed.
    public async Task CompleteAsync_RetainsReasoningSnapshot()
    {
        var handler = new Handler(true, 700, nonStreaming: true);
        using var http = new HttpClient(handler);
        var response = await new OpenAIResponsesClient(http).CompleteAsync(new(
            Runiq.AI.Core.Models.ModelReference.Parse("openai/gpt-5"), [new(ChatRole.User, "question")]));
        Assert.True(JsonElement.DeepEquals(handler.Output(1), response.Message.Continuation!.Payload));
        Assert.Equal(700, response.Message.Continuation.OutputTokens);
        Assert.Equal(2, response.Message.ToolCalls!.Count);
    }

    private static Agent Create() => new Agent("agent", "Agent", "instructions", "openai/gpt-5", "key").AddTool<LookupTool>();
    private sealed class Counter { internal int Calls; }
    [RuniqTool("lookup", "Returns a result.")]
    private sealed class LookupTool(Counter counter) : IRuniqTool<Dictionary<string, string>, string>
    {
        public Task<string> ExecuteAsync(Dictionary<string, string> input, CancellationToken cancellationToken = default)
        {
            counter.Calls++;
            return Task.FromResult("result");
        }
    }
    private sealed class Resolver(IChatClient client) : IChatClientResolver
    {
        internal List<ChatRequest> Requests { get; } = [];
        public IChatClient Resolve(ChatRequest request) { Requests.Add(request); return client; }
    }
    private sealed class Handler(bool completedOutput, int? outputTokens, string cipher = "cipher-secret", bool nonStreaming = false) : HttpMessageHandler
    {
        internal List<string> Bodies { get; } = [];
        internal JsonElement Output(int round) => JsonSerializer.SerializeToElement(new object[]
        {
            new { type = "reasoning", id = $"reason-{round}-a", summary = new[] { new { type = "summary_text", text = "summary" } }, encrypted_content = cipher },
            new { type = "message", id = $"message-{round}", role = "assistant", status = "completed",
                content = new[] { new { type = "output_text", text = "checking", annotations = Array.Empty<string>() } } },
            new { type = "function_call", id = $"function-{round}-a", call_id = $"call-{round}-a", name = "lookup", arguments = "{ }", status = "completed" },
            new { type = "reasoning", id = $"reason-{round}-b", summary = Array.Empty<string>(), encrypted_content = cipher + "-b" },
            new { type = "function_call", id = $"function-{round}-b", call_id = $"call-{round}-b", name = "lookup", arguments = "{}", status = "completed" }
        });
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var round = Bodies.Count;
            if (nonStreaming)
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { output = Output(1), usage = new { output_tokens = outputTokens } }), Encoding.UTF8, "application/json") };
            var events = new List<string>();
            if (round <= 2)
            {
                var output = Output(round);
                // The completed output may enrich done items with encryption; it must replace, not duplicate, them.
                foreach (var (item, index) in output.EnumerateArray().Select((item, index) => (item, index)))
                {
                    if (item.GetProperty("type").GetString() == "message")
                        events.Add(JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = "checking" }));
                    var done = completedOutput && item.GetProperty("type").GetString() == "reasoning"
                        ? JsonSerializer.SerializeToElement(new { type = "reasoning", id = item.GetProperty("id").GetString(), summary = Array.Empty<string>() }) : item;
                    events.Add(JsonSerializer.Serialize(new { type = "response.output_item.done", output_index = index, item = done }));
                }
                events.Add(completedOutput
                    ? JsonSerializer.Serialize(new { type = "response.completed", response = new { output, usage = new { output_tokens = outputTokens } } })
                    : JsonSerializer.Serialize(new { type = "response.completed", response = new { usage = new { output_tokens = outputTokens } } }));
            }
            else events.Add("{\"type\":\"response.output_text.delta\",\"delta\":\"answer\"}");
            return new(HttpStatusCode.OK) { Content = new StringContent(string.Join("\n\n", events.Select(item => "data: " + item)) + "\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream") };
        }
    }
}
