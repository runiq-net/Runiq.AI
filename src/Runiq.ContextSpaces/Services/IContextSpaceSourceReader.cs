using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ContextSpaces.Services;

/// <summary>
/// Context space'e bağlı source dokümanlarını okumak için kullanılan servis sözleşmesini temsil eder.
/// </summary>
public interface IContextSpaceSourceReader
{
    /// <summary>
    /// Verilen context space içindeki desteklenen source dokümanlarını okur.
    /// </summary>
    /// <param name="contextSpace">Source dokümanları okunacak context space.</param>
    /// <param name="cancellationToken">İşlemin iptal edilmesini izleyen token.</param>
    /// <returns>Okunabilen source dokümanları.</returns>
    Task<IReadOnlyList<ContextSpaceSourceDocument>> ReadAsync(
        ContextSpace contextSpace,
        CancellationToken cancellationToken = default);
}