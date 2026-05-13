using FindThatBook.API.Models;

namespace FindThatBook.API.Services;

public interface IGeminiService
{
    Task<QueryHypothesis> ParseQueryAsync(string rawQuery);
    Task<List<BookCandidate>> RerankWithExplanationsAsync(string rawQuery, QueryHypothesis hypothesis, List<BookCandidate> candidates);
}
