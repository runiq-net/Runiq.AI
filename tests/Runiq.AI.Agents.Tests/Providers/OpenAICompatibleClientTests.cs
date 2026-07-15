using System.Net;
using System.Text;
using System.Text.Json;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Agents.Tests.Providers;

public sealed class OpenAICompatibleClientTests
{
    // Verifies that text, finish reason, and token usage map to the shared response contract.
    [Fact]
    public async Task CompleteAsync_ShouldMapTextFinishReasonAndUsage()
    {
        var handler = new CapturingHandler("""
            {"choices":[{"finish_reason":"length","message":{"content":"hello"}}],"usage":{"prompt_tokens":2,"completion_tokens":3,"total_tokens":5}}
            """);
        var response = await new OpenAICompatibleClient(new HttpClient(handler)).CompleteAsync(CreateRequest());

        Assert.Equal("hello", response.Message.Content);
        Assert.Equal(ChatFinishReason.Length, response.FinishReason);
        Assert.Equal(new ChatUsage(2, 3, 5), response.Usage);
    }

    // Verifies that tool definitions, assistant tool calls, and tool results preserve protocol identifiers.
    [Fact]
    public async Task CompleteAsync_ShouldMapToolPayloadAndMultipleToolCalls()
    {
        var handler = new CapturingHandler("""
            {"choices":[{"finish_reason":"tool_calls","message":{"content":null,"tool_calls":[{"id":"call_1","type":"function","function":{"name":"first","arguments":"{\"x\":1}"}},{"id":"call_2","type":"function","function":{"name":"second","arguments":"{}"}}]}}]}
            """);
        var request = CreateRequest() with
        {
            Tools = [new ChatToolDefinition("first", "First tool", "{\"type\":\"object\"}")],
            Messages =
            [
                new ChatMessage(ChatRole.Assistant, string.Empty, ToolCalls: [new ChatToolCall("prior", "first", "{}")]),
                new ChatMessage(ChatRole.Tool, "{\"ok\":true}", "prior")
            ]
        };

        var response = await new OpenAICompatibleClient(new HttpClient(handler)).CompleteAsync(request);

        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.Equal(2, response.Message.ToolCalls?.Count);
        using var payload = JsonDocument.Parse(handler.RequestBodies.Single());
        Assert.Equal("function", payload.RootElement.GetProperty("tools")[0].GetProperty("type").GetString());
        Assert.Equal("prior", payload.RootElement.GetProperty("messages")[1].GetProperty("tool_call_id").GetString());
    }

    // Verifies that structured output is serialized as an OpenAI-compatible JSON schema response format.
    [Fact]
    public async Task CompleteAsync_ShouldMapStructuredOutputFormat()
    {
        var handler = new CapturingHandler("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"{}\"}}]}");
        var request = CreateRequest() with { ResponseFormat = new ChatResponseFormat("answer", "{\"type\":\"object\"}") };

        await new OpenAICompatibleClient(new HttpClient(handler)).CompleteAsync(request);

        using var payload = JsonDocument.Parse(handler.RequestBodies.Single());
        Assert.Equal("json_schema", payload.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }

    // Verifies that fragmented streaming tool arguments are combined into one provider-neutral tool call.
    [Fact]
    public async Task CompleteStreamingAsync_ShouldAggregateToolCallDeltas()
    {
        var stream = string.Join('\n',
            "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call_1\",\"function\":{\"name\":\"echo\",\"arguments\":\"{\\\"value\\\":\"}}]},\"finish_reason\":null}]}",
            "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"1}\"}}]},\"finish_reason\":\"tool_calls\"}]}",
            "data: [DONE]", string.Empty);

        var updates = await new OpenAICompatibleClient(new HttpClient(new CapturingHandler(stream, "text/event-stream")))
            .CompleteStreamingAsync(CreateRequest()).ToListAsync();

        var call = Assert.Single(updates, update => update.Kind == ChatStreamingUpdateKind.ToolCallDelta).ToolCall;
        Assert.Equal("call_1", call?.Id);
        Assert.Equal("{\"value\":1}", call?.ArgumentsJson);
        Assert.Equal(ChatFinishReason.ToolCalls, updates[^1].FinishReason);
    }

    // Verifies that HTTP failures map to the provider-neutral error taxonomy.
    [Fact]
    public async Task CompleteAsync_ShouldMapProviderError()
    {
        var client = new OpenAICompatibleClient(new HttpClient(new CapturingHandler("missing", statusCode: HttpStatusCode.NotFound)));

        var exception = await Assert.ThrowsAsync<ChatProviderException>(() => client.CompleteAsync(CreateRequest()));

        Assert.Equal(ProviderErrorKind.ModelNotFound, exception.Kind);
        Assert.Equal(404, exception.StatusCode);
    }

    // Verifies that cancellation is propagated without being converted into a provider protocol error.
    [Fact]
    public async Task CompleteAsync_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new OpenAICompatibleClient(new HttpClient(new CapturingHandler("{}")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CompleteAsync(CreateRequest(), cancellation.Token));
    }

    private static ChatRequest CreateRequest() => new(
        ModelReference.Parse("ollama/llama3"),
        [new ChatMessage(ChatRole.User, "hello")],
        new Uri("https://api.example.test/v1"));

    private sealed class CapturingHandler(
        string responseBody,
        string mediaType = "application/json",
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody, Encoding.UTF8, mediaType) };
        }
    }
}
