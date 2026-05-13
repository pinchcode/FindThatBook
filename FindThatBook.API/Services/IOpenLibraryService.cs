using FindThatBook.API.Models.OpenLibrary;

namespace FindThatBook.API.Services;

public interface IOpenLibraryService
{
    Task<List<OLDoc>> SearchAsync(string query, int limit = 10);
    Task<List<OLDoc>> SearchByAuthorAsync(string authorName, int limit = 10);
    Task<OLWorkDetail?> GetWorkAsync(string workKey);
    Task<OLAuthorDetail?> GetAuthorAsync(string authorKey);
}
