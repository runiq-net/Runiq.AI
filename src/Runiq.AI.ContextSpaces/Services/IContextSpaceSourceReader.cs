using Runiq.AI.ContextSpaces.Models.Sources;

namespace Runiq.AI.ContextSpaces.Services;

/// <summary>
/// Context space'e bagli source dokümanlarini okumak için kullanilan servis sözlesmesini temsil eder.
/// </summary>
public interface IContextSpaceSourceReader
{
    /// <summary>
    /// Verilen context space içindeki desteklenen source dokümanlarini okur.
    /// </summary>
    /// <param name="contextSpace">Source dokümanlari okunacak context space.</param>
    /// <param name="cancellationToken">Islemin iptal edilmesini izleyen token.</param>
    /// <returns>Okunabilen source dokümanlari.</returns>
    Task<IReadOnlyList<ContextSpaceSourceDocument>> ReadAsync(
        ContextSpace contextSpace,
        CancellationToken cancellationToken = default);
}
