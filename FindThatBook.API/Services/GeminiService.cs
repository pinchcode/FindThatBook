using System.Text;
using System.Text.Json;
using FindThatBook.API.Models;

namespace FindThatBook.API.Services;

public class GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    : IGeminiService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private string ApiKey => configuration["Gemini:ApiKey"]
        ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");

    private string Model => configuration["Gemini:Model"] ?? "gemini-2.5-flash";

    public async Task<QueryHypothesis> ParseQueryAsync(string rawQuery)
    {
        // Use $$""" so single { } are literals; interpolations use {{ }}
        var prompt = $$"""
            You are a book search assistant. A user typed a messy, inconsistent query that might reference a book by title, author, or a mix of both with extra noise.

            Query: "{{rawQuery}}"

            Extract structured information and return a JSON object with these fields:
            - "title": the most likely book title with spelling corrected (null if not identifiable)
            - "author": the most likely author name with spelling corrected (null if not identifiable)
            - "keywords": array of other relevant search terms like edition descriptors, years, etc. (empty array if none)
            - "correctedQuery": the full query rewritten with all spelling mistakes fixed (always provide this)

            Rules:
            - Correct ALL spelling errors aggressively (e.g. "spagotti" → "spaghetti", "hemmingway" → "Hemingway", "tolkein" → "Tolkien")
            - Resolve obvious partial references: "huckleberry" likely means "The Adventures of Huckleberry Finn", "austen bennet" likely means Pride and Prejudice by Jane Austen
            - "tolkien" alone likely means author J.R.R. Tolkien; "hobbit" alone likely means the title "The Hobbit"
            - Include only what you're reasonably confident about for title/author; prefer null over a wrong guess
            - Always provide correctedQuery even if only whitespace/punctuation changed

            Return only valid JSON, no other text. Example: {"title":"The Hobbit","author":"J.R.R. Tolkien","keywords":["illustrated","1937"],"correctedQuery":"tolkien hobbit illustrated 1937"}
            """;

        try
        {
            var responseText = await CallGeminiAsync(prompt);
            var hypothesis = JsonSerializer.Deserialize<QueryHypothesis>(responseText, JsonOpts)
                             ?? new QueryHypothesis();
            hypothesis.Keywords ??= [];
            return hypothesis;
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
        {
            logger.LogWarning(ex, "Gemini query parsing failed: rate limited");
            var fallback = FallbackParse(rawQuery);
            fallback.GeminiWarning = "Gemini AI is rate-limited (5 requests/minute on the free tier). Results may be less accurate — please wait a moment and try again.";
            return fallback;
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 403)
        {
            logger.LogWarning(ex, "Gemini query parsing failed: forbidden — check your API key and model access");
            var fallback = FallbackParse(rawQuery);
            fallback.GeminiWarning = "Gemini AI access denied — please check your API key is valid and has access to the configured model.";
            return fallback;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gemini query parsing failed; using fallback");
            var fallback = FallbackParse(rawQuery);
            fallback.GeminiWarning = "Gemini AI is temporarily unavailable. Results may be less accurate.";
            return fallback;
        }
    }

    private async Task<string> CallGeminiAsync(string prompt)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={ApiKey}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.1 // almost fully deterministic ... don't be too loose here!
            }
        };

        var json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "{}";

        return text.Trim();
    }

    private static QueryHypothesis FallbackParse(string rawQuery)
    {
        var yearMatch = System.Text.RegularExpressions.Regex.Match(rawQuery, @"\b(1[5-9]\d\d|20\d\d)\b");
        var withoutYear = System.Text.RegularExpressions.Regex.Replace(rawQuery, @"\b(1[5-9]\d\d|20\d\d)\b", "").Trim();

        return new QueryHypothesis
        {
            Title = withoutYear.Length > 0 ? withoutYear : null,
            Author = null,
            Keywords = yearMatch.Success ? [yearMatch.Value] : []
        };
    }
}
