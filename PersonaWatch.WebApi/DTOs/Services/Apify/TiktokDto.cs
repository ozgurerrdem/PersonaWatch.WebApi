// PATH: WebApi/Services/Dtos/TiktokDto.cs
using System.Text.Json.Serialization;

public class TiktokDto
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("webVideoUrl")]
    public string? WebVideoUrl { get; set; }

    [JsonPropertyName("createTimeISO")]
    public string? CreateTimeISO { get; set; }

    // Sayaçlar
    [JsonPropertyName("diggCount")]
    public int? DiggCount { get; set; }          // like

    [JsonPropertyName("shareCount")]
    public int? ShareCount { get; set; }         // share → RtCount

    [JsonPropertyName("playCount")]
    public int? PlayCount { get; set; }          // view

    [JsonPropertyName("commentCount")]
    public int? CommentCount { get; set; }

    [JsonPropertyName("collectCount")]
    public int? CollectCount { get; set; }       // bookmark

    // Yazar bilgileri (nested ve flat fallback)
    [JsonPropertyName("authorMeta")]
    public TiktokAuthorMeta? AuthorMeta { get; set; }

    // Bazı datasetlerde düz alan olarak gelebilir (örnek JSON’da “authorMeta.name” şeklinde görülebilir)
    [JsonPropertyName("authorMeta.name")]
    public string? AuthorNameFlat { get; set; }
}

public class TiktokAuthorMeta
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }
}
