using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FindThatBook.API.Helpers;

public static class TextNormalizer
{
    private static readonly string[] StopWords =
        ["a", "an", "the", "of", "and", "or", "in", "on", "at", "to", "for", "with", "by"];

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // NFD decompose to separate base chars from diacritics, then strip non-ASCII
        var normalized = text.Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stripped.Append(c);
        }

        var result = stripped.ToString().ToLowerInvariant();
        result = Regex.Replace(result, @"[^\w\s]", " ");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        return result;
    }

    // Jaccard similarity over significant tokens (stop words excluded)
    public static double TitleSimilarity(string a, string b)
    {
        var aTokens = SignificantTokens(Normalize(a));
        var bTokens = SignificantTokens(Normalize(b));
        if (aTokens.Count == 0 && bTokens.Count == 0) return 1.0;
        if (aTokens.Count == 0 || bTokens.Count == 0) return 0.0;

        var intersection = aTokens.Intersect(bTokens).Count();
        var union = aTokens.Union(bTokens).Count();
        return (double)intersection / union;
    }

    // True if one title contains all tokens of the other (handles subtitle variants)
    public static bool TitlesOverlap(string a, string b)
    {
        var aTokens = SignificantTokens(Normalize(a));
        var bTokens = SignificantTokens(Normalize(b));
        if (aTokens.Count == 0 || bTokens.Count == 0) return false;

        // Shorter set must be fully contained in the longer
        var shorter = aTokens.Count <= bTokens.Count ? aTokens : bTokens;
        var longer = aTokens.Count <= bTokens.Count ? bTokens : aTokens;
        return shorter.IsSubsetOf(longer);
    }

    public static bool AuthorNamesMatch(string queryAuthor, string candidateAuthor)
    {
        var qNorm = Normalize(queryAuthor);
        var cNorm = Normalize(candidateAuthor);

        if (qNorm == cNorm) return true;

        // Token-level: every token in the shorter name appears in the longer
        var qTokens = new HashSet<string>(qNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var cTokens = new HashSet<string>(cNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var shorter = qTokens.Count <= cTokens.Count ? qTokens : cTokens;
        var longer = qTokens.Count <= cTokens.Count ? cTokens : qTokens;
        return shorter.Count > 0 && shorter.IsSubsetOf(longer);
    }

    private static HashSet<string> SignificantTokens(string normalizedText)
    {
        return normalizedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToHashSet();
    }
}
