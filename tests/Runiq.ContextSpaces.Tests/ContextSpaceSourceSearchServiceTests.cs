using Runiq.ContextSpaces.Models;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.ContextSpaces.Services;

namespace Runiq.ContextSpaces.Tests;

public sealed class ContextSpaceSourceSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_ShouldReturnMatchingDocumentsOrderedByScore()
    {
        // Bu test, source dokümanları içinde sorguyla eşleşen sonuçların skor sırasına göre döndüğünü doğrular.
        var documents = new[]
        {
            CreateDocument(
                relativePath: "istanbul.txt",
                content: "Istanbul travel plan includes museum, ferry and food. Museum visit is important."),

            CreateDocument(
                relativePath: "ankara.txt",
                content: "Ankara travel plan includes castle and museum."),

            CreateDocument(
                relativePath: "weather.txt",
                content: "Weather forecast is sunny.")
        };

        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader(documents));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            "museum travel");

        Assert.Equal(2, results.Count);
        Assert.Equal("istanbul.txt", results[0].RelativePath);
        Assert.Equal("ankara.txt", results[1].RelativePath);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmptyList_WhenQueryIsEmpty()
    {
        // Bu test, boş sorgu geldiğinde source dokümanlarının aranmadığını doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader([
                CreateDocument("notes.txt", "Some content.")
            ]));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            " ");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmptyList_WhenNoDocumentMatches()
    {
        // Bu test, sorguyla eşleşmeyen dokümanlar için boş sonuç döndüğünü doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader([
                CreateDocument("notes.txt", "Only unrelated content.")
            ]));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            "museum");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ShouldRespectMaxResults()
    {
        // Bu test, arama sonucunda döndürülecek maksimum sonuç sayısının uygulandığını doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader([
                CreateDocument("a.txt", "museum"),
                CreateDocument("b.txt", "museum"),
                CreateDocument("c.txt", "museum")
            ]));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            "museum",
            maxResults: 2);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_ShouldCreateSnippetAroundFirstMatchedTerm()
    {
        // Bu test, arama sonucundaki snippet'in eşleşen terim çevresinden üretildiğini doğrular.
        var content = string.Join(
            " ",
            Enumerable.Repeat("intro", 80)) + " important museum detail";

        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader([
                CreateDocument("notes.txt", content)
            ]));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            "museum");

        var result = Assert.Single(results);

        Assert.Contains("museum", result.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("...", result.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_ShouldThrow_WhenMaxResultsIsInvalid()
    {
        // Bu test, geçersiz maksimum sonuç sayısının kabul edilmediğini doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader([]));

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            searchService.SearchAsync(
                CreateContextSpace(),
                "museum",
                maxResults: 0));

        Assert.Equal("maxResults", exception.ParamName);
    }

    [Fact]
    public async Task SearchAsync_ShouldBoostMatchesInFileNameRelativePathAndTitleArea()
    {
        // Bu test, şehir adının dosya adı, göreli yol veya başlık alanında geçmesinin arama skorunu yükselttiğini doğrular.
        var documents = new[]
        {
        CreateDocument(
            relativePath: "ankara-guide.md",
            content: "# Ankara Practical Travel Guide Ankara has history, food, museums and cultural routes."),

        CreateDocument(
            relativePath: "bursa-guide.md",
            content: "# Bursa Practical Travel Guide Bursa is strong for history, food, Koza Han, Ulu Cami and Iskender."),

        CreateDocument(
            relativePath: "istanbul-guide.md",
            content: "# Istanbul Practical Travel Guide Istanbul has history, food, ferry routes and district-based planning.")
    };

        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader(documents));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            "Bursa tarih yemek",
            maxResults: 3);

        var result = Assert.Single(results);

        Assert.Equal("bursa-guide.md", result.RelativePath);
        Assert.True(result.Score > 0);
    }

    private static ContextSpace CreateContextSpace()
    {
        return new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");
    }

    private static ContextSpaceSourceDocument CreateDocument(
        string relativePath,
        string content)
    {
        return new ContextSpaceSourceDocument
        {
            SourceId = "travel-docs",
            SourceName = "Travel Documents",
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Extension = Path.GetExtension(relativePath),
            ContentType = "text/plain",
            Content = content,
            SizeInBytes = content.Length
        };
    }

    private sealed class StubSourceReader : IContextSpaceSourceReader
    {
        private readonly IReadOnlyList<ContextSpaceSourceDocument> documents;

        public StubSourceReader(IReadOnlyList<ContextSpaceSourceDocument> documents)
        {
            this.documents = documents;
        }

        public Task<IReadOnlyList<ContextSpaceSourceDocument>> ReadAsync(
            ContextSpace contextSpace,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(documents);
        }
    }
}