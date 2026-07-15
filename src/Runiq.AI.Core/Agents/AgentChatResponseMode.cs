using System.Text.Json.Serialization;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Studio chat endpoint'inin cevabi hangi biçimde döndürecegini belirtir.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentChatResponseMode
{
    /// <summary>
    /// Agent cevabini tek seferlik JSON sonuç olarak döndürür.
    /// </summary>
    Result,

    /// <summary>
    /// Agent execution olaylarini SSE stream olarak döndürür.
    /// </summary>
    Stream
}
