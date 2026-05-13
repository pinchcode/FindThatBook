using System.Text.Json.Serialization;

namespace FindThatBook.API.Models.OpenLibrary;

public class OLWorkDetail
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("authors")]
    public List<OLWorkAuthorEntry> Authors { get; set; } = [];
}

public class OLWorkAuthorEntry
{
    [JsonPropertyName("author")]
    public OLKeyRef? Author { get; set; }

    [JsonPropertyName("type")]
    public OLKeyRef? Type { get; set; }
}

public class OLKeyRef
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";
}
