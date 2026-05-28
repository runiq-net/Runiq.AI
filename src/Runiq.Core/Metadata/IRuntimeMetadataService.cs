namespace Runiq.Core.Metadata;

/// <summary>
/// Dashboard tarafÄ±ndan kullanÄ±lacak runtime metadata bilgilerini saÄŸlar.
/// </summary>
public interface IRuntimeMetadataService
{
    /// <summary>
    /// Host uygulamada register edilmiÅŸ agent listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    IReadOnlyList<AgentMetadataDto> GetAgents();

    /// <summary>
    /// Host uygulamada register edilmiÅŸ ve agent'lara baÄŸlÄ± tool listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    IReadOnlyList<ToolMetadataDto> GetTools();

    /// <summary>
    /// Host uygulamada register edilmiÅŸ context space listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    IReadOnlyList<ContextSpaceMetadataDto> GetContextSpaces();
}
