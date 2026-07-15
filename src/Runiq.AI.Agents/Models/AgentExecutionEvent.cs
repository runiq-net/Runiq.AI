namespace Runiq.AI.Agents;

/// <summary>
/// Agent çalismasi sirasinda üretilen stream olayini temsil eder.
/// </summary>
public sealed record AgentExecutionEvent(
    AgentExecutionEventKind Kind,
    string? Content,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ArgumentsJson = null,
    string? OutputJson = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    IReadOnlyList<AgentExecutionContextSpaceInfo>? ContextSpaces = null,
    IReadOnlyList<AgentExecutionSkillInfo>? Skills = null,
    IReadOnlyList<AgentExecutionSourceInfo>? Sources = null,
    IReadOnlyList<AgentExecutionLoadedSkillInfo>? LoadedSkills = null,
    AgentExecutionContextSearchSummaryInfo? ContextSearchSummary = null,
    IReadOnlyList<AgentExecutionSourceSearchResultInfo>? SourceSearchResults = null)
{
    /// <summary>
    /// Assistant yanitindan gelen parça metin olayini olusturur.
    /// </summary>
    /// <param name="content">Assistant yanitina eklenecek parça metindir.</param>
    /// <returns>Assistant delta olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent AssistantDelta(string content)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.AssistantDelta,
            Content: content);
    }

    /// <summary>
    /// Bir tool çagrisinin basladigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="toolCallId">Model tarafindan üretilen tool çagrisi kimligidir.</param>
    /// <param name="toolName">Çalistirilacak tool adidir.</param>
    /// <param name="argumentsJson">Tool çagrisi için üretilen JSON argümanlaridir.</param>
    /// <returns>Tool çagrisi baslangiç olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent ToolCallStarted(
        string toolCallId,
        string toolName,
        string argumentsJson)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallStarted,
            Content: toolName,
            ToolCallId: toolCallId,
            ToolName: toolName,
            ArgumentsJson: argumentsJson);
    }

    /// <summary>
    /// Bir tool çagrisinin basariyla tamamlandigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="toolCallId">Tamamlanan tool çagrisi kimligidir.</param>
    /// <param name="toolName">Tamamlanan tool adidir.</param>
    /// <param name="outputJson">Tool çalismasi sonucunda üretilen JSON çiktidir.</param>
    /// <returns>Tool çagrisi tamamlanma olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent ToolCallCompleted(
        string toolCallId,
        string toolName,
        string outputJson)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallCompleted,
            Content: outputJson,
            ToolCallId: toolCallId,
            ToolName: toolName,
            OutputJson: outputJson);
    }

    /// <summary>
    /// Bir tool çagrisinin hata ile sonuçlandigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="toolCallId">Hata alan tool çagrisi kimligidir.</param>
    /// <param name="toolName">Hata alan tool adidir.</param>
    /// <param name="errorMessage">Tool çalismasi sirasinda olusan hata mesajidir.</param>
    /// <param name="errorCode">Varsa hata kodudur.</param>
    /// <returns>Tool çagrisi hata olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent ToolCallFailed(
        string toolCallId,
        string toolName,
        string errorMessage,
        string? errorCode = null)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallFailed,
            Content: errorMessage,
            ToolCallId: toolCallId,
            ToolName: toolName,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    /// Agent çalismasina saglanan context space, skill ve source bilgilerini bildiren stream olayini olusturur.
    /// </summary>
    public static AgentExecutionEvent ContextProvided(
        IReadOnlyList<AgentExecutionContextSpaceInfo> contextSpaces,
        IReadOnlyList<AgentExecutionSkillInfo> skills,
        IReadOnlyList<AgentExecutionSourceInfo> sources)
    {
        ArgumentNullException.ThrowIfNull(contextSpaces);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(sources);

        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ContextProvided,
            Content: null,
            ContextSpaces: contextSpaces,
            Skills: skills,
            Sources: sources);
    }

    /// <summary>
    /// Agent çalismasi için skill yönergelerinin model context'ine eklendigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="loadedSkills">Model context'ine eklenen skill özet bilgileridir.</param>
    /// <returns>Skill yükleme olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent SkillLoaded(
        IReadOnlyList<AgentExecutionLoadedSkillInfo> loadedSkills)
    {
        ArgumentNullException.ThrowIfNull(loadedSkills);

        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.SkillLoaded,
            Content: null,
            LoadedSkills: loadedSkills);
    }

    /// <summary>
    /// Agent çalismasi sirasinda context source'larda arama yapildigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="summary">Context arama özet metrikleridir.</param>
    /// <param name="sourceSearchResults">Kullanici girdisine göre bulunan source arama sonuçlaridir.</param>
    /// <returns>Context arama olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent ContextSearched(
        AgentExecutionContextSearchSummaryInfo summary,
        IReadOnlyList<AgentExecutionSourceSearchResultInfo> sourceSearchResults)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(sourceSearchResults);

        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ContextSearched,
            Content: null,
            ContextSearchSummary: summary,
            SourceSearchResults: sourceSearchResults);
    }

    /// <summary>
    /// Agent çalismasinin basariyla tamamlandigini bildiren stream olayini olusturur.
    /// </summary>
    /// <returns>Tamamlanma olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent Completed()
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Completed,
            Content: null);
    }

    /// <summary>
    /// Agent çalismasinin hata ile sonlandigini bildiren stream olayini olusturur.
    /// </summary>
    /// <param name="errorMessage">Agent çalismasi sirasinda olusan hata mesajidir.</param>
    /// <param name="errorCode">Varsa hata kodudur.</param>
    /// <returns>Hata olayini temsil eden stream olayidir.</returns>
    public static AgentExecutionEvent Failed(
        string errorMessage,
        string? errorCode = null)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Failed,
            Content: errorMessage,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }
}

/// <summary>
/// Agent stream olay tiplerini belirtir.
/// </summary>
public enum AgentExecutionEventKind
{
    /// <summary>
    /// Assistant yanitindan gelen parça metin olayini belirtir.
    /// </summary>
    AssistantDelta = 0,

    /// <summary>
    /// Tool çagrisinin basladigini belirtir.
    /// </summary>
    ToolCallStarted = 1,

    /// <summary>
    /// Tool çagrisinin basariyla tamamlandigini belirtir.
    /// </summary>
    ToolCallCompleted = 2,

    /// <summary>
    /// Tool çagrisinin hata ile sonuçlandigini belirtir.
    /// </summary>
    ToolCallFailed = 3,

    /// <summary>
    /// Agent çalismasinin basariyla tamamlandigini belirtir.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Agent çalismasinin hata ile sonlandigini belirtir.
    /// </summary>
    Failed = 5,

    /// <summary>
    /// Agent çalismasina context space bilgilerinin saglandigini belirtir.
    /// </summary>
    ContextProvided = 6,

    /// <summary>
    /// Agent çalismasi sirasinda context source'larda arama yapildigini belirtir.
    /// </summary>
    ContextSearched = 7,

    /// <summary>
    /// Agent çalismasi için skill yönergelerinin yüklendigini belirtir.
    /// </summary>
    SkillLoaded = 8

}

/// <summary>
/// Agent çalismasina saglanan context space özet bilgisini temsil eder.
/// </summary>
public sealed record AgentExecutionContextSpaceInfo(
    string Id,
    string Name,
    string? Description);

/// <summary>
/// Agent çalismasina saglanan skill özet bilgisini temsil eder.
/// </summary>
public sealed record AgentExecutionSkillInfo(
    string Id,
    string Name,
    string? Description,
    string? Version,
    IReadOnlyList<string> Tags,
    string SourceId,
    string RelativePath);

/// <summary>
/// Agent çalismasi için model context'ine eklenen skill özet bilgisini temsil eder.
/// </summary>
public sealed record AgentExecutionLoadedSkillInfo(
    string SkillId,
    string SkillName,
    string? Version,
    string? Description);

/// <summary>
/// Agent çalismasina kullanilabilir olarak saglanan source özet bilgisini temsil eder.
/// </summary>
public sealed record AgentExecutionSourceInfo(
    string Id,
    string Name,
    string Kind,
    string? Description);

/// <summary>
/// Agent çalismasi sirasinda yapilan context source aramasinin özet metriklerini temsil eder.
/// </summary>
public sealed record AgentExecutionContextSearchSummaryInfo(
    int AttachedSourceCount,
    int SearchedDocumentCount,
    int CandidateCount,
    int SelectedCount);

/// <summary>
/// Agent çalismasi sirasinda bulunan source arama sonucunu temsil eder.
/// </summary>
public sealed record AgentExecutionSourceSearchResultInfo(
    string SourceId,
    string SourceName,
    string RelativePath,
    string FileName,
    string Snippet,
    double Score);

