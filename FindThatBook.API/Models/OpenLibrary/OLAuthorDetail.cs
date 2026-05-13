using System.Text.Json.Serialization;

namespace FindThatBook.API.Models.OpenLibrary;

public class OLAuthorDetail
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("personal_name")]
    public string? PersonalName { get; set; }
}
