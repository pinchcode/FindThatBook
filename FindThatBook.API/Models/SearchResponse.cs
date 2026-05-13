namespace FindThatBook.API.Models;

public class SearchResponse
{
    public string OriginalQuery { get; set; } = "";
    public QueryHypothesis? ParsedQuery { get; set; }
    public List<BookCandidate> Candidates { get; set; } = [];
    public string? ApiWarning { get; set; }
}
