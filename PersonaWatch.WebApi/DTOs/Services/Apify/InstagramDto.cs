// PATH: WebApi/Services/Dtos/InstagramDto.cs
public class InstagramDto
{
    public string? Caption { get; set; }
    public string? OwnerFullName { get; set; }
    public string? OwnerUsername { get; set; }
    public string? Url { get; set; }

    public int? CommentsCount { get; set; }
    public string? FirstComment { get; set; }
    public int? LikesCount { get; set; }

    // ISO 8601 — örn: "2025-10-24T13:03:51.000Z"
    public string? Timestamp { get; set; }

    // İsteğe bağlı: hashtag listesi
    public List<string>? Hashtags { get; set; }
}
