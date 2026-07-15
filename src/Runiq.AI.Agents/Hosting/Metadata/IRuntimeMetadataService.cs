namespace Runiq.AI.Core.Metadata;

/// <summary>
/// Dashboard tarafından kullanılacak runtime metadata bilgilerini sağlar.
/// </summary>
public interface IRuntimeMetadataService
{
    /// <summary>
    /// Host uygulamada register edilmiş agent listesini döndürür.
    /// </summary>
    IReadOnlyList<AgentMetadataDto> GetAgents();

    /// <summary>
    /// Host uygulamada register edilmiş ve agent'lara bağlı tool listesini döndürür.
    /// </summary>
    IReadOnlyList<ToolMetadataDto> GetTools();

    /// <summary>
    /// Host uygulamada register edilmiş context space listesini döndürür.
    /// </summary>
    IReadOnlyList<ContextSpaceMetadataDto> GetContextSpaces();
}

