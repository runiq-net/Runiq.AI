using System.Text;
using Microsoft.AspNetCore.Http;
using Runiq.AI.ContextSpaces.Models;
using Runiq.AI.ContextSpaces.Models.Skills;
using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.ContextSpaces.Services;

namespace Runiq.AI.Core.ContextSpaces;

/// <summary>
/// Dashboard context space skill dokümanlarını read-only olarak sunan API handler'ıdır.
/// </summary>
internal sealed class ContextSpaceSkillDocumentApiHandler
{
    private const long MaxPreviewSizeInBytes = 64 * 1024;
    private const string SkillContentType = "text/markdown";

    private readonly IReadOnlyList<ContextSpace> contextSpaces;
    private readonly IContextSpaceSkillDiscoveryService skillDiscoveryService;

    public ContextSpaceSkillDocumentApiHandler(
        IReadOnlyList<ContextSpace>? contextSpaces,
        IContextSpaceSkillDiscoveryService skillDiscoveryService)
    {
        this.contextSpaces = contextSpaces ?? [];
        this.skillDiscoveryService = skillDiscoveryService;
    }

    public IResult List(string contextSpaceId)
    {
        var contextSpace = FindContextSpace(contextSpaceId);

        if (contextSpace is null)
        {
            return Results.NotFound();
        }

        var skills = skillDiscoveryService.Discover(contextSpace);
        var skillsBySourceId = skills
            .GroupBy(skill => skill.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(skill => skill.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var sourceGroups = contextSpace.SkillSources
            .Select(skillSource =>
            {
                skillsBySourceId.TryGetValue(skillSource.Id, out var sourceSkills);
                sourceSkills ??= [];

                return new ContextSpaceSkillSourceDocumentDto(
                    SourceId: skillSource.Id,
                    SourceName: skillSource.Name,
                    Provider: FormatSkillProvider(skillSource.Kind),
                    Path: FormatSkillSourceLocation(skillSource),
                    SkillCount: sourceSkills.Length,
                    Skills: sourceSkills
                        .Select(skill => new ContextSpaceSkillDocumentListItemDto(
                            SkillId: skill.Id,
                            Name: skill.Name,
                            Version: skill.Version,
                            Description: skill.Description,
                            Tags: skill.Tags,
                            RelativePath: skill.RelativePath,
                            ContentType: SkillContentType,
                            IsPreviewSupported: skillSource.Kind == ContextSpaceLocationKind.FileSystem))
                        .ToArray());
            })
            .ToArray();

        return Results.Ok(new ContextSpaceSkillDocumentsResponse(
            ContextSpaceId: contextSpace.Id,
            SkillSources: sourceGroups));
    }

    public async Task<IResult> PreviewAsync(
        string contextSpaceId,
        string? skillId,
        CancellationToken cancellationToken)
    {
        var contextSpace = FindContextSpace(contextSpaceId);

        if (contextSpace is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(skillId))
        {
            return Results.BadRequest("skillId is required.");
        }

        var skill = skillDiscoveryService
            .Discover(contextSpace)
            .FirstOrDefault(candidate =>
                candidate.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase));

        if (skill is null)
        {
            return Results.NotFound();
        }

        var skillSource = contextSpace.SkillSources.FirstOrDefault(candidate =>
            candidate.Id.Equals(skill.SourceId, StringComparison.OrdinalIgnoreCase));

        if (skillSource is null ||
            skillSource.Kind != ContextSpaceLocationKind.FileSystem ||
            string.IsNullOrWhiteSpace(skillSource.Path))
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        if (!TryNormalizeRelativePath(skill.RelativePath, out var relativePath))
        {
            return Results.BadRequest("Skill path is invalid.");
        }

        var sourceRoot = Path.GetFullPath(skillSource.Path);
        var fullPath = Path.GetFullPath(Path.Combine(sourceRoot, relativePath));

        if (!IsUnderRoot(sourceRoot, fullPath))
        {
            return Results.BadRequest("Skill path is invalid.");
        }

        if (!File.Exists(fullPath))
        {
            return Results.NotFound();
        }

        var fileInfo = new FileInfo(fullPath);
        var isTruncated = fileInfo.Length > MaxPreviewSizeInBytes;

        await using var stream = File.OpenRead(fullPath);
        var bytesToRead = (int)Math.Min(fileInfo.Length, MaxPreviewSizeInBytes);
        var buffer = new byte[bytesToRead];
        var bytesRead = await stream.ReadAsync(
            buffer.AsMemory(0, bytesToRead),
            cancellationToken);
        var content = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        return Results.Ok(new ContextSpaceSkillDocumentPreviewDto(
            ContextSpaceId: contextSpace.Id,
            SkillId: skill.Id,
            Name: skill.Name,
            Version: skill.Version,
            Description: skill.Description,
            Tags: skill.Tags,
            RelativePath: relativePath,
            ContentType: SkillContentType,
            Content: content,
            IsTruncated: isTruncated,
            SizeBytes: fileInfo.Length));
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

    private static bool IsUnderRoot(string sourceRoot, string filePath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot))
            + Path.DirectorySeparatorChar;

        var normalizedFilePath = Path.GetFullPath(filePath);

        return normalizedFilePath.StartsWith(
            normalizedRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatSkillProvider(ContextSpaceLocationKind kind)
    {
        return kind switch
        {
            ContextSpaceLocationKind.FileSystem => "FileSystem",
            ContextSpaceLocationKind.S3 => "S3",
            _ => kind.ToString()
        };
    }

    private static string? FormatSkillSourceLocation(ContextSpaceSkillSource skillSource)
    {
        if (skillSource.Kind == ContextSpaceLocationKind.S3)
        {
            return $"s3://{skillSource.BucketName ?? string.Empty}/{skillSource.Prefix ?? string.Empty}";
        }

        return skillSource.Path;
    }
}

