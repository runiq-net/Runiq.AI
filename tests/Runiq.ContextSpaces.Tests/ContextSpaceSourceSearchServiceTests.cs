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

    [Fact]
    public async Task SearchAsync_ShouldReturnPdfDocument_WhenQueryMatchesExtractedContent()
    {
        // Bu test, PDF'ten çıkarılmış metin içindeki eşleşmelerin source search sonucuna dahil edildiğini doğrular.
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
                     Kemeraltı, Kadifekale and Basmane regions, known as historical Izmir today,
                     are uniquely located in the historical port city.

                     --- Page 2 ---
                     Kemeraltı, one of the biggest open-air bazaars of Turkey, is a place to be discovered
                     with its colorful shops as well as its inns.
                     """,
            contentType: "application/pdf")
    };

        var searchService = new ContextSpaceSourceSearchService(
            new StubSourceReader(documents));

        var results = await searchService.SearchAsync(
            CreateContextSpace(),
            "Kemeraltı için kısa bir gezi planı çıkar",
            maxResults: 5);

        var result = Assert.Single(results);

        Assert.Equal("journey-to-history-and-culture.pdf", result.RelativePath);
        Assert.Contains("Kemeraltı", result.Snippet, StringComparison.OrdinalIgnoreCase);
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
    public async Task SearchAsync_ShouldFindKemeraltı_InRealSamplePdf()
    {
        // Bu test, sample travel context içindeki gerçek PDF kaynağının source search tarafından bulunabildiğini doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Kemeraltı için kısa bir gezi planı çıkar",
            maxResults: 5);

        var result = Assert.Single(results, candidate =>
            candidate.RelativePath.Equals(
                "journey-to-history-and-culture.pdf",
                StringComparison.OrdinalIgnoreCase));

        Assert.Contains("Kemeraltı", result.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Score >= 8.0);
    }

    [Fact]
    public async Task SearchAsync_ShouldSelectIzmirSources_WhenIzmirHistoricalTripQueryIsUsed()
    {
        // Bu test, İzmir şehir sorgularında PDF ve markdown kaynaklarının birlikte seçilebildiğini doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "15 kişilik bir grupla izmir de 2 günlük tarihi gezi planı",
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
        // Bu test, dar landmark sorgusunda farklı şehir dokümanlarının elendiğini doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Kadifekale için 10 kişilik bir gruba 2 günlük gezi planı hazırla",
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
        // Bu test, İzmir + tema sorgularında genel şehir markdown'u ile tarihi PDF'in birlikte seçildiğini doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "İzmir için tarihi mekanları da kapsayacak şekilde bir program hazırla",
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
        // Bu test, Ankara entity'si geçen query'lerde yalnızca Ankara source'larının döndüğünü doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Ankara için 2 günlük gezi programı",
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
        // Bu test, Bursa entity'si geçen query'lerde yalnızca Bursa source'larının döndüğünü doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "Bursa için 2 günlük gezi planı",
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
        // Bu test, İstanbul sorgusunda sadece body içinde İstanbul geçen farklı şehir kaynaklarının elendiğini doğrular.
        var searchService = new ContextSpaceSourceSearchService(
            new ContextSpaceFileSystemSourceReader());

        var results = await searchService.SearchAsync(
            CreateSampleTravelContextSpace(),
            "İstanbul için vapur ve tarihi semtleri içeren kısa bir gezi planı hazırla",
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
        // Bu test, başka bir dokümanın body içindeki kıyaslama mention'ının ownership sinyali sayılmadığını doğrular.
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
                "Runiq.ContextTravelGuide",
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
