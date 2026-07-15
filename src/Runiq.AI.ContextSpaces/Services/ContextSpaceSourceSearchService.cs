using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using Runiq.AI.ContextSpaces.Models.Skills;
using Runiq.AI.ContextSpaces.Models.Sources;

namespace Runiq.AI.ContextSpaces.Services;

/// <summary>
/// Context source dokümanlari üzerinde basit ve deterministik metin aramasi yapan servisi temsil eder.
/// </summary>
public sealed partial class ContextSpaceSourceSearchService : IContextSpaceSourceSearchService
{
    private const int DefaultSnippetLength = 320;
    private const int TitleScanLength = 250;
    private const double StrongEntityMatchBoost = 8.0;
    private const double MinimumRelativeResultScoreRatio = 0.30;

    private static readonly ISet<string> StopWords = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "and",
        "bir",
        "bu",
        "cultural",
        "da",
        "day",
        "days",
        "de",
        "çikar",
        "gezi",
        "group",
        "grup",
        "grupla",
        "guide",
        "gün",
        "günlük",
        "historical",
        "history",
        "hazirla",
        "icin",
        "için",
        "ile",
        "kapsayan",
        "kapsayacak",
        "kisilik",
        "kisa",
        "kisa",
        "lazim",
        "mekan",
        "mekanlar",
        "mekanlari",
        "orta",
        "plan",
        "plani",
        "planlamamiz",
        "program",
        "programi",
        "rota",
        "route",
        "the",
        "tarih",
        "tarihi",
        "travel",
        "trip",
        "yas",
        "üzeri"
    };

    private readonly IContextSpaceSourceReader sourceReader;

    /// <summary>
    /// Yeni bir context source search service örnegi olusturur.
    /// </summary>
    /// <param name="sourceReader">Arama yapilacak dokümanlari okuyan source reader.</param>
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
        var response = await SearchWithSummaryAsync(
            contextSpace,
            query,
            maxResults,
            cancellationToken);

        return response.Results;
    }

    /// <inheritdoc />
    public async Task<ContextSpaceSourceSearchResponse> SearchWithSummaryAsync(
        ContextSpace contextSpace,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextSpace);

        if (string.IsNullOrWhiteSpace(query))
        {
            return new ContextSpaceSourceSearchResponse(
                SearchedDocumentCount: 0,
                Results: []);
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
            return new ContextSpaceSourceSearchResponse(
                SearchedDocumentCount: 0,
                Results: []);
        }

        var documents = await sourceReader.ReadAsync(
            contextSpace,
            cancellationToken);

        var candidates = new List<ScoredSourceCandidate>();

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawContent = document.Content;
            var content = NormalizeWhitespace(rawContent);

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var match = CalculateMatch(
                document,
                rawContent,
                content,
                queryTerms);

            if (match.Score <= 0)
            {
                continue;
            }

            candidates.Add(new ScoredSourceCandidate(
                Result: new ContextSpaceSourceSearchResult
                {
                    SourceId = document.SourceId,
                    SourceName = document.SourceName,
                    RelativePath = document.RelativePath,
                    FileName = document.FileName,
                    Snippet = CreateSnippet(content, queryTerms),
                    Score = match.Score
                },
                OwnershipMatchedTerms: match.OwnershipMatchedTerms));
        }

        var ownershipMatchedTerms = candidates
            .SelectMany(candidate => candidate.OwnershipMatchedTerms)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ownershipMatchedTerms.Length > 0)
        {
            candidates = candidates
                .Where(candidate => candidate.OwnershipMatchedTerms.Overlaps(ownershipMatchedTerms))
                .ToList();
        }

        var minimumResultScore = candidates.Count == 0
            ? 0.0
            : candidates.Max(candidate => candidate.Result.Score) * MinimumRelativeResultScoreRatio;

        var orderedResults = candidates
            .Select(candidate => candidate.Result)
            .Where(result => result.Score >= minimumResultScore)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToArray();

        return new ContextSpaceSourceSearchResponse(
            SearchedDocumentCount: documents.Count,
            Results: orderedResults);
    }

    private static SourceMatch CalculateMatch(
        ContextSpaceSourceDocument document,
        string rawContent,
        string content,
        IReadOnlyList<SearchTerm> queryTerms)
    {
        var score = 0.0;
        var titleArea = NormalizeWhitespace(ExtractTitleArea(rawContent));
        var foldedContent = FoldForSearch(content);
        var foldedTitleArea = FoldForSearch(titleArea);
        var foldedFileName = FoldForSearch(document.FileName);
        var foldedRelativePath = FoldForSearch(document.RelativePath);
        var ownershipMatchedTerms = new HashSet<string>(StringComparer.Ordinal);

        foreach (var term in queryTerms)
        {
            var occurrenceCount = CountOccurrences(
                content,
                foldedContent,
                term);

            if (occurrenceCount > 0)
            {
                score += 1.0 + Math.Min(occurrenceCount, 5) * 0.25;

                if (term.IsStrongEntity)
                {
                    score += StrongEntityMatchBoost;
                }
            }

            if (ContainsTerm(document.FileName, foldedFileName, term))
            {
                score += 8.0;

                if (term.IsStrongEntity)
                {
                    ownershipMatchedTerms.Add(term.FoldedValue);
                }
            }

            if (ContainsTerm(document.RelativePath, foldedRelativePath, term))
            {
                score += 8.0;

                if (term.IsStrongEntity)
                {
                    ownershipMatchedTerms.Add(term.FoldedValue);
                }
            }

            if (ContainsTerm(titleArea, foldedTitleArea, term))
            {
                score += 3.0;

                if (term.IsStrongEntity)
                {
                    ownershipMatchedTerms.Add(term.FoldedValue);
                }
            }
        }

        return new SourceMatch(score, ownershipMatchedTerms);
    }

    private static string ExtractTitleArea(string content)
    {
        var markdownHeadings = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('#'))
            .Take(3)
            .ToArray();

        if (markdownHeadings.Length > 0)
        {
            return string.Join(" ", markdownHeadings);
        }

        return content[..Math.Min(content.Length, TitleScanLength)];
    }

    private static bool ContainsTerm(
        string value,
        string foldedValue,
        SearchTerm term)
    {
        return value.Contains(term.Value, StringComparison.OrdinalIgnoreCase) ||
               foldedValue.Contains(term.FoldedValue, StringComparison.Ordinal);
    }

    private static int CountOccurrences(
        string content,
        string foldedContent,
        SearchTerm term)
    {
        var exactCount = CountOccurrences(
            content,
            term.Value,
            StringComparison.OrdinalIgnoreCase);

        if (term.Value.Equals(term.FoldedValue, StringComparison.Ordinal))
        {
            return exactCount;
        }

        var foldedCount = CountOccurrences(
            foldedContent,
            term.FoldedValue,
            StringComparison.Ordinal);

        return Math.Max(exactCount, foldedCount);
    }

    private static int CountOccurrences(
        string content,
        string term,
        StringComparison comparison)
    {
        var count = 0;
        var currentIndex = 0;

        while (currentIndex < content.Length)
        {
            var foundIndex = content.IndexOf(
                term,
                currentIndex,
                comparison);

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
        IReadOnlyList<SearchTerm> queryTerms)
    {
        var firstMatchIndex = queryTerms
            .Select(term => content.IndexOf(term.Value, StringComparison.OrdinalIgnoreCase))
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

    private static IReadOnlyList<SearchTerm> Tokenize(string query)
    {
        return WordRegex()
            .Matches(query)
            .Select(match => CreateSearchTerm(match.Value.Trim()))
            .Where(term => term.Value.Length >= 2)
            .Where(term => !StopWords.Contains(term.Value))
            .Where(term => !StopWords.Contains(term.FoldedValue))
            .DistinctBy(term => term.FoldedValue)
            .ToArray();
    }

    private static SearchTerm CreateSearchTerm(string rawValue)
    {
        var value = rawValue.ToLowerInvariant();
        var foldedValue = FoldForSearch(value);

        return new SearchTerm(
            Value: value,
            FoldedValue: foldedValue,
            IsStrongEntity: IsStrongEntityTerm(rawValue, value, foldedValue));
    }

    private static bool IsStrongEntityTerm(
        string rawValue,
        string value,
        string foldedValue)
    {
        return value.Length >= 4 &&
               foldedValue.Any(char.IsLetter) &&
               !StopWords.Contains(value) &&
               !StopWords.Contains(foldedValue);
    }

    private static string FoldForSearch(string value)
    {
        var normalized = value
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character switch
            {
                'İ' => 'i',
                'I' => 'i',
                'ı' => 'i',
                'i' => 'i',
                _ => char.ToLowerInvariant(character)
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
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

    private sealed record SearchTerm(
        string Value,
        string FoldedValue,
        bool IsStrongEntity);

    private sealed record SourceMatch(
        double Score,
        ISet<string> OwnershipMatchedTerms);

    private sealed record ScoredSourceCandidate(
        ContextSpaceSourceSearchResult Result,
        ISet<string> OwnershipMatchedTerms);
}

