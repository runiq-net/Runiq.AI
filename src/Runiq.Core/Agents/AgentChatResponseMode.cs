using System.Text.Json.Serialization;

namespace Runiq.Core.Agents;

/// <summary>
/// Studio chat endpoint'inin cevabı hangi biçimde döndüreceğini belirtir.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentChatResponseMode
{
    /// <summary>
    /// Agent cevabını tek seferlik JSON sonuç olarak döndürür.
    /// </summary>
    Result,

    /// <summary>
    /// Agent execution olaylarını SSE stream olarak döndürür.
    /// </summary>
    Stream
}