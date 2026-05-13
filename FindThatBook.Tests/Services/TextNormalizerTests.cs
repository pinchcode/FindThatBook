using FindThatBook.API.Helpers;
using FluentAssertions;

namespace FindThatBook.Tests.Services;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("The Hobbit", "the hobbit")]
    [InlineData("Héros", "heros")]
    [InlineData("A Tale of Two Cities!", "a tale of two cities")]
    [InlineData("  extra  spaces  ", "extra spaces")]
    public void Normalize_StripsAndLowercases(string input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("The Hobbit", "The Hobbit", 1.0)]
    [InlineData("The Hobbit", "Hobbit", 1.0)]          // subset → full overlap on significant tokens
    [InlineData("hobbit", "the lord of the rings", 0.0)]
    public void TitleSimilarity_ReturnsExpectedScore(string a, string b, double expected)
    {
        TextNormalizer.TitleSimilarity(a, b).Should().BeApproximately(expected, 0.01);
    }

    [Theory]
    [InlineData("The Hobbit", "The Hobbit, or There and Back Again", true)]
    [InlineData("There and Back Again", "The Hobbit, or There and Back Again", true)]
    [InlineData("Dune", "The Hobbit", false)]
    public void TitlesOverlap_HandlesSubtitleVariants(string a, string b, bool expected)
    {
        TextNormalizer.TitlesOverlap(a, b).Should().Be(expected);
    }

    [Theory]
    [InlineData("J.R.R. Tolkien", "J.R.R. Tolkien", true)]
    [InlineData("tolkien", "J.R.R. Tolkien", true)]
    [InlineData("Tolkien", "tolkien", true)]
    [InlineData("Tolkien", "George Orwell", false)]
    [InlineData("jane austen", "Jane Austen", true)]
    public void AuthorNamesMatch_HandlesPartialAndCaseVariants(string a, string b, bool expected)
    {
        TextNormalizer.AuthorNamesMatch(a, b).Should().Be(expected);
    }
}
