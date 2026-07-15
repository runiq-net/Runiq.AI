using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Providers;

namespace Runiq.AI.Agents.Providers.OpenAI;

/// <summary>
/// Carries HTTP client responsibilities for OpenAI-compatible chat completion endpoints.
/// </summary>
public sealed class OpenAICompatibleClient : IChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAICompatibleClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for provider calls.</param>
    public OpenAICompatibleClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public async Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        using var httpRequest = CreateHttpRequest(request, BuildChatCompletionsUrl(ResolveEndpoint(request)), stream: null);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateProviderExceptionAsync(request, response, cancellationToken).ConfigureAwait(false);
        }

        var completion = await response.Content.ReadFromJsonAsync<OpenAIChatCompletionResponse>(
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        var choice = completion?.Choices?.FirstOrDefault();
        var toolCalls = choice?.Message?.ToolCalls?.Select(MapToolCall).ToArray() ?? [];
        var message = choice?.Message?.Content ?? string.Empty;

        if (string.IsNullOrWhiteSpace(message) && toolCalls.Length == 0)
        {
            throw new ChatProviderException(
                ProviderErrorKind.MalformedProviderResponse,
                "Provider response did not contain an assistant message.",
                request.Model.ProviderName);
        }

        return new ChatResponse(
            new ChatMessage(ChatRole.Assistant, message, ToolCalls: toolCalls.Length == 0 ? null : toolCalls),
            MapFinishReason(choice?.FinishReason),
            MapUsage(completion?.Usage));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        using var httpRequest = CreateHttpRequest(request, BuildChatCompletionsUrl(ResolveEndpoint(request)), stream: true);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateProviderExceptionAsync(request, response, cancellationToken).ConfigureAwait(false);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(responseStream);
        var pendingToolCalls = new Dictionary<int, PendingToolCall>();
        var finishReason = ChatFinishReason.Unknown;
        ChatUsage? usage = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                foreach (var toolCall in MapPendingToolCalls(pendingToolCalls))
                    yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: toolCall);
                yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.Completed, FinishReason: finishReason, Usage: usage);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line) ||
                !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();

            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var toolCall in MapPendingToolCalls(pendingToolCalls))
                    yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: toolCall);
                yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.Completed, FinishReason: finishReason, Usage: usage);
                yield break;
            }

            var chunk = ReadStreamingChunk(data);

            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.ContentDelta, ContentDelta: chunk.Content);
            }
            finishReason = chunk.FinishReason ?? finishReason;
            usage = chunk.Usage ?? usage;
            AppendToolCallDeltas(pendingToolCalls, chunk.ToolCallDeltas);
        }
    }

    private static HttpRequestMessage CreateHttpRequest(
        ChatRequest request,
        Uri requestUrl,
        bool? stream)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        }

        httpRequest.Content = JsonContent.Create(
            new OpenAIChatCompletionRequest(
                Model: request.Model.ModelName,
                Stream: stream,
                Messages: request.Messages.Select(MapMessage).ToArray(),
                Tools: request.Tools?.Select(MapToolDefinition).ToArray(),
                ResponseFormat: MapResponseFormat(request.ResponseFormat)),
            options: JsonOptions);

        return httpRequest;
    }

    private static Uri ResolveEndpoint(ChatRequest request)
    {
        return request.ProviderEndpoint
            ?? ProviderDefaults.ResolveUrl(request.Model.ProviderName, request.Model.ModelName, configuredUrl: null);
    }

    private static OpenAIChatMessage MapMessage(ChatMessage message)
    {
        return new OpenAIChatMessage(
            MapRole(message.Role),
            message.Content,
            message.ToolCallId,
            message.ToolCalls?.Select(call => new OpenAIToolCall(
                call.Id,
                "function",
                new OpenAIToolCallFunction(call.Name, call.ArgumentsJson))).ToArray());
    }

    private static OpenAIToolDefinition MapToolDefinition(ChatToolDefinition tool) =>
        new("function", new OpenAIFunctionDefinition(tool.Name, tool.Description, ParseJson(tool.ParametersJsonSchema)));

    private static OpenAIResponseFormat? MapResponseFormat(ChatResponseFormat? format) => format is null
        ? null
        : new("json_schema", new OpenAIJsonSchema(format.Name, ParseJson(format.JsonSchema), true));

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static ChatToolCall MapToolCall(OpenAIToolCall call) =>
        new(call.Id ?? string.Empty, call.Function?.Name ?? string.Empty, call.Function?.Arguments ?? "{}");

    private static string MapRole(ChatRole role)
    {
        return role switch
        {
            ChatRole.System => "system",
            ChatRole.User => "user",
            ChatRole.Assistant => "assistant",
            ChatRole.Tool => "tool",
            _ => "user"
        };
    }

    private static ChatFinishReason MapFinishReason(string? finishReason)
    {
        return finishReason switch
        {
            "stop" => ChatFinishReason.Stop,
            "length" => ChatFinishReason.Length,
            "tool_calls" => ChatFinishReason.ToolCalls,
            "content_filter" => ChatFinishReason.ContentFilter,
            _ => ChatFinishReason.Unknown
        };
    }

    private static ChatUsage? MapUsage(OpenAIChatCompletionUsage? usage)
    {
        return usage is null
            ? null
            : new ChatUsage(usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
    }

    private static async Task<ChatProviderException> CreateProviderExceptionAsync(
        ChatRequest request,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var kind = (int)response.StatusCode switch
        {
            400 => ProviderErrorKind.InvalidRequest,
            401 or 403 => ProviderErrorKind.AuthenticationFailure,
            404 => ProviderErrorKind.ModelNotFound,
            408 => ProviderErrorKind.Timeout,
            429 => ProviderErrorKind.RateLimited,
            >= 500 and <= 599 => ProviderErrorKind.ProviderUnavailable,
            _ => ProviderErrorKind.Unknown
        };

        return new ChatProviderException(
            kind,
            $"Provider request failed with status code {(int)response.StatusCode}. {errorBody}",
            request.Model.ProviderName,
            (int)response.StatusCode);
    }

    private static Uri BuildChatCompletionsUrl(Uri endpoint)
    {
        var baseUrl = endpoint.ToString().TrimEnd('/');

        if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(baseUrl);
        }

        return new Uri($"{baseUrl}/chat/completions");
    }

    private static StreamingChunk ReadStreamingChunk(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;

            if (!root.TryGetProperty("choices", out var choices))
            {
                return new();
            }

            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return new();
            }

            var firstChoice = choices[0];

            var content = firstChoice.TryGetProperty("delta", out var delta) &&
                          delta.TryGetProperty("content", out var contentElement)
                ? contentElement.GetString()
                : null;
            var deltas = new List<ToolCallDelta>();
            if (delta.ValueKind == JsonValueKind.Object && delta.TryGetProperty("tool_calls", out var calls))
            {
                foreach (var call in calls.EnumerateArray())
                {
                    var index = call.TryGetProperty("index", out var indexElement) ? indexElement.GetInt32() : 0;
                    var id = call.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                    var function = call.TryGetProperty("function", out var functionElement) ? functionElement : default;
                    var name = function.ValueKind == JsonValueKind.Object && function.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    var arguments = function.ValueKind == JsonValueKind.Object && function.TryGetProperty("arguments", out var argumentsElement) ? argumentsElement.GetString() : null;
                    deltas.Add(new ToolCallDelta(index, id, name, arguments));
                }
            }
            ChatFinishReason? reason = firstChoice.TryGetProperty("finish_reason", out var reasonElement)
                ? MapFinishReason(reasonElement.GetString())
                : null;
            ChatUsage? usage = null;
            if (root.TryGetProperty("usage", out var usageElement) && usageElement.ValueKind == JsonValueKind.Object)
                usage = new ChatUsage(ReadInt(usageElement, "prompt_tokens"), ReadInt(usageElement, "completion_tokens"), ReadInt(usageElement, "total_tokens"));
            return new StreamingChunk(content, deltas, reason, usage);
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;

    private static void AppendToolCallDeltas(Dictionary<int, PendingToolCall> pending, IReadOnlyList<ToolCallDelta> deltas)
    {
        foreach (var delta in deltas)
        {
            if (!pending.TryGetValue(delta.Index, out var call))
                pending[delta.Index] = call = new PendingToolCall();
            call.Id = delta.Id ?? call.Id;
            call.Name = delta.Name ?? call.Name;
            call.Arguments += delta.Arguments;
        }
    }

    private static IEnumerable<ChatToolCall> MapPendingToolCalls(Dictionary<int, PendingToolCall> pending) =>
        pending.OrderBy(pair => pair.Key).Select(pair => new ChatToolCall(pair.Value.Id ?? string.Empty, pair.Value.Name ?? string.Empty, pair.Value.Arguments));

    private sealed record OpenAIChatCompletionRequest(
        string Model,
        bool? Stream,
        IReadOnlyList<OpenAIChatMessage> Messages,
        IReadOnlyList<OpenAIToolDefinition>? Tools,
        [property: JsonPropertyName("response_format")] OpenAIResponseFormat? ResponseFormat);

    private sealed record OpenAIChatMessage(
        string Role,
        string Content,
        [property: JsonPropertyName("tool_call_id")] string? ToolCallId,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<OpenAIToolCall>? ToolCalls);

    private sealed record OpenAIToolDefinition(string Type, OpenAIFunctionDefinition Function);
    private sealed record OpenAIFunctionDefinition(string Name, string Description, JsonElement Parameters);
    private sealed record OpenAIResponseFormat(string Type, [property: JsonPropertyName("json_schema")] OpenAIJsonSchema JsonSchema);
    private sealed record OpenAIJsonSchema(string Name, JsonElement Schema, bool Strict);
    private sealed record OpenAIToolCall(string? Id, string Type, OpenAIToolCallFunction? Function);
    private sealed record OpenAIToolCallFunction(string? Name, string? Arguments);

    private sealed class OpenAIChatCompletionResponse
    {
        public IReadOnlyList<OpenAIChatCompletionChoice>? Choices { get; set; }

        public OpenAIChatCompletionUsage? Usage { get; set; }
    }

    private sealed class OpenAIChatCompletionChoice
    {
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        public OpenAIChatCompletionMessage? Message { get; set; }
    }

    private sealed class OpenAIChatCompletionMessage
    {
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public IReadOnlyList<OpenAIToolCall>? ToolCalls { get; set; }
    }

    private sealed class OpenAIChatCompletionUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }

    private sealed record StreamingChunk(string? Content = null, IReadOnlyList<ToolCallDelta>? Deltas = null, ChatFinishReason? FinishReason = null, ChatUsage? Usage = null)
    {
        public IReadOnlyList<ToolCallDelta> ToolCallDeltas => Deltas ?? [];
    }
    private sealed record ToolCallDelta(int Index, string? Id, string? Name, string? Arguments);
    private sealed class PendingToolCall
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string Arguments { get; set; } = string.Empty;
    }
}
