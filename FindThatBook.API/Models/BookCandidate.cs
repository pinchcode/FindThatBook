using System.Text.Json.Serialization;

namespace FindThatBook.API.Models;

public class BookCandidate
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int? FirstPublishYear { get; set; }
    public string WorkKey { get; set; } = "";
    public string WorkUrl { get; set; } = "";
    public string? CoverImageUrl { get; set; }
    public string Explanation { get; set; } = "";

    // Scoring / intermediate data — excluded from API response
    [JsonIgnore] public int MatchScore { get; set; }
    [JsonIgnore] public List<string> AllAuthors { get; set; } = [];
    [JsonIgnore] public bool AuthorIsPrimary { get; set; }
    [JsonIgnore] public string NormalizedTitle { get; set; } = "";
}
