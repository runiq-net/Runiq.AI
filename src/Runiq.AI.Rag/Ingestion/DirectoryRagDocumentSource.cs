using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Models.Ingestion;
using UglyToad.PdfPig;

namespace Runiq.AI.Rag.Ingestion;

/// <summary>Discovers supported files below a directory in ordinal path order.</summary>
public sealed class DirectoryRagDocumentSource : IRagDocumentSource
{
    private readonly string rootPath;
    private readonly HashSet<string> extensions;

    /// <summary>Initializes a directory source with optional file extensions.</summary>
    /// <param name="rootPath">The directory to scan recursively.</param>
    /// <param name="extensions">Allowed extensions; defaults to text, Markdown and JSON.</param>
    public DirectoryRagDocumentSource(string rootPath, IEnumerable<string>? extensions = null)
    {
        this.rootPath = string.IsNullOrWhiteSpace(rootPath) ? throw new ArgumentException("A root path is required.", nameof(rootPath)) : Path.GetFullPath(rootPath);
        this.extensions = new HashSet<string>(extensions ?? [".txt", ".md", ".markdown", ".json", ".pdf"], StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootPath)) throw new DirectoryNotFoundException(rootPath);
        var files = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
            .Where(path => extensions.Contains(Path.GetExtension(path))).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var result = new List<RagSourceDocument>(files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var type = GetContentType(file);
            var content = type == "application/pdf" ? ExtractPdfText(file) : await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            result.Add(new RagSourceDocument { Id = Path.GetRelativePath(rootPath, file).Replace('\\', '/'), Content = content, ContentType = type, Title = Path.GetFileNameWithoutExtension(file), Source = file, Version = File.GetLastWriteTimeUtc(file).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }
        return result;
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".md" or ".markdown" => "text/markdown", ".json" => "application/json", ".pdf" => "application/pdf", _ => "text/plain" };
    private static string ExtractPdfText(string path) { using var pdf = PdfDocument.Open(path); return string.Join("\f", pdf.GetPages().Select(page => page.Text)); }
}
