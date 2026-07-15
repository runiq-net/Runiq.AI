using Runiq.AI.ContextSpaces.Models;
using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.ContextSpaces.Services;

namespace Runiq.AI.ContextSpaces.Tests;

public sealed class ContextSpaceSourceSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_ShouldReturnMatchingDocumentsOrderedByScore()
    {
        // Bu test, source dokümanlari içinde sorguyla eslesen sonuçlarin skor sirasina göre döndügünü dogrular.
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
        // Bu test, bos sorgu geldiginde source dokümanlarinin aranmadigini dogrular.
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
        // Bu test, sorguyla eslesmeyen dokümanlar için bos sonuç döndügünü dogrular.
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
        // Bu test, arama sonucunda döndürülecek maksimum sonuç sayisinin uygulandigini dogrular.
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
        // Bu test, arama sonucundaki snippet'in eslesen terim çevresinden üretildigini dogrular.
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
        // Bu test, geçersiz maksimum sonuç sayisinin kabul edilmedigini dogrular.
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
        // Bu test, sehir adinin dosya adi, göreli yol veya baslik alaninda geçmesinin arama skorunu yükselttigini dogrular.
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

    [Fact]
    public async Task SearchAsync_ShouldReturnPdfDocument_WhenQueryMatchesExtractedContent()
    {
        // Bu test, PDF'ten çikarilmis metin içindeki eslesmelerin source search sonucuna dahil edildigini dogrular.
        var documents = new[]
        {
        CreateDocument(
            relativePath: "ankara-guide.md",
            content: "# Ankara Guide Ankara has castle, museums and food routes."),

        CreateDocument(
            relativePath: "journey-to-history-and-culture.pdf",
            content: """
                     --- Page 1 ---
                     Journey to History and Culture

                     Traces of Historical Izmir
                     Kemeralti, Kadifekale and Basmane regions, known as historical Izmir today,
                     are uniquely located in the historical port city.

                     --- Page 2 ---
                     Kemeralti, one of the biggest open-air bazaars of Turkey, is a place to be discovered
                     with its colorful shops as well as its inns.
                     """,
            contentType: "application/pdf")
    };

        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader(documents));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            "Kemeralti için kisa bir gezi plani çikar",
            maxResults: 5);

        var result = Assert.Single(results);

        Assert.Equal("journey-to-history-and-culture.pdf", result.RelativePath);
        Assert.Contains("Kemeralti", result.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Score >= 8.0);
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("bursa-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("istanbul-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("ankara-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("ankara-food-history-guide.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_ShouldSelectIzmirSources_WhenIzmirHistoricalTripQueryIsUsed()
    {
        // Bu test, Izmir sehir sorgularinda PDF ve markdown kaynaklarinin birlikte seçilebildigini dogrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "15 kisilik bir grupla izmir de 2 günlük tarihi gezi plani",
            maxResults: 5);

        Assert.Contains(results, candidate =>
            candidate.RelativePath.Equals("journey-to-history-and-culture.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, candidate =>
            candidate.RelativePath.Equals("izmir-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("bursa-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("istanbul-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("ankara-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("ankara-food-history-guide.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_ShouldSelectOnlyIzmirSources_WhenKadifekaleQueryIsUsed()
    {
        // Bu test, dar landmark sorgusunda farkli sehir dokümanlarinin elendigini dogrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Kadifekale için 10 kisilik bir gruba 2 günlük gezi plani hazirla",
            maxResults: 5);

        Assert.Contains(results, candidate =>
            candidate.RelativePath.Equals("journey-to-history-and-culture.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.All(results, result =>
            Assert.Contains(
                result.RelativePath,
                ["journey-to-history-and-culture.pdf", "izmir-guide.md"],
                StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_ShouldSelectIzmirMarkdownAndPdf_WhenIzmirThemeQueryIsUsed()
    {
        // Bu test, Izmir + tema sorgularinda genel sehir markdown'u ile tarihi PDF'in birlikte seçildigini dogrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Izmir için tarihi mekanlari da kapsayacak sekilde bir program hazirla",
            maxResults: 5);

        Assert.Contains(results, candidate =>
            candidate.RelativePath.Equals("journey-to-history-and-culture.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, candidate =>
            candidate.RelativePath.Equals("izmir-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("bursa-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("istanbul-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("ankara-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("ankara-food-history-guide.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_ShouldSelectOnlyAnkaraDocuments_WhenAnkaraTripQueryIsUsed()
    {
        // Bu test, Ankara entity'si geçen query'lerde yalnizca Ankara source'larinin döndügünü dogrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Ankara için 2 günlük gezi programi",
            maxResults: 5);

        Assert.NotEmpty(results);
        Assert.All(results, result =>
            Assert.Contains("ankara", result.RelativePath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, result =>
            result.RelativePath.Equals("ankara-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, result =>
            result.RelativePath.Equals("journey-to-history-and-culture.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, result =>
            result.RelativePath.Equals("izmir-guide.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_ShouldSelectOnlyBursaDocuments_WhenBursaTripQueryIsUsed()
    {
        // Bu test, Bursa entity'si geçen query'lerde yalnizca Bursa source'larinin döndügünü dogrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Bursa için 2 günlük gezi plani",
            maxResults: 5);

        var result = Assert.Single(results);

        Assert.Equal("bursa-guide.md", result.RelativePath);
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("journey-to-history-and-culture.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("izmir-guide.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_ShouldSelectOnlyIstanbulDocument_WhenIstanbulFerryQueryIsUsed()
    {
        // Bu test, Istanbul sorgusunda sadece body içinde Istanbul geçen farkli sehir kaynaklarinin elendigini dogrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Istanbul için vapur ve tarihi semtleri içeren kisa bir gezi plani hazirla",
            maxResults: 5);

        var result = Assert.Single(results);

        Assert.Equal("istanbul-guide.md", result.RelativePath);
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("bursa-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("ankara-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("ankara-food-history-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("izmir-guide.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, candidate =>
            candidate.RelativePath.Equals("journey-to-history-and-culture.pdf", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_ShouldIgnoreBodyOnlyLocationMention_WhenOwnedDocumentExists()
    {
        // Bu test, baska bir dokümanin body içindeki kiyaslama mention'inin ownership sinyali sayilmadigini dogrular.
        var documents = new[]
        {
            CreateDocument(
                relativePath: "istanbul-guide.md",
                content: """
                         # Istanbul Practical Travel Guide
                         Istanbul ferry routes, historic districts and neighborhood walks fit a short plan.
                         """),

            CreateDocument(
                relativePath: "bursa-guide.md",
                content: """
                         # Bursa Practical Travel Guide
                         Bursa is easier to plan than Istanbul, but the main route is Ulu Cami and Koza Han.
                         """)
        };

        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader(documents));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            "Istanbul ferry historic districts",
            maxResults: 5);

        var result = Assert.Single(results);

        Assert.Equal("istanbul-guide.md", result.RelativePath);
    }


    private static ContextSpace CreateContextSpace()
    {
        return new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");
    }

    private static ContextSpace CreateSampleTravelContextSpace()
    {
        var sourcePath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "samples",
                "Runiq.AI.ContextTravelGuide",
                "Context"));

        return new ContextSpace(
                id: "travel-planning",
                name: "Travel Planning")
            .AddSources(sources => sources.FromFileSystem(
                id: "travel-docs",
                name: "Travel Documents",
                path: sourcePath));
    }

    private static ContextSpaceSourceDocument CreateDocument(
        string relativePath,
        string content,
        string contentType = "text/plain")
    {
        return new ContextSpaceSourceDocument
        {
            SourceId = "travel-docs",
            SourceName = "Travel Documents",
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Extension = Path.GetExtension(relativePath),
            ContentType = contentType,
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

