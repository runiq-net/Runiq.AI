using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Runiq.AI.Core.AI.Embeddings;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Services;

internal sealed class OpenAiEmbeddingClient : IEmbeddingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly HttpClient httpClient;
    private readonly string apiKey;

    public OpenAiEmbeddingClient(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("The OpenAI API key is not configured.");
    }

    public async Task<EmbeddingResponse> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        using var message = new HttpRequestMessage(HttpMethod.Post, "embeddings");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = JsonContent.Create(new OpenAiEmbeddingRequest(
            request.Model.ModelName,
            request.Inputs,
            request.Dimensions), options: JsonOptions);

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI embedding request failed with status code {(int)response.StatusCode}.");
        }

        var payload = await response.Content
            .ReadFromJsonAsync<OpenAiEmbeddingResponse>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("OpenAI returned an empty embedding response.");

        var results = payload.Data
            .OrderBy(item => item.Index)
            .Select(item => new EmbeddingResult(item.Index, item.Embedding, item.Embedding.Count))
            .ToArray();

        if (results.Length != request.Inputs.Count)
        {
            throw new InvalidOperationException("OpenAI returned an unexpected number of embeddings.");
        }

        return new EmbeddingResponse(
            results,
            payload.Usage is null
                ? null
                : new EmbeddingUsage(payload.Usage.PromptTokens, payload.Usage.TotalTokens));
    }

    private sealed record OpenAiEmbeddingRequest(
        string Model,
        IReadOnlyList<string> Input,
        int? Dimensions);

    private sealed record OpenAiEmbeddingResponse(
        IReadOnlyList<OpenAiEmbeddingData> Data,
        OpenAiEmbeddingUsage? Usage);

    private sealed record OpenAiEmbeddingData(
        int Index,
        IReadOnlyList<float> Embedding);

    private sealed record OpenAiEmbeddingUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens);
}
