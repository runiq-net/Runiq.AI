using System.Text;
using Microsoft.AspNetCore.Http;
using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.ContextSpaces.Services;

namespace Runiq.AI.Core.ContextSpaces;

/// <summary>
/// Dashboard context space source dokümanlarını read-only olarak sunan API handler'ıdır.
/// </summary>
internal sealed class ContextSpaceSourceDocumentApiHandler
{
    private const long MaxPreviewSizeInBytes = 64 * 1024;

    private static readonly IReadOnlyDictionary<string, string> SupportedPreviewContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".md"] = "text/markdown",
            [".txt"] = "text/plain",
            [".json"] = "application/json",
            [".pdf"] = "application/pdf"
        };

    private readonly IReadOnlyList<ContextSpace> contextSpaces;
    private readonly IContextSpaceSourceReader sourceReader;

    public ContextSpaceSourceDocumentApiHandler(
        IReadOnlyList<ContextSpace>? contextSpaces,
        IContextSpaceSourceReader sourceReader)
    {
        this.contextSpaces = contextSpaces ?? [];
        this.sourceReader = sourceReader;
    }

    public async Task<IResult> ListAsync(
        string contextSpaceId,
        CancellationToken cancellationToken)
    {
        var contextSpace = FindContextSpace(contextSpaceId);

        if (contextSpace is null)
        {
            return Results.NotFound();
        }

        var documents = await sourceReader.ReadAsync(
            contextSpace,
            cancellationToken);

        var documentsBySourceId = documents
            .Where(IsDashboardPreviewSafeDocument)
            .GroupBy(document => document.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(document => document.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var sourceGroups = contextSpace.Sources
            .Select(source =>
            {
                documentsBySourceId.TryGetValue(source.Id, out var sourceDocuments);
                sourceDocuments ??= [];

                return new ContextSpaceSourceGroupDocumentDto(
                    SourceId: source.Id,
                    SourceName: source.Name,
                    Provider: FormatSourceProvider(source.Kind),
                    Path: FormatSourceLocation(source),
                    DocumentCount: sourceDocuments.Length,
                    Documents: sourceDocuments
                        .Select(document => new ContextSpaceSourceDocumentListItemDto(
                            RelativePath: document.RelativePath,
                            FileName: document.FileName,
                            ContentType: document.ContentType,
                            SizeBytes: document.SizeInBytes,
                            IsPreviewSupported: IsSupportedPreviewExtension(document.Extension)))
                        .ToArray());
            })
            .ToArray();

        return Results.Ok(new ContextSpaceSourceDocumentsResponse(
            ContextSpaceId: contextSpace.Id,
            SourceGroups: sourceGroups));
    }


    public async Task<IResult> PreviewAsync(
    string contextSpaceId,
    string? sourceId,
    string? path,
    CancellationToken cancellationToken)
    {
        var contextSpace = FindContextSpace(contextSpaceId);

        if (contextSpace is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(sourceId) ||
            string.IsNullOrWhiteSpace(path))
        {
            return Results.BadRequest("sourceId and path are required.");
        }

        var source = contextSpace.Sources.FirstOrDefault(candidate =>
            candidate.Id.Equals(sourceId, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return Results.NotFound();
        }

        if (source.Kind != ContextSpaceSourceKind.LocalFileSystem ||
            string.IsNullOrWhiteSpace(source.Path))
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        if (!TryNormalizeRelativePath(path, out var relativePath))
        {
            return Results.BadRequest("Document path is invalid.");
        }

        var extension = Path.GetExtension(relativePath);

        if (!IsSupportedPreviewExtension(extension) ||
            IsBlockedFileName(Path.GetFileName(relativePath)))
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        var sourceRoot = Path.GetFullPath(source.Path);
        var fullPath = Path.GetFullPath(Path.Combine(sourceRoot, relativePath));

        if (!IsUnderRoot(sourceRoot, fullPath))
        {
            return Results.BadRequest("Document path is invalid.");
        }

        if (!File.Exists(fullPath))
        {
            return Results.NotFound();
        }

        var documents = await sourceReader.ReadAsync(
            contextSpace,
            cancellationToken);

        var previewDocument = documents.FirstOrDefault(document =>
            document.SourceId.Equals(source.Id, StringComparison.OrdinalIgnoreCase) &&
            document.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

        if (previewDocument is null)
        {
            return Results.NotFound();
        }

        var content = previewDocument.Content;
        var isTruncated = content.Length > MaxPreviewSizeInBytes;

        if (isTruncated)
        {
            content = content[..(int)MaxPreviewSizeInBytes];
        }

        return Results.Ok(new ContextSpaceSourceDocumentPreviewDto(
            ContextSpaceId: contextSpace.Id,
            SourceId: source.Id,
            SourceName: source.Name,
            RelativePath: relativePath,
            FileName: previewDocument.FileName,
            ContentType: previewDocument.ContentType,
            Content: content,
            IsTruncated: isTruncated,
            SizeBytes: previewDocument.SizeInBytes));
    }

    private ContextSpace? FindContextSpace(string contextSpaceId)
    {
        return contextSpaces.FirstOrDefault(contextSpace =>
            contextSpace.Id.Equals(contextSpaceId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryNormalizeRelativePath(
        string value,
        out string relativePath)
    {
        relativePath = string.Empty;

        if (string.IsNullOrWhiteSpace(value) ||
            Path.IsPathRooted(value))
        {
            return false;
        }

        var normalized = value
            .Replace('\\', '/')
            .Trim('/');

        var segments = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 ||
            segments.Any(segment =>
                segment is "." or ".." ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            return false;
        }

        relativePath = string.Join('/', segments);
        return true;
    }

    private static bool IsDashboardPreviewSafeDocument(
        ContextSpaceSourceDocument document)
    {
        return IsSupportedPreviewExtension(document.Extension) &&
            !IsBlockedFileName(document.FileName);
    }

    private static bool IsSupportedPreviewExtension(string extension)
    {
        return SupportedPreviewContentTypes.ContainsKey(extension);
    }

    private static bool IsBlockedFileName(string fileName)
    {
        return fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
            (fileName.StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ||
            fileName.Equals("secrets.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderRoot(string sourceRoot, string filePath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot))
            + Path.DirectorySeparatorChar;

        var normalizedFilePath = Path.GetFullPath(filePath);

        return normalizedFilePath.StartsWith(
            normalizedRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatSourceProvider(ContextSpaceSourceKind kind)
    {
        return kind switch
        {
            ContextSpaceSourceKind.LocalFileSystem => "FileSystem",
            ContextSpaceSourceKind.ObjectStorage => "S3",
            _ => kind.ToString()
        };
    }

    private static string? FormatSourceLocation(ContextSpaceSource source)
    {
        if (source.Kind == ContextSpaceSourceKind.ObjectStorage)
        {
            return $"s3://{source.BucketName ?? string.Empty}/{source.Prefix ?? string.Empty}";
        }

        return source.Path;
    }
}

