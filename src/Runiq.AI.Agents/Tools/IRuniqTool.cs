namespace Runiq.AI.Agents.Tools;

/// <summary>
/// Host uygulamanin code-first sekilde tanimladigi çalistirilabilir Runiq tool sözlesmesini temsil eder.
/// </summary>
/// <typeparam name="TInput">Tool çalistirilirken alinacak güçlü tipli input modelidir.</typeparam>
/// <typeparam name="TOutput">Tool çalistirildiktan sonra dönecek güçlü tipli output modelidir.</typeparam>
public interface IRuniqTool<TInput, TOutput>
{
    /// <summary>
    /// Tool'u verilen input ile çalistirir.
    /// </summary>
    /// <param name="input">Tool input modelidir.</param>
    /// <param name="cancellationToken">Iptal istegini tasir.</param>
    /// <returns>Tool çalistirma sonucunu döner.</returns>
    Task<TOutput> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}
