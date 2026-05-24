using Runiq.ContextSpaces.Models;
using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ContextSpaces.Services;

/// <summary>
/// Context space source dokümanları üzerinde arama yapmak için kullanılan servis sözleşmesini temsil eder.
/// </summary>
public interface IContextSpaceSourceSearchService
{
    /// <summary>
    /// Verilen context space içindeki source dokümanlarında sorguya göre arama yapar.
    /// </summary>
    /// <param name="contextSpace">Source dokümanları aranacak context space.</param>
    /// <param name="query">Arama sorgusu.</param>
    /// <param name="maxResults">Döndürülecek maksimum sonuç sayısı.</param>
    /// <param name="cancellationToken">İşlemin iptal edilmesini izleyen token.</param>
    /// <returns>Sorguyla eşleşen source sonuçları.</returns>
    Task<IReadOnlyList<ContextSpaceSourceSearchResult>> SearchAsync(
        ContextSpace contextSpace,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default);
}