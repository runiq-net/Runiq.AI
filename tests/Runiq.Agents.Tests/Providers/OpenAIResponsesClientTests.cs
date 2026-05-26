using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents.Providers.OpenAI;
using Runiq.Agents.Tools;

namespace Runiq.Agents.Tests.Providers;

/// <summary>
/// OpenAI Responses istemcisinin tool output devam isteklerini doğrular.
/// </summary>
public sealed class OpenAIResponsesClientTests
{
    /// <summary>
    /// Function call output devam isteğinde OpenAI tarafından verilen call_id değerinin aynen kullanıldığını doğrular.
    /// </summary>
    [Fact]
    public async Task StreamAsync_ShouldSubmitFunctionCallOutputWithExactCallId()
    {
        var handler = new CapturingResponsesHandler(
            CreateFunctionCallStream(
                responseId: "resp_exact",
                itemId: "fc_item_should_not_be_used",
                callId: "call_exact_123",
                toolName: "echo_tool",
                argumentsJson: "{\"text\":\"hello\"}"),
            CreateTextStream("done"));

        var client = new OpenAIResponsesClient(new HttpClient(handler));
        var agent = CreateAgent<EchoTool>();
        var toolInvoker = CreateToolInvoker();

        var events = await client.StreamAsync(
                agent,
                new Uri("https://api.example.test/v1"),
                "Run tool.",
                toolInvoker)
            .ToListAsync();

        Assert.Contains(events, executionEvent =>
            executionEvent.Kind == AgentExecutionEventKind.ToolCallCompleted &&
            executionEvent.ToolCallId == "call_exact_123");

        var continuationJson = handler.RequestBodies[1];
        using var document = JsonDocument.Parse(continuationJson);
        var root = document.RootElement;
        var inputItem = root.GetProperty("input")[0];

        Assert.Equal("resp_exact", root.GetProperty("previous_response_id").GetString());
        Assert.Equal("function_call_output", inputItem.GetProperty("type").GetString());
        Assert.Equal("call_exact_123", inputItem.GetProperty("call_id").GetString());
        Assert.NotEqual("fc_item_should_not_be_used", inputItem.GetProperty("call_id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(inputItem.GetProperty("output").GetString()));
    }

    /// <summary>
    /// Lokal tool hata aldığında da Responses API'ye aynı call_id için function_call_output gönderildiğini doğrular.
    /// </summary>
    [Fact]
    public async Task StreamAsync_ShouldSubmitFunctionCallOutput_WhenLocalToolFails()
    {
        var handler = new CapturingResponsesHandler(
            CreateFunctionCallStream(
                responseId: "resp_failed_tool",
                itemId: "fc_item_failed",
                callId: "call_failed_123",
                toolName: "failing_tool",
                argumentsJson: "{\"text\":\"hello\"}"),
            CreateTextStream("handled"));

        var client = new OpenAIResponsesClient(new HttpClient(handler));
        var agent = CreateAgent<FailingTool>();
        var toolInvoker = CreateToolInvoker();

        var events = await client.StreamAsync(
                agent,
                new Uri("https://api.example.test/v1"),
                "Run tool.",
                toolInvoker)
            .ToListAsync();

        Assert.Contains(events, executionEvent =>
            executionEvent.Kind == AgentExecutionEventKind.ToolCallFailed &&
            executionEvent.ToolCallId == "call_failed_123");

        var continuationJson = handler.RequestBodies[1];
        using var document = JsonDocument.Parse(continuationJson);
        var inputItem = document.RootElement.GetProperty("input")[0];
        var outputJson = inputItem.GetProperty("output").GetString();

        Assert.Equal("call_failed_123", inputItem.GetProperty("call_id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(outputJson));

        using var outputDocument = JsonDocument.Parse(outputJson!);

        Assert.False(outputDocument.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("ToolExecutionFailed", outputDocument.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal("Demo tool failed.", outputDocument.RootElement.GetProperty("errorMessage").GetString());
    }

    /// <summary>
    /// OpenAI stream'i DONE satırı olmadan kapandığında bekleyen tool output devam isteğinin yine gönderildiğini doğrular.
    /// </summary>
    [Fact]
    public async Task StreamAsync_ShouldContinuePendingToolOutputs_WhenStreamClosesWithoutDone()
    {
        var handler = new CapturingResponsesHandler(
            CreateFunctionCallStream(
                responseId: "resp_without_done",
                itemId: "fc_item_without_done",
                callId: "call_without_done",
                toolName: "echo_tool",
                argumentsJson: "{\"text\":\"hello\"}",
                includeDone: false),
            CreateTextStream("continued"));

        var client = new OpenAIResponsesClient(new HttpClient(handler));
        var agent = CreateAgent<EchoTool>();
        var toolInvoker = CreateToolInvoker();

        var events = await client.StreamAsync(
                agent,
                new Uri("https://api.example.test/v1"),
                "Run tool.",
                toolInvoker)
            .ToListAsync();

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains(events, executionEvent =>
            executionEvent.Kind == AgentExecutionEventKind.AssistantDelta &&
            executionEvent.Content == "continued");
    }

    private static Agent CreateAgent<TTool>()
        where TTool : class
    {
        return new Agent(
                id: "agent",
                name: "Agent",
                instructions: "Use tools.",
                model: "openai/gpt-5",
                apiKey: "test-key")
            .AddTool<TTool>();
    }

    private static AgentToolInvoker CreateToolInvoker()
    {
        return new AgentToolInvoker(new ServiceCollection().BuildServiceProvider());
    }

    private static string CreateFunctionCallStream(
        string responseId,
        string itemId,
        string callId,
        string toolName,
        string argumentsJson,
        bool includeDone = true)
    {
        var lines = new List<string>
        {
            $"data: {{\"type\":\"response.created\",\"response\":{{\"id\":\"{responseId}\"}}}}",
            $"data: {{\"type\":\"response.output_item.done\",\"item\":{{\"id\":\"{itemId}\",\"type\":\"function_call\",\"status\":\"completed\",\"call_id\":\"{callId}\",\"name\":\"{toolName}\",\"arguments\":{JsonSerializer.Serialize(argumentsJson)}}}}}",
            $"data: {{\"type\":\"response.completed\",\"response\":{{\"id\":\"{responseId}\"}}}}",
        };

        if (includeDone)
        {
            lines.Add("data: [DONE]");
        }

        lines.Add(string.Empty);

        return string.Join("\n", lines);
    }

    private static string CreateTextStream(string text)
    {
        return string.Join(
            "\n",
            $"data: {{\"type\":\"response.output_text.delta\",\"delta\":{JsonSerializer.Serialize(text)}}}",
            "data: [DONE]",
            string.Empty);
    }

    private sealed class CapturingResponsesHandler : HttpMessageHandler
    {
        private readonly Queue<string> responseBodies;

        public CapturingResponsesHandler(params string[] responseBodies)
        {
            this.responseBodies = new Queue<string>(responseBodies);
        }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBodies.Dequeue(),
                    Encoding.UTF8,
                    "text/event-stream")
            };
        }
    }

    [RuniqTool("echo_tool", "Echoes text.")]
    private sealed class EchoTool : IRuniqTool<EchoInput, EchoOutput>
    {
        public Task<EchoOutput> ExecuteAsync(
            EchoInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EchoOutput(input.Text));
        }
    }

    [RuniqTool("failing_tool", "Fails for testing.")]
    private sealed class FailingTool : IRuniqTool<EchoInput, EchoOutput>
    {
        public Task<EchoOutput> ExecuteAsync(
            EchoInput input,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Demo tool failed.");
        }
    }

    private sealed record EchoInput(string Text);

    private sealed record EchoOutput(string Text);
}
