namespace FindThatBook.API.Models;

public class QueryHypothesis
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public List<string> Keywords { get; set; } = [];
    public string? CorrectedQuery { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? GeminiWarning { get; set; }
}
