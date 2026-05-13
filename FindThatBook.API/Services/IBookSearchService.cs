using FindThatBook.API.Models;

namespace FindThatBook.API.Services;

public interface IBookSearchService
{
    Task<SearchResponse> SearchAsync(string rawQuery);
}
