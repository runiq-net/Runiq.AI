using Runiq.AI.Rag.CorporateDocumentAssistant.Models;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Services;

/// <summary>
/// Reads the checked-in plain-text seed documents used by the corporate document assistant sample.
/// </summary>
public sealed class SeedDocumentReader
{
    private readonly IWebHostEnvironment environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeedDocumentReader"/> class.
    /// </summary>
    /// <param name="environment">The host environment used to locate the sample content root.</param>
    public SeedDocumentReader(IWebHostEnvironment environment)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Reads all seed document summaries without ingesting them.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel file reads.</param>
    /// <returns>The available seed document summaries.</returns>
    public async Task<IReadOnlyList<SeedDocumentSummary>> ReadSummariesAsync(CancellationToken cancellationToken = default)
    {
        var documents = new List<SeedDocumentSummary>();

        foreach (var path in EnumerateSeedDocumentPaths())
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            documents.Add(new SeedDocumentSummary
            {
                Id = Path.GetFileNameWithoutExtension(path),
                Name = Path.GetFileName(path),
                Preview = CreatePreview(content),
                Url = $"/documents/{Uri.EscapeDataString(Path.GetFileNameWithoutExtension(path))}",
            });
        }

        return documents;
    }

    /// <summary>
    /// Reads one seed document by identifier.
    /// </summary>
    /// <param name="id">The seed document identifier derived from the file name.</param>
    /// <param name="cancellationToken">A token that can cancel the file read.</param>
    /// <returns>The ingestion request for the seed document, or <see langword="null" /> when it is not found.</returns>
    public async Task<CorporateDocumentIngestionRequest?> ReadDocumentAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var safeId = Path.GetFileNameWithoutExtension(id);
        var path = Path.Combine(GetSampleDocumentsPath(), $"{safeId}.md");

        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        return new CorporateDocumentIngestionRequest
        {
            Id = safeId,
            Title = Path.GetFileName(path),
            Content = content,
        };
    }

    /// <summary>
    /// Reads every checked-in seed document as an ingestion request.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel file reads.</param>
    /// <returns>The seed documents prepared for ingestion.</returns>
    public async Task<IReadOnlyList<CorporateDocumentIngestionRequest>> ReadDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var documents = new List<CorporateDocumentIngestionRequest>();

        foreach (var path in EnumerateSeedDocumentPaths())
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            documents.Add(new CorporateDocumentIngestionRequest
            {
                Id = Path.GetFileNameWithoutExtension(path),
                Title = Path.GetFileName(path),
                Content = content,
            });
        }

        return documents;
    }

    private IEnumerable<string> EnumerateSeedDocumentPaths()
    {
        var sampleDocumentsPath = GetSampleDocumentsPath();

        return Directory.Exists(sampleDocumentsPath)
            ? Directory.EnumerateFiles(sampleDocumentsPath, "*.md").Order(StringComparer.OrdinalIgnoreCase)
            : [];
    }

    private string GetSampleDocumentsPath()
    {
        return Path.Combine(environment.ContentRootPath, "SampleDocuments");
    }

    private static string CreatePreview(string content)
    {
        const int maxLength = 180;

        var preview = content
            .ReplaceLineEndings(" ")
            .Trim();

        return preview.Length <= maxLength
            ? preview
            : string.Concat(preview.AsSpan(0, maxLength), "...");
    }
}

