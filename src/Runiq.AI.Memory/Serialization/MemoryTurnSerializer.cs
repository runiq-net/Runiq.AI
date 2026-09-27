using System.Text.Json;
using System.Text.Json.Serialization;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Serialization;

// Turn payload evolution is independent of the existing message JSON and append receipt formats.
internal static class MemoryTurnSerializer
{
    internal const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true
    };

    internal static string Serialize(MemoryTurn turn) => JsonSerializer.Serialize(turn, Options);

    internal static MemoryTurn Deserialize(string payload, int version)
    {
        if (version != CurrentVersion) throw new MemoryStoreException(MemoryStoreError.InvalidPayload);
        try
        {
            return JsonSerializer.Deserialize<MemoryTurn>(payload, Options)
                ?? throw new MemoryStoreException(MemoryStoreError.InvalidPayload);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new MemoryStoreException(MemoryStoreError.InvalidPayload, exception);
        }
    }
}
