using Runiq.Agents.Tools;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Runiq.Agents.Providers.OpenAI;

/// <summary>
/// OpenAI Responses API üzerinden agent cevabı üreten native OpenAI istemcisidir.
/// </summary>
internal static class OpenAIResponsesClient
{
    private static readonly HttpClient HttpClient = new();
    private static readonly NullabilityInfoContext NullabilityContext = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<AgentExecutionResult> ExecuteAsync(
        Agent agent,
        Uri endpoint,
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(endpoint);

        var requestUrl = BuildResponsesUrl(endpoint);

        using var request = CreateRequest(
            agent,
            requestUrl,
            new OpenAIResponseRequest(
                Model: agent.ModelName,
                Instructions: agent.Instructions,
                Input: input,
                Stream: false,
                Reasoning: new OpenAIReasoningOptions(Effort: agent.ReasoningEffort),
                Text: new OpenAITextOptions(Verbosity: agent.Verbosity)
                ));

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return AgentExecutionResult.Failure(
                errorCode: "OpenAIResponsesRequestFailed",
                errorMessage: $"Provider request failed with status code {(int)response.StatusCode}. {body}");
        }

        var message = TryReadOutputText(body);

        return string.IsNullOrWhiteSpace(message)
            ? AgentExecutionResult.Failure(
                errorCode: "OpenAIResponsesEmptyMessage",
                errorMessage: "Provider returned an empty response.")
            : AgentExecutionResult.Success(message);
    }

    /// <summary>
    /// OpenAI Responses API stream çıktısını parça parça üretir.
    /// </summary>
    public static async IAsyncEnumerable<AgentExecutionEvent> StreamAsync(
        Agent agent,
        Uri endpoint,
        string input,
         AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(endpoint);

        var startedAt = Stopwatch.GetTimestamp();
        var requestUrl = BuildResponsesUrl(endpoint);
        var toolDefinitions = CreateToolDefinitions(agent);

        if (toolDefinitions is { Count: > 0 })
        {
            Console.WriteLine(
                $"[responses-tools] {JsonSerializer.Serialize(toolDefinitions, JsonOptions)}");
        }

        using var request = CreateRequest(
            agent,
            requestUrl,
            new OpenAIResponseRequest(
                Model: agent.ModelName,
                Instructions: agent.Instructions,
                Input: input,
                Stream: true,
                Reasoning: new OpenAIReasoningOptions(Effort: agent.ReasoningEffort),
                Text: new OpenAITextOptions(Verbosity: agent.Verbosity),
                Tools: toolDefinitions));

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        Console.WriteLine(
            $"[responses-stream] headers received in {Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:N0} ms. status={(int)response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            yield return AgentExecutionEvent.Failed(
                $"Provider request failed with status code {(int)response.StatusCode}. {errorBody}");
            yield break;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        var firstContentLogged = false;
        var hasAnyRuntimeEvent = false;
        string? responseId = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();

            responseId ??= TryReadResponseId(data);

            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"[responses-stream] done in {Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:N0} ms");

                break;
            }

            Console.WriteLine($"[responses-stream-raw] {data}");

            var functionCall = TryReadCompletedFunctionCall(data);

            if (functionCall is not null)
            {
                hasAnyRuntimeEvent = true;

                Console.WriteLine(
                    $"[responses-tool-call] name={functionCall.ToolName}, callId={functionCall.ToolCallId}, arguments={functionCall.ArgumentsJson}");

                yield return AgentExecutionEvent.ToolCallStarted(
                    functionCall.ToolCallId,
                    functionCall.ToolName,
                    functionCall.ArgumentsJson);

                if (toolInvoker is null)
                {
                    yield return AgentExecutionEvent.ToolCallFailed(
                        functionCall.ToolCallId,
                        functionCall.ToolName,
                        "Tool invoker is not available.");

                    continue;
                }

                var toolResult = await toolInvoker.InvokeAsync(
                    agent,
                    functionCall.ToolName,
                    functionCall.ArgumentsJson,
                    cancellationToken);

                if (!toolResult.IsSuccess)
                {
                    yield return AgentExecutionEvent.ToolCallFailed(
                        functionCall.ToolCallId,
                        functionCall.ToolName,
                        toolResult.ErrorMessage ?? "Tool execution failed.",
                        toolResult.ErrorCode);

                    continue;
                }

                Console.WriteLine(
                    $"[responses-tool-result] name={functionCall.ToolName}, callId={functionCall.ToolCallId}, output={toolResult.OutputJson}");

                var outputJson = toolResult.OutputJson ?? "{}";

                yield return AgentExecutionEvent.ToolCallCompleted(
                    functionCall.ToolCallId,
                    functionCall.ToolName,
                    outputJson);

                if (string.IsNullOrWhiteSpace(responseId))
                {
                    yield return AgentExecutionEvent.ToolCallFailed(
                        functionCall.ToolCallId,
                        functionCall.ToolName,
                        "OpenAIResponseIdMissing",
                        "OpenAI response id could not be read before submitting tool output.");

                    continue;
                }

                await foreach (var followUpEvent in StreamToolOutputAsync(
                                   agent,
                                   requestUrl,
                                   previousResponseId: responseId,
                                   toolCallId: functionCall.ToolCallId,
                                   outputJson: outputJson,
                                   cancellationToken))
                {
                    hasAnyRuntimeEvent = true;
                    yield return followUpEvent;
                }

                continue;
            }

            var content = TryReadStreamTextDelta(data);

            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            if (!firstContentLogged)
            {
                firstContentLogged = true;

                Console.WriteLine(
                    $"[responses-stream] first content received in {Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:N0} ms");
            }

            hasAnyRuntimeEvent = true;
            yield return AgentExecutionEvent.AssistantDelta(content);
        }

        yield return hasAnyRuntimeEvent
            ? AgentExecutionEvent.Completed()
            : AgentExecutionEvent.Failed("OpenAI Responses stream completed without producing content.");
    }

    private static HttpRequestMessage CreateRequest(
        Agent agent,
        Uri requestUrl,
        OpenAIResponseRequest payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        if (!string.IsNullOrWhiteSpace(agent.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.ApiKey);
        }

        request.Content = JsonContent.Create(payload, options: JsonOptions);

        return request;
    }

    private static HttpRequestMessage CreateToolOutputRequest(
    Agent agent,
    Uri requestUrl,
    string previousResponseId,
    string toolCallId,
    string outputJson)
    {
        return CreateRequest(
            agent,
            requestUrl,
            new OpenAIResponseRequest(
                Model: agent.ModelName,
                Instructions: agent.Instructions,
                Input: new object[]
                {
                new OpenAIFunctionCallOutputInput(
                    Type: "function_call_output",
                    CallId: toolCallId,
                    Output: outputJson)
                },
                Stream: true,
                Reasoning: new OpenAIReasoningOptions(Effort: agent.ReasoningEffort),
                Text: new OpenAITextOptions(Verbosity: agent.Verbosity),
                PreviousResponseId: previousResponseId));
    }


    private static async IAsyncEnumerable<AgentExecutionEvent> StreamToolOutputAsync(
    Agent agent,
    Uri requestUrl,
    string previousResponseId,
    string toolCallId,
    string outputJson,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = CreateToolOutputRequest(
            agent,
            requestUrl,
            previousResponseId,
            toolCallId,
            outputJson);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            yield return AgentExecutionEvent.Failed(
                    $"OpenAIToolOutputRequestFailed: Tool output request failed with status code " +
                    $"{(int)response.StatusCode}. {errorBody}");

            yield break;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        var hasAnyContent = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();

            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var content = TryReadStreamTextDelta(data);

            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            hasAnyContent = true;

            yield return AgentExecutionEvent.AssistantDelta(content);
        }

        if (!hasAnyContent)
        {
            yield return AgentExecutionEvent.Failed(
                "OpenAIToolOutputEmptyStream: OpenAI tool output " +
                "stream completed without producing content.");
        }
    }

    private static Uri BuildResponsesUrl(Uri endpoint)
    {
        var endpointValue = endpoint.ToString().TrimEnd('/');

        return new Uri($"{endpointValue}/responses");
    }

    private static string? TryReadStreamTextDelta(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                return null;
            }

            var eventType = typeElement.GetString();

            if (!string.Equals(eventType, "response.output_text.delta", StringComparison.Ordinal))
            {
                return null;
            }

            if (!root.TryGetProperty("delta", out var deltaElement))
            {
                return null;
            }

            return deltaElement.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }


    private static OpenAIFunctionCall? TryReadCompletedFunctionCall(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement) ||
                typeElement.GetString() != "response.output_item.done")
            {
                return null;
            }

            if (!root.TryGetProperty("item", out var itemElement))
            {
                return null;
            }

            if (!itemElement.TryGetProperty("type", out var itemTypeElement) ||
                itemTypeElement.GetString() != "function_call")
            {
                return null;
            }

            if (!itemElement.TryGetProperty("status", out var statusElement) ||
                statusElement.GetString() != "completed")
            {
                return null;
            }

            var toolCallId = itemElement.TryGetProperty("call_id", out var callIdElement)
                ? callIdElement.GetString()
                : null;

            var toolName = itemElement.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;

            var argumentsJson = itemElement.TryGetProperty("arguments", out var argumentsElement)
                ? argumentsElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(toolCallId) ||
                string.IsNullOrWhiteSpace(toolName))
            {
                return null;
            }

            return new OpenAIFunctionCall(
                ToolCallId: toolCallId,
                ToolName: toolName,
                ArgumentsJson: string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadOutputText(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;

            if (root.TryGetProperty("output_text", out var outputTextElement))
            {
                return outputTextElement.GetString();
            }

            if (!root.TryGetProperty("output", out var outputElement) ||
                outputElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var parts = new List<string>();

            foreach (var outputItem in outputElement.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out var contentElement) ||
                    contentElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in contentElement.EnumerateArray())
                {
                    if (!contentItem.TryGetProperty("type", out var typeElement))
                    {
                        continue;
                    }

                    var type = typeElement.GetString();

                    if (!string.Equals(type, "output_text", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (contentItem.TryGetProperty("text", out var textElement))
                    {
                        parts.Add(textElement.GetString() ?? string.Empty);
                    }
                }
            }

            return parts.Count == 0
                ? null
                : string.Concat(parts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadResponseId(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("response", out var responseElement) &&
                responseElement.ValueKind == JsonValueKind.Object &&
                responseElement.TryGetProperty("id", out var responseIdElement))
            {
                return responseIdElement.GetString();
            }

            if (root.TryGetProperty("id", out var idElement))
            {
                var id = idElement.GetString();

                return string.IsNullOrWhiteSpace(id) ||
                       !id.StartsWith("resp_", StringComparison.Ordinal)
                    ? null
                    : id;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<OpenAIToolDefinition>? CreateToolDefinitions(Agent agent)
    {
        if (agent.Tools.Count == 0)
        {
            return null;
        }

        return agent.Tools
            .Select(CreateToolDefinition)
            .ToList();
    }

    private static OpenAIToolDefinition CreateToolDefinition(AgentToolRegistration tool)
    {
        return new OpenAIToolDefinition(
            Type: "function",
            Name: tool.Name,
            Description: CreateToolDescription(tool),
            Parameters: CreateToolParameters(tool.InputType));
    }

    private static string CreateToolDescription(AgentToolRegistration tool)
    {
        return string.IsNullOrWhiteSpace(tool.Description)
            ? $"Executes the {tool.Name} tool."
            : tool.Description;
    }

    private static OpenAIToolParameters CreateToolParameters(Type inputType)
    {
        var properties = new Dictionary<string, OpenAIToolProperty>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var property in GetSerializableProperties(inputType))
        {
            var propertyName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);

            properties[propertyName] = new OpenAIToolProperty(
                Type: CreateJsonSchemaType(property.PropertyType));

            if (IsRequiredProperty(property))
            {
                required.Add(propertyName);
            }
        }

        return new OpenAIToolParameters(
            Type: "object",
            Properties: properties,
            Required: required);
    }

    private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.GetMethod is not null &&
                property.GetMethod.IsPublic &&
                property.GetCustomAttribute<JsonIgnoreAttribute>() is null);
    }

    private static bool IsRequiredProperty(PropertyInfo property)
    {
        if (property.GetCustomAttributes()
            .Any(attribute => attribute.GetType().Name == "RequiredAttribute"))
        {
            return true;
        }

        var propertyType = property.PropertyType;

        if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null)
        {
            return true;
        }

        var nullabilityInfo = NullabilityContext.Create(property);

        return nullabilityInfo.ReadState == NullabilityState.NotNull;
    }

    private static string CreateJsonSchemaType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;

        if (actualType == typeof(string) ||
            actualType == typeof(Guid) ||
            actualType == typeof(DateTime) ||
            actualType == typeof(DateTimeOffset))
        {
            return "string";
        }

        if (actualType == typeof(int) ||
            actualType == typeof(long) ||
            actualType == typeof(short) ||
            actualType == typeof(byte))
        {
            return "integer";
        }

        if (actualType == typeof(decimal) ||
            actualType == typeof(double) ||
            actualType == typeof(float))
        {
            return "number";
        }

        if (actualType == typeof(bool))
        {
            return "boolean";
        }

        return "string";
    }

    private sealed record OpenAIFunctionCall(
        string ToolCallId,
        string ToolName,
        string ArgumentsJson);

    private sealed record OpenAIFunctionCallOutputInput(
    string Type,
    [property: JsonPropertyName("call_id")]
    string CallId,
    string Output);

    private sealed record OpenAIResponseRequest(
        string Model,
        string Instructions,
        object Input,
        bool Stream,
        OpenAIReasoningOptions? Reasoning,
        OpenAITextOptions? Text,
        IReadOnlyList<OpenAIToolDefinition>? Tools = null,
        [property: JsonPropertyName("previous_response_id")]
        string? PreviousResponseId = null);

    private sealed record OpenAIReasoningOptions(
        string Effort);

    private sealed record OpenAITextOptions(
        string Verbosity);

    private sealed record OpenAIToolDefinition(
        string Type,
        string Name,
        string Description,
        OpenAIToolParameters Parameters);

    private sealed record OpenAIToolParameters(
        string Type,
        IReadOnlyDictionary<string, OpenAIToolProperty> Properties,
        IReadOnlyList<string> Required);

    private sealed record OpenAIToolProperty(
        string Type);
}
