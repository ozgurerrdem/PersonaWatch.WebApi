using System.Text.Json.Serialization;

public class TiktokDto
{
    public AuthorMeta? AuthorMeta { get; set; }

    public string? Text { get; set; }

    public string? WebVideoUrl { get; set; }

    public string? CreateTimeISO { get; set; }
}

public class AuthorMeta
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}