using FindThatBook.API.Models;
using FindThatBook.API.Models.OpenLibrary;
using FindThatBook.API.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FindThatBook.Tests.Services;

public class BookSearchServiceTests
{
    private readonly Mock<IGeminiService> _gemini = new();
    private readonly Mock<IOpenLibraryService> _openLibrary = new();

    private BookSearchService BuildService() =>
        new(_gemini.Object, _openLibrary.Object, NullLogger<BookSearchService>.Instance);

    [Fact]
    public async Task SearchAsync_ExactTitleAndPrimaryAuthor_ReturnsSingleTopCandidate()
    {
        _gemini.Setup(g => g.ParseQueryAsync(It.IsAny<string>()))
            .ReturnsAsync(new QueryHypothesis { Title = "The Hobbit", Author = "J.R.R. Tolkien" });

_openLibrary.Setup(o => o.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync([
                new OLDoc
                {
                    Key = "/works/OL27448W",
                    Title = "The Hobbit",
                    AuthorNames = ["J.R.R. Tolkien"],
                    AuthorKeys = ["/authors/OL26320A"],
                    FirstPublishYear = 1937,
                    CoverId = 8406786
                }
            ]);

        _openLibrary.Setup(o => o.GetWorkAsync("/works/OL27448W"))
            .ReturnsAsync(new OLWorkDetail
            {
                Key = "/works/OL27448W",
                Title = "The Hobbit",
                Authors = [new OLWorkAuthorEntry { Author = new OLKeyRef { Key = "/authors/OL26320A" } }]
            });

        _openLibrary.Setup(o => o.GetAuthorAsync("/authors/OL26320A"))
            .ReturnsAsync(new OLAuthorDetail { Key = "/authors/OL26320A", Name = "J.R.R. Tolkien" });

        var service = BuildService();
        var result = await service.SearchAsync("tolkien hobbit 1937");

        result.Candidates.Should().HaveCount(1);
        result.Candidates[0].Title.Should().Be("The Hobbit");
        result.Candidates[0].Author.Should().Be("J.R.R. Tolkien");
        result.Candidates[0].FirstPublishYear.Should().Be(1937);
        result.Candidates[0].AuthorIsPrimary.Should().BeTrue();
        result.Candidates[0].MatchScore.Should().Be(100, "exact title + primary author is spec 4a, the strongest tier");
    }

    [Fact]
    public async Task SearchAsync_AuthorOnlyQuery_UsesAuthorFallback()
    {
        _gemini.Setup(g => g.ParseQueryAsync(It.IsAny<string>()))
            .ReturnsAsync(new QueryHypothesis { Title = null, Author = "Charles Dickens" });

_openLibrary.Setup(o => o.SearchByAuthorAsync("Charles Dickens", It.IsAny<int>()))
            .ReturnsAsync([
                new OLDoc
                {
                    Key = "/works/OL1234W",
                    Title = "A Tale of Two Cities",
                    AuthorNames = ["Charles Dickens"],
                    FirstPublishYear = 1859
                }
            ]);

        var service = BuildService();
        var result = await service.SearchAsync("charles dickens");

        result.Candidates.Should().NotBeEmpty();
        result.Candidates[0].Title.Should().Be("A Tale of Two Cities");
        result.Candidates[0].Explanation.Should().Contain("Author-only match");
    }

    [Fact]
    public async Task SearchAsync_DeduplicatesWorksByKey()
    {
        _gemini.Setup(g => g.ParseQueryAsync(It.IsAny<string>()))
            .ReturnsAsync(new QueryHypothesis { Title = "Dune" });

// Same work key returned twice (simulating duplicate editions in search results)
        var duplicateDoc = new OLDoc { Key = "/works/OL102749W", Title = "Dune", AuthorNames = ["Frank Herbert"] };
        _openLibrary.Setup(o => o.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync([duplicateDoc, duplicateDoc]);

        _openLibrary.Setup(o => o.GetWorkAsync("/works/OL102749W"))
            .ReturnsAsync(new OLWorkDetail { Key = "/works/OL102749W", Title = "Dune", Authors = [] });

        var service = BuildService();
        var result = await service.SearchAsync("dune");

        result.Candidates.Should().HaveCount(1, "duplicate work keys should be collapsed");
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsEmptyCandidates()
    {
        _gemini.Setup(g => g.ParseQueryAsync(It.IsAny<string>()))
            .ReturnsAsync(new QueryHypothesis { Title = "xyzzy not a real book" });

_openLibrary.Setup(o => o.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync([]);

        var service = BuildService();
        var result = await service.SearchAsync("xyzzy not a real book");

        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ContributorAuthorMatch_ScoresLowerThanPrimary()
    {
        _gemini.Setup(g => g.ParseQueryAsync(It.IsAny<string>()))
            .ReturnsAsync(new QueryHypothesis { Title = "The Hobbit", Author = "Dixon" });

_openLibrary.Setup(o => o.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync([
                new OLDoc
                {
                    Key = "/works/OL27448W",
                    Title = "The Hobbit",
                    AuthorNames = ["J.R.R. Tolkien", "Michael Dixon"],   // Dixon is a contributor
                    AuthorKeys = ["/authors/OL26320A"],
                    FirstPublishYear = 1937
                }
            ]);

        _openLibrary.Setup(o => o.GetWorkAsync("/works/OL27448W"))
            .ReturnsAsync(new OLWorkDetail
            {
                Key = "/works/OL27448W",
                Title = "The Hobbit",
                // Only Tolkien is in the canonical work record
                Authors = [new OLWorkAuthorEntry { Author = new OLKeyRef { Key = "/authors/OL26320A" } }]
            });

        _openLibrary.Setup(o => o.GetAuthorAsync("/authors/OL26320A"))
            .ReturnsAsync(new OLAuthorDetail { Key = "/authors/OL26320A", Name = "J.R.R. Tolkien" });

        var service = BuildService();
        var result = await service.SearchAsync("hobbit dixon");

        result.Candidates.Should().HaveCount(1);
        result.Candidates[0].AuthorIsPrimary.Should().BeFalse("Dixon is a contributor, not the primary author");
        result.Candidates[0].MatchScore.Should().Be(80, "exact title + contributor author is spec 4b, lower than 4a");
    }

    [Fact]
    public async Task SearchAsync_PrimaryAuthorRanksAboveContributor()
    {
        _gemini.Setup(g => g.ParseQueryAsync(It.IsAny<string>()))
            .ReturnsAsync(new QueryHypothesis { Title = "The Hobbit", Author = "Tolkien" });

        _openLibrary.Setup(o => o.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync([
                new OLDoc
                {
                    Key = "/works/OL27448W",
                    Title = "The Hobbit",
                    AuthorNames = ["J.R.R. Tolkien", "Michael Dixon"],
                    AuthorKeys = ["/authors/OL26320A"],
                    FirstPublishYear = 1937
                },
                new OLDoc
                {
                    Key = "/works/OL99999W",
                    Title = "The Hobbit",
                    AuthorNames = ["Michael Dixon", "J.R.R. Tolkien"],
                    AuthorKeys = ["/authors/OL99999A"],
                    FirstPublishYear = 1990
                }
            ]);

        _openLibrary.Setup(o => o.GetWorkAsync("/works/OL27448W"))
            .ReturnsAsync(new OLWorkDetail
            {
                Key = "/works/OL27448W",
                Authors = [new OLWorkAuthorEntry { Author = new OLKeyRef { Key = "/authors/OL26320A" } }]
            });

        _openLibrary.Setup(o => o.GetWorkAsync("/works/OL99999W"))
            .ReturnsAsync(new OLWorkDetail
            {
                Key = "/works/OL99999W",
                Authors = [new OLWorkAuthorEntry { Author = new OLKeyRef { Key = "/authors/OL99999A" } }]
            });

        _openLibrary.Setup(o => o.GetAuthorAsync("/authors/OL26320A"))
            .ReturnsAsync(new OLAuthorDetail { Key = "/authors/OL26320A", Name = "J.R.R. Tolkien" });

        _openLibrary.Setup(o => o.GetAuthorAsync("/authors/OL99999A"))
            .ReturnsAsync(new OLAuthorDetail { Key = "/authors/OL99999A", Name = "Michael Dixon" });

        var service = BuildService();
        var result = await service.SearchAsync("hobbit tolkien");

        result.Candidates.Should().HaveCountGreaterThan(1);
        result.Candidates[0].MatchScore.Should().Be(100, "Tolkien is primary author — spec 4a");
        result.Candidates[1].MatchScore.Should().Be(80, "Tolkien is contributor on second edition — spec 4b");
        result.Candidates[0].MatchScore.Should().BeGreaterThan(result.Candidates[1].MatchScore,
            "primary author match must rank above contributor match");
    }
}
