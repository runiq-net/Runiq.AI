using Runiq.AI.ContextSpaces.Models;
using Runiq.AI.ContextSpaces.Models.Sources;

namespace Runiq.AI.ContextSpaces.Services;

/// <summary>
/// Context space source dokümanlari üzerinde arama yapmak için kullanilan servis sözlesmesini temsil eder.
/// </summary>
public interface IContextSpaceSourceSearchService
{
    /// <summary>
    /// Verilen context space içindeki source dokümanlarinda sorguya göre arama yapar.
    /// </summary>
    /// <param name="contextSpace">Source dokümanlari aranacak context space.</param>
    /// <param name="query">Arama sorgusu.</param>
    /// <param name="maxResults">Döndürülecek maksimum sonuç sayisi.</param>
    /// <param name="cancellationToken">Islemin iptal edilmesini izleyen token.</param>
    /// <returns>Sorguyla eslesen source sonuçlari.</returns>
    Task<IReadOnlyList<ContextSpaceSourceSearchResult>> SearchAsync(
        ContextSpace contextSpace,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen context space içindeki source dokümanlarinda sorguya göre arama yapar ve arama özetini döner.
    /// </summary>
    /// <param name="contextSpace">Source dokümanlari aranacak context space.</param>
    /// <param name="query">Arama sorgusu.</param>
    /// <param name="maxResults">Döndürülecek maksimum sonuç sayisi.</param>
    /// <param name="cancellationToken">Islemin iptal edilmesini izleyen token.</param>
    /// <returns>Arama sonucunu ve taranan doküman sayisini içeren yanit.</returns>
    Task<ContextSpaceSourceSearchResponse> SearchWithSummaryAsync(
        ContextSpace contextSpace,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default);
}

