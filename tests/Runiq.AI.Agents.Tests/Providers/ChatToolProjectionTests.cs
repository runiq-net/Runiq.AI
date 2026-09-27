using System.Net;
using System.Text;
using System.Text.Json;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Agents.Tests.Providers;

public sealed class ChatToolProjectionTests
{
    [Theory]
    [InlineData(false, false, "checking")]
    [InlineData(false, true, "checking")]
    [InlineData(true, false, "checking")]
    [InlineData(true, true, "checking")]
    [InlineData(true, false, "")]
    [InlineData(true, true, "")]
    // Both wire protocols preserve complete projected tool groups without requiring hidden server-side history.
    public async Task SelfContainedProjection_PreservesCallsAndResults(bool responses, bool streaming, string assistantText)
    {
        var handler = new CapturingHandler(responses, streaming);
        using var http = new HttpClient(handler);
        IChatClient client = responses ? new OpenAIResponsesClient(http) : new OpenAICompatibleClient(http);
        var request = new ChatRequest(ModelReference.Parse("openai/gpt-4.1"),
            [new(ChatRole.System, "instructions"), new(ChatRole.User, "question"),
                new(ChatRole.Assistant, assistantText, ToolCalls: [new("a", "lookup", "{ \"id\": 1 }"), new("b", "other", "{}")]),
                new(ChatRole.Tool, "{ \"value\": 42 }", "a"), new(ChatRole.Tool, "{\"ok\":false}", "b"),
                new(ChatRole.User, "continue")], new Uri("https://example.test/v1"), "key");
        if (streaming)
            await foreach (var _ in client.CompleteStreamingAsync(request)) { }
        else
            await client.CompleteAsync(request);

        using var document = JsonDocument.Parse(handler.Body!);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("previous_response_id", out _));
        if (responses)
        {
            Assert.Equal("instructions", root.GetProperty("instructions").GetString());
            var input = root.GetProperty("input").EnumerateArray().ToArray();
            Assert.Equal("question", input[0].GetProperty("content").GetString());
            var callIndex = assistantText.Length == 0 ? 1 : 2;
            if (assistantText.Length > 0) Assert.Equal(assistantText, input[1].GetProperty("content").GetString());
            AssertCall(input[callIndex], "a", "lookup", "{ \"id\": 1 }");
            AssertCall(input[callIndex + 1], "b", "other", "{}");
            Assert.Equal("function_call_output", input[callIndex + 2].GetProperty("type").GetString());
            Assert.Equal("a", input[callIndex + 2].GetProperty("call_id").GetString());
            Assert.Equal("{ \"value\": 42 }", input[callIndex + 2].GetProperty("output").GetString());
            Assert.Equal("b", input[callIndex + 3].GetProperty("call_id").GetString());
            Assert.Equal("{\"ok\":false}", input[callIndex + 3].GetProperty("output").GetString());
            Assert.Equal("continue", input[^1].GetProperty("content").GetString());
            Assert.Equal(callIndex + 5, input.Length);
        }
        else
        {
            var messages = root.GetProperty("messages");
            Assert.Equal(6, messages.GetArrayLength());
            Assert.Equal(assistantText, messages[2].GetProperty("content").GetString());
            var calls = messages[2].GetProperty("tool_calls");
            Assert.Equal(2, calls.GetArrayLength());
            Assert.Equal("a", calls[0].GetProperty("id").GetString());
            Assert.Equal("lookup", calls[0].GetProperty("function").GetProperty("name").GetString());
            Assert.Equal("{ \"id\": 1 }", calls[0].GetProperty("function").GetProperty("arguments").GetString());
            Assert.Equal("b", calls[1].GetProperty("id").GetString());
            Assert.Equal("other", calls[1].GetProperty("function").GetProperty("name").GetString());
            Assert.Equal("{}", calls[1].GetProperty("function").GetProperty("arguments").GetString());
            Assert.Equal("a", messages[3].GetProperty("tool_call_id").GetString());
            Assert.Equal("{ \"value\": 42 }", messages[3].GetProperty("content").GetString());
            Assert.Equal("b", messages[4].GetProperty("tool_call_id").GetString());
            Assert.Equal("{\"ok\":false}", messages[4].GetProperty("content").GetString());
            Assert.Equal("continue", messages[5].GetProperty("content").GetString());
        }
    }

    private static void AssertCall(JsonElement item, string id, string name, string arguments)
    {
        Assert.Equal("function_call", item.GetProperty("type").GetString());
        Assert.Equal(id, item.GetProperty("call_id").GetString());
        Assert.Equal(name, item.GetProperty("name").GetString());
        Assert.Equal(arguments, item.GetProperty("arguments").GetString());
    }

    private sealed class CapturingHandler(bool responses, bool streaming) : HttpMessageHandler
    {
        internal string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var body = streaming ? "data: [DONE]\n\n" : responses ? "{\"output_text\":\"answer\"}" :
                "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"answer\"},\"finish_reason\":\"stop\"}]}";
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, streaming ? "text/event-stream" : "application/json") };
        }
    }
}
