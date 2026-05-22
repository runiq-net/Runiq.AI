using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Runiq.Agents.Providers.OpenAI
{
    /// <summary>
    /// OpenAI-compatible chat completion endpointleriyle konuşan HTTP client sorumluluğunu taşır.
    /// </summary>
    public sealed class OpenAICompatibleClient
    {

        private readonly HttpClient httpClient;

        public OpenAICompatibleClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Agent girdisini OpenAI-compatible chat completions formatıyla modele gönderir.
        /// </summary>
        public async Task<AgentExecutionResult> ExecuteAsync(
            Agent agent,
            Uri endpoint,
            string input,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(agent);
            ArgumentNullException.ThrowIfNull(endpoint);

            try
            {
                var requestUrl = BuildChatCompletionsUrl(endpoint);

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

                if (!string.IsNullOrWhiteSpace(agent.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.ApiKey);
                }

                request.Content = JsonContent.Create(
                    new OpenAIChatCompletionRequest(
                        Model: agent.ModelName,
                        Stream: null,
                        Messages:
                        [
                            new OpenAIChatMessage("system", agent.Instructions),
                            new OpenAIChatMessage("user", input)
                        ]),
                    options: JsonOptions);

                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    return AgentExecutionResult.Failure(
                        errorCode: "ProviderRequestFailed",
                        errorMessage: $"Provider request failed with status code {(int)response.StatusCode}. {errorBody}");
                }

                var completion = await response.Content.ReadFromJsonAsync<OpenAIChatCompletionResponse>(
                    JsonOptions,
                    cancellationToken);

                var message = completion?
                    .Choices?
                    .FirstOrDefault()?
                    .Message?
                    .Content;

                if (string.IsNullOrWhiteSpace(message))
                {
                    return AgentExecutionResult.Failure(
                        errorCode: "ProviderResponseInvalid",
                        errorMessage: "Provider response did not contain an assistant message.");
                }

                return AgentExecutionResult.Success(message);
            }
            catch (OperationCanceledException)
            {
                return AgentExecutionResult.Failure(
                    errorCode: "ProviderRequestCanceled",
                    errorMessage: "Provider request was canceled.");
            }
            catch (Exception ex)
            {
                return AgentExecutionResult.Failure(
                    errorCode: "ProviderRequestFailed",
                    errorMessage: ex.Message);
            }
        }

        /// <summary>
        /// Agent cevabını OpenAI-compatible stream formatı üzerinden parça parça üretir.
        /// </summary>
        /// <summary>
        /// Agent cevabını OpenAI-compatible stream formatı üzerinden parça parça üretir.
        /// </summary>
        public  async IAsyncEnumerable<AgentExecutionEvent> StreamAsync(
            Agent agent,
            Uri endpoint,
            string input,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(agent);
            ArgumentNullException.ThrowIfNull(endpoint);

            var startedAt = Stopwatch.GetTimestamp();

            var requestUrl = BuildChatCompletionsUrl(endpoint);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            if (!string.IsNullOrWhiteSpace(agent.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.ApiKey);
            }

            request.Content = JsonContent.Create(
                new OpenAIChatCompletionRequest(
                    Model: agent.ModelName,
                    Stream: true,
                    Messages:
                    [
                        new OpenAIChatMessage("system", agent.Instructions),
                new OpenAIChatMessage("user", input)
                    ]),
                options: JsonOptions);

    
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

   
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                yield return AgentExecutionEvent.Failed(
                           errorCode: "ProviderRequestFailed",
                           errorMessage: $"Provider request failed with status code {(int)response.StatusCode}. {errorBody}");

                yield return AgentExecutionEvent.Completed();
                yield break;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(responseStream);

            var firstContentLogged = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line is null)
                {
                    yield return AgentExecutionEvent.Completed();
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

                if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
                {
                    yield return AgentExecutionEvent.Completed();
                    yield break;
                }

                var content = TryReadDeltaContent(data);

                if (string.IsNullOrEmpty(content))               
                    continue;
                

                if (!firstContentLogged)          
                    firstContentLogged = true;                

                yield return AgentExecutionEvent.AssistantDelta(content);
            }
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

        /// <summary>
        /// OpenAI-compatible stream satırındaki delta content değerini okur.
        /// </summary>
        private static string? TryReadDeltaContent(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);

                var root = document.RootElement;

                if (!root.TryGetProperty("choices", out var choices))
                {
                    return null;
                }

                if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                {
                    return null;
                }

                var firstChoice = choices[0];

                if (!firstChoice.TryGetProperty("delta", out var delta))
                {
                    return null;
                }

                if (!delta.TryGetProperty("content", out var content))
                {
                    return null;
                }

                return content.GetString();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private sealed record OpenAIChatCompletionRequest(
            string Model,
            bool? Stream,
            IReadOnlyList<OpenAIChatMessage> Messages);

        private sealed record OpenAIChatMessage(
            string Role,
            string Content);

        private sealed class OpenAIChatCompletionResponse
        {
            public IReadOnlyList<OpenAIChatCompletionChoice>? Choices { get; set; }
        }

        private sealed class OpenAIChatCompletionChoice
        {
            public OpenAIChatCompletionMessage? Message { get; set; }
        }

        private sealed class OpenAIChatCompletionMessage
        {
            public string? Content { get; set; }
        }
    }
}