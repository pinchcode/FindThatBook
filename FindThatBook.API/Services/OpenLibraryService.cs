using System.Text.Json;
using FindThatBook.API.Models.OpenLibrary;

namespace FindThatBook.API.Services;

public class OpenLibraryService(HttpClient httpClient, ILogger<OpenLibraryService> logger)
    : IOpenLibraryService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string BaseUrl = "https://openlibrary.org";
    private const string Fields = "key,title,author_name,author_key,first_publish_year,cover_i,edition_count";

    public async Task<List<OLDoc>> SearchAsync(string query, int limit = 10)
    {
        try
        {
            var encoded = Uri.EscapeDataString(query);
            var url = $"{BaseUrl}/search.json?q={encoded}&fields={Fields}&limit={limit}";
            var result = await httpClient.GetFromJsonAsync<OLSearchResult>(url, JsonOpts);
            return result?.Docs ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Open Library search failed for query: {Query}", query);
            return [];
        }
    }

    public async Task<List<OLDoc>> SearchByAuthorAsync(string authorName, int limit = 10)
    {
        try
        {
            var encoded = Uri.EscapeDataString(authorName);
            var url = $"{BaseUrl}/search.json?author={encoded}&fields={Fields}&sort=editions&limit={limit}";
            var result = await httpClient.GetFromJsonAsync<OLSearchResult>(url, JsonOpts);
            return result?.Docs ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Open Library author search failed for: {Author}", authorName);
            return [];
        }
    }

    public async Task<OLWorkDetail?> GetWorkAsync(string workKey)
    {
        // workKey is like "/works/OL27448W" — strip leading slash for the URL
        var path = workKey.TrimStart('/');
        try
        {
            return await httpClient.GetFromJsonAsync<OLWorkDetail>($"{BaseUrl}/{path}.json", JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch work detail for {WorkKey}", workKey);
            return null;
        }
    }

    public async Task<OLAuthorDetail?> GetAuthorAsync(string authorKey)
    {
        var path = authorKey.TrimStart('/');
        try
        {
            return await httpClient.GetFromJsonAsync<OLAuthorDetail>($"{BaseUrl}/{path}.json", JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch author detail for {AuthorKey}", authorKey);
            return null;
        }
    }
}
