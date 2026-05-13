using FindThatBook.API.Models;

namespace FindThatBook.API.Services;

public interface IGeminiService
{
    Task<QueryHypothesis> ParseQueryAsync(string rawQuery);
}
