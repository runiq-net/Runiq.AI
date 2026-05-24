using System.Text.RegularExpressions;
using Runiq.ContextSpaces.Models.Skills;
using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ContextSpaces.Services;

/// <summary>
/// Context source dokümanları üzerinde basit ve deterministik metin araması yapan servisi temsil eder.
/// </summary>
public sealed partial class ContextSpaceSourceSearchService : IContextSpaceSourceSearchService
{
    private const int DefaultSnippetLength = 320;
    private const int TitleScanLength = 250;

    private readonly IContextSpaceSourceReader sourceReader;

    /// <summary>
    /// Yeni bir context source search service örneği oluşturur.
    /// </summary>
    /// <param name="sourceReader">Arama yapılacak dokümanları okuyan source reader.</param>
    public ContextSpaceSourceSearchService(IContextSpaceSourceReader sourceReader)
    {
        this.sourceReader = sourceReader
            ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextSpaceSourceSearchResult>> SearchAsync(
        ContextSpace contextSpace,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextSpace);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxResults),
                maxResults,
                "Maximum result count must be greater than zero.");
        }

        var queryTerms = Tokenize(query);

        if (queryTerms.Count == 0)
        {
            return [];
        }

        var documents = await sourceReader.ReadAsync(
            contextSpace,
            cancellationToken);

        var results = new List<ContextSpaceSourceSearchResult>();

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = NormalizeWhitespace(document.Content);

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var score = CalculateScore(document, content, queryTerms);

            if (score <= 0)
            {
                continue;
            }

            results.Add(new ContextSpaceSourceSearchResult
            {
                SourceId = document.SourceId,
                SourceName = document.SourceName,
                RelativePath = document.RelativePath,
                FileName = document.FileName,
                Snippet = CreateSnippet(content, queryTerms),
                Score = score
            });
        }

        return results
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToArray();
    }

    private static double CalculateScore(
        ContextSpaceSourceDocument document,
        string content,
        IReadOnlyList<string> queryTerms)
    {
        var score = 0.0;
        var titleArea = content[..Math.Min(content.Length, TitleScanLength)];

        foreach (var term in queryTerms)
        {
            var occurrenceCount = CountOccurrences(content, term);

            if (occurrenceCount > 0)
            {
                score += 1.0 + Math.Min(occurrenceCount, 5) * 0.25;
            }

            if (ContainsTerm(document.FileName, term))
            {
                score += 8.0;
            }

            if (ContainsTerm(document.RelativePath, term))
            {
                score += 8.0;
            }

            if (ContainsTerm(titleArea, term))
            {
                score += 3.0;
            }
        }

        return score;
    }

    private static bool ContainsTerm(string value, string term)
    {
        return value.Contains(
            term,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string content, string term)
    {
        var count = 0;
        var currentIndex = 0;

        while (currentIndex < content.Length)
        {
            var foundIndex = content.IndexOf(
                term,
                currentIndex,
                StringComparison.OrdinalIgnoreCase);

            if (foundIndex < 0)
            {
                break;
            }

            count++;
            currentIndex = foundIndex + term.Length;
        }

        return count;
    }

    private static string CreateSnippet(
        string content,
        IReadOnlyList<string> queryTerms)
    {
        var firstMatchIndex = queryTerms
            .Select(term => content.IndexOf(term, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(0)
            .Min();

        var startIndex = Math.Max(0, firstMatchIndex - DefaultSnippetLength / 3);
        var length = Math.Min(DefaultSnippetLength, content.Length - startIndex);

        var snippet = content.Substring(startIndex, length).Trim();

        if (startIndex > 0)
        {
            snippet = "..." + snippet;
        }

        if (startIndex + length < content.Length)
        {
            snippet += "...";
        }

        return snippet;
    }

    private static IReadOnlyList<string> Tokenize(string query)
    {
        return WordRegex()
            .Matches(query)
            .Select(match => match.Value.Trim().ToLowerInvariant())
            .Where(term => term.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeWhitespace(string value)
    {
        return WhitespaceRegex()
            .Replace(value, " ")
            .Trim();
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}