using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.AI.Embeddings;

namespace Runiq.AI.Core.AI.Capabilities;

/// <summary>Performs deterministic preflight validation for provider-neutral model requests.</summary>
public static class ModelCapabilityValidator
{
    /// <summary>Validates a chat request before a non-streaming invocation.</summary>
    public static void ValidateChat(AiModelDescriptor descriptor, ChatRequest request) => Validate(descriptor, request, false);
    /// <summary>Validates a chat request before a streaming invocation.</summary>
    public static void ValidateStreaming(AiModelDescriptor descriptor, ChatRequest request) => Validate(descriptor, request, true);
    /// <summary>Validates an embedding request before invocation.</summary>
    public static void ValidateEmbedding(AiModelDescriptor descriptor, EmbeddingRequest request)
    {
        Require(descriptor, ModelCapability.Embeddings, "embedding");
        if (request.Dimensions is not null && descriptor.EmbeddingDimensions is not null && request.Dimensions != descriptor.EmbeddingDimensions)
            throw new ArgumentException($"Requested embedding dimensions '{request.Dimensions}' do not match configured dimensions '{descriptor.EmbeddingDimensions}'.", nameof(request));
    }

    private static void Validate(AiModelDescriptor descriptor, ChatRequest request, bool streaming)
    {
        Require(descriptor, ModelCapability.Chat, streaming ? "streaming chat" : "chat");
        if (streaming) Require(descriptor, ModelCapability.Streaming, "streaming chat");
        if (request.Tools is { Count: > 0 }) Require(descriptor, ModelCapability.ToolCalling, "chat with tools");
        if (request.ResponseFormat is not null) Require(descriptor, ModelCapability.StructuredOutput, "structured output");
    }

    private static void Require(AiModelDescriptor descriptor, ModelCapability capability, string operation)
    {
        if (!descriptor.Supports(capability)) throw new UnsupportedModelCapabilityException(descriptor.Model, capability, operation);
    }
}
