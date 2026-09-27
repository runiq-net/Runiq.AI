using System.Text.Json;
using System.Text.Json.Serialization;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Serialization;

/// <summary>Defines the strict version-one JSON wire format independently of provider schema migrations.</summary>
public static class MemoryMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true
    };

    /// <summary>Gets the only format supported by this initial release; no pre-release formats are supported.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Serializes all message fields without changing timestamps or tool argument text.</summary>
    /// <param name="message">The validated neutral message.</param>
    /// <returns>The version-one JSON payload.</returns>
    public static string Serialize(MemoryMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.Serialize(message, Options);
    }

    /// <summary>Reads a supported payload, rejecting unknown fields, missing fields, and invalid values.</summary>
    /// <param name="payload">The complete JSON message.</param>
    /// <param name="version">The independently stored payload format version.</param>
    /// <returns>A validated immutable message snapshot.</returns>
    /// <exception cref="MemoryStoreException">The payload is malformed or its format is unsupported.</exception>
    public static MemoryMessage Deserialize(string payload, int version)
    {
        if (version != CurrentVersion) throw new MemoryStoreException(MemoryStoreError.InvalidPayload);
        try
        {
            return JsonSerializer.Deserialize<MemoryMessage>(payload, Options)
                ?? throw new MemoryStoreException(MemoryStoreError.InvalidPayload);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new MemoryStoreException(MemoryStoreError.InvalidPayload, exception);
        }
    }

    // Stable equality includes expected version, batch order, every message field, and exact string/offset representation.
    internal static string RequestPayload(MemoryAppendRequest request) =>
        request.Turn is null
            ? JsonSerializer.Serialize(new { request.ExpectedVersion, request.Messages }, Options)
            : JsonSerializer.Serialize(new { request.ExpectedVersion, request.Messages, request.Turn }, Options);
}
