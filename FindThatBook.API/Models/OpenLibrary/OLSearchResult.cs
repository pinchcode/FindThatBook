using System.Text.Json.Serialization;

namespace FindThatBook.API.Models.OpenLibrary;

public class OLSearchResult
{
    [JsonPropertyName("numFound")]
    public int NumFound { get; set; }

    [JsonPropertyName("docs")]
    public List<OLDoc> Docs { get; set; } = [];
}

public class OLDoc
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";   // e.g. "/works/OL27448W"

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("author_name")]
    public List<string> AuthorNames { get; set; } = [];

    [JsonPropertyName("author_key")]
    public List<string> AuthorKeys { get; set; } = [];

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; set; }

    [JsonPropertyName("cover_i")]
    public long? CoverId { get; set; }

    [JsonPropertyName("edition_count")]
    public int EditionCount { get; set; }
}
