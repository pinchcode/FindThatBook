using FindThatBook.API.Helpers;
using FindThatBook.API.Models;
using FindThatBook.API.Models.OpenLibrary;

namespace FindThatBook.API.Services;

public class BookSearchService(
    IGeminiService gemini,
    IOpenLibraryService openLibrary,
    ILogger<BookSearchService> logger) : IBookSearchService
{
    private const int MaxCandidates = 5;

    // Scoring tiers (higher = stronger match)
    private const int ScoreExactTitlePrimaryAuthor = 100;
    private const int ScoreExactTitleContributorAuthor = 80;
    private const int ScoreNearTitlePrimaryAuthor = 70;
    private const int ScoreNearTitleContributorAuthor = 55;
    private const int ScoreTitleOnly = 50;
    private const int ScoreAuthorFallback = 40;

    public async Task<SearchResponse> SearchAsync(string rawQuery)
    {
        // Step 1: AI-powered field extraction
        var hypothesis = await gemini.ParseQueryAsync(rawQuery);
        logger.LogInformation("Parsed query: title={Title}, author={Author}, correctedQuery={CQ}",
            hypothesis.Title, hypothesis.Author, hypothesis.CorrectedQuery);

        List<BookCandidate> candidates;

        // effectiveQuery: use Gemini's spelling-corrected version when available
        var effectiveQuery = !string.IsNullOrWhiteSpace(hypothesis.CorrectedQuery)
            ? hypothesis.CorrectedQuery
            : rawQuery;

        if (!string.IsNullOrWhiteSpace(hypothesis.Title))
        {
            // Run title search and corrected-query search in parallel so a misspelled
            // title doesn't silently block the corrected path
            var titleTask = SearchByTitleAsync(hypothesis);
            var correctedTask = !string.IsNullOrWhiteSpace(hypothesis.CorrectedQuery)
                ? KeywordSearchAsync(hypothesis.CorrectedQuery)
                : Task.FromResult(new List<BookCandidate>());

            await Task.WhenAll(titleTask, correctedTask);

            // Merge, preferring title-search results but filling in from the corrected search
            var seen = new HashSet<string>(titleTask.Result.Select(c => c.WorkKey));
            candidates = [..titleTask.Result,
                ..correctedTask.Result.Where(c => !seen.Contains(c.WorkKey))];
        }
        else if (!string.IsNullOrWhiteSpace(hypothesis.Author))
        {
            candidates = await AuthorFallbackAsync(hypothesis.Author);
        }
        else
        {
            candidates = await KeywordSearchAsync(effectiveQuery);
        }

        candidates = candidates
            .OrderByDescending(c => c.MatchScore)
            .Take(MaxCandidates)
            .ToList();

        return new SearchResponse
        {
            OriginalQuery = rawQuery,
            ParsedQuery = hypothesis,
            Candidates = candidates,
            ApiWarning = hypothesis.GeminiWarning
        };
    }

    // --- Search paths ---

    private async Task<List<BookCandidate>> SearchByTitleAsync(QueryHypothesis hypothesis)
    {
        var titleQuery = hypothesis.Title!;
        if (!string.IsNullOrWhiteSpace(hypothesis.Author))
            titleQuery += " " + hypothesis.Author;

        var docs = await openLibrary.SearchAsync(titleQuery, limit: 15);

        // De-duplicate by work key before making detail requests
        var uniqueDocs = docs
            .Where(d => !string.IsNullOrEmpty(d.Key))
            .GroupBy(d => d.Key)
            .Select(g => g.First())
            .ToList();

        // Fetch work details concurrently to resolve primary authors
        var workTasks = uniqueDocs
            .Select(d => openLibrary.GetWorkAsync(d.Key))
            .ToList();

        var workDetails = await Task.WhenAll(workTasks);
        var workMap = workDetails
            .Where(w => w != null)
            .ToDictionary(w => w!.Key, w => w!);

        // Restore docs to original order, using the unique set
        docs = uniqueDocs;

        // Resolve primary author names for each work
        var authorNameTasks = new Dictionary<string, Task<OLAuthorDetail?>>();
        foreach (var work in workMap.Values)
        {
            foreach (var entry in work.Authors)
            {
                var key = entry.Author?.Key;
                if (key != null && !authorNameTasks.ContainsKey(key))
                    authorNameTasks[key] = openLibrary.GetAuthorAsync(key);
            }
        }
        await Task.WhenAll(authorNameTasks.Values);
        var authorNames = authorNameTasks.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Result?.Name ?? "");

        var seen = new HashSet<string>();
        var candidates = new List<BookCandidate>();

        foreach (var doc in docs)
        {
            if (string.IsNullOrEmpty(doc.Key) || !seen.Add(doc.Key)) continue;

            workMap.TryGetValue(doc.Key, out var workDetail);

            // Primary authors come from the canonical work record
            var primaryAuthorKeys = workDetail?.Authors
                .Select(a => a.Author?.Key)
                .Where(k => k != null)
                .ToHashSet() ?? [];

            var primaryAuthorNames = primaryAuthorKeys
                .Select(k => authorNames.GetValueOrDefault(k!, ""))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            // Determine the display author: prefer primary; fall back to OL search author_name
            var displayAuthor = primaryAuthorNames.FirstOrDefault()
                ?? doc.AuthorNames.FirstOrDefault()
                ?? "";

            var allAuthors = doc.AuthorNames.Distinct().ToList();

            bool authorIsPrimary = false;
            bool authorMatchFound = false;

            if (!string.IsNullOrWhiteSpace(hypothesis.Author))
            {
                // Check primary authors first
                authorIsPrimary = primaryAuthorNames.Any(n =>
                    TextNormalizer.AuthorNamesMatch(hypothesis.Author, n));

                if (!authorIsPrimary)
                {
                    // Check all listed (contributors)
                    authorMatchFound = allAuthors.Any(n =>
                        TextNormalizer.AuthorNamesMatch(hypothesis.Author, n));
                }
                else
                {
                    authorMatchFound = true;
                }
            }

            var score = ComputeScore(hypothesis, doc, authorMatchFound, authorIsPrimary);
            var explanation = BuildExplanation(hypothesis, doc, primaryAuthorNames, allAuthors, authorIsPrimary, authorMatchFound);

            candidates.Add(new BookCandidate
            {
                Title = doc.Title,
                Author = displayAuthor,
                FirstPublishYear = doc.FirstPublishYear,
                WorkKey = doc.Key,
                WorkUrl = $"https://openlibrary.org{doc.Key}",
                CoverImageUrl = doc.CoverId.HasValue
                    ? $"https://covers.openlibrary.org/b/id/{doc.CoverId}-M.jpg"
                    : null,
                Explanation = explanation,
                MatchScore = score,
                AllAuthors = allAuthors,
                AuthorIsPrimary = authorIsPrimary,
                NormalizedTitle = TextNormalizer.Normalize(doc.Title)
            });
        }

        return candidates;
    }

    private async Task<List<BookCandidate>> AuthorFallbackAsync(string authorName)
    {
        var docs = await openLibrary.SearchByAuthorAsync(authorName, limit: 10);

        return docs
            .Where(d => !string.IsNullOrEmpty(d.Key))
            .GroupBy(d => d.Key)
            .Select(g => g.First())
            .Select(doc =>
            {
                var displayAuthor = doc.AuthorNames.FirstOrDefault() ?? "";
                return new BookCandidate
                {
                    Title = doc.Title,
                    Author = displayAuthor,
                    FirstPublishYear = doc.FirstPublishYear,
                    WorkKey = doc.Key,
                    WorkUrl = $"https://openlibrary.org{doc.Key}",
                    CoverImageUrl = doc.CoverId.HasValue
                        ? $"https://covers.openlibrary.org/b/id/{doc.CoverId}-M.jpg"
                        : null,
                    Explanation = $"Author-only match; top work by {displayAuthor}.",
                    MatchScore = ScoreAuthorFallback,
                    AllAuthors = doc.AuthorNames,
                    AuthorIsPrimary = false
                };
            })
            .ToList();
    }

    private async Task<List<BookCandidate>> KeywordSearchAsync(string rawQuery)
    {
        var docs = await openLibrary.SearchAsync(rawQuery, limit: 10);

        return docs
            .Where(d => !string.IsNullOrEmpty(d.Key))
            .GroupBy(d => d.Key)
            .Select(g => g.First())
            .Select(doc =>
            {
                var displayAuthor = doc.AuthorNames.FirstOrDefault() ?? "";
                return new BookCandidate
                {
                    Title = doc.Title,
                    Author = displayAuthor,
                    FirstPublishYear = doc.FirstPublishYear,
                    WorkKey = doc.Key,
                    WorkUrl = $"https://openlibrary.org{doc.Key}",
                    CoverImageUrl = doc.CoverId.HasValue
                        ? $"https://covers.openlibrary.org/b/id/{doc.CoverId}-M.jpg"
                        : null,
                    Explanation = $"Keyword match for \"{rawQuery}\".",
                    MatchScore = ScoreAuthorFallback,
                    AllAuthors = doc.AuthorNames,
                    AuthorIsPrimary = false
                };
            })
            .ToList();
    }

    // --- Scoring ---

    private int ComputeScore(QueryHypothesis hypothesis, OLDoc doc, bool authorMatch, bool authorIsPrimary)
    {
        var titleNorm = TextNormalizer.Normalize(doc.Title);
        var queryTitleNorm = TextNormalizer.Normalize(hypothesis.Title!);

        bool exactTitle = titleNorm == queryTitleNorm
            || TextNormalizer.TitlesOverlap(doc.Title, hypothesis.Title!);
        bool nearTitle = !exactTitle && TextNormalizer.TitleSimilarity(doc.Title, hypothesis.Title!) >= 0.5;

        if (string.IsNullOrWhiteSpace(hypothesis.Author))
        {
            return exactTitle ? ScoreTitleOnly + 10 : nearTitle ? ScoreTitleOnly : ScoreAuthorFallback;
        }

        return (exactTitle, nearTitle, authorIsPrimary, authorMatch) switch
        {
            (true, _, true, _) => ScoreExactTitlePrimaryAuthor,
            (true, _, false, true) => ScoreExactTitleContributorAuthor,
            (true, _, false, false) => ScoreTitleOnly,
            (false, true, true, _) => ScoreNearTitlePrimaryAuthor,
            (false, true, false, true) => ScoreNearTitleContributorAuthor,
            _ => ScoreAuthorFallback
        };
    }

    // --- Explanation (rule-based; Gemini re-ranking overwrites this) ---

    private static string BuildExplanation(
        QueryHypothesis hypothesis,
        OLDoc doc,
        List<string> primaryAuthors,
        List<string> allAuthors,
        bool authorIsPrimary,
        bool authorMatchFound)
    {
        var parts = new List<string>();

        var similarity = hypothesis.Title != null
            ? TextNormalizer.TitleSimilarity(doc.Title, hypothesis.Title)
            : 0;

        if (hypothesis.Title != null)
        {
            if (TextNormalizer.TitlesOverlap(doc.Title, hypothesis.Title))
                parts.Add("Exact title match");
            else if (similarity >= 0.5)
                parts.Add($"Partial title match ({similarity:P0} token overlap)");
            else
                parts.Add("Weak title similarity");
        }

        if (!string.IsNullOrWhiteSpace(hypothesis.Author))
        {
            var primaryName = primaryAuthors.FirstOrDefault();
            if (authorIsPrimary && primaryName != null)
                parts.Add($"{primaryName} is the primary author");
            else if (authorMatchFound)
            {
                var contributorMatch = allAuthors.FirstOrDefault(n =>
                    TextNormalizer.AuthorNamesMatch(hypothesis.Author!, n));
                parts.Add($"{contributorMatch} is listed as a contributor");
            }

            // Call out any non-primary authors
            var contributors = allAuthors
                .Where(n => !primaryAuthors.Any(p => TextNormalizer.AuthorNamesMatch(p, n)))
                .Take(2)
                .ToList();
            if (contributors.Count > 0 && authorIsPrimary)
                parts.Add($"{string.Join(", ", contributors)} listed as contributor(s)");
        }

        return parts.Count > 0 ? string.Join("; ", parts) + "." : "Retrieved from Open Library search.";
    }
}
