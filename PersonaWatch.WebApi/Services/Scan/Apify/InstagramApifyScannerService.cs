// PATH: WebApi/Services/InstagramApifyScannerService.cs
using PersonaWatch.WebApi.Entities;
using PersonaWatch.WebApi.Helpers;
using PersonaWatch.WebApi.Services;
using PersonaWatch.WebApi.Services.Interfaces;

public class InstagramApifyScannerService : IScanner
{
    private readonly ApifyService _apifyService;
    public string Source => "InstagramApify";

    public InstagramApifyScannerService(ApifyService apifyService)
    {
        _apifyService = apifyService;
    }

    public async Task<List<NewsContent>> ScanAsync(string searchKeyword)
    {
        var results = new List<NewsContent>();
        var actorId = "reGe1ST3OBgYZSsZJ";
        var requestTypes = new[] { "posts", "stories" };

        foreach (var type in requestTypes)
        {
            var input = new
            {
                hashtags = new[] { searchKeyword.Replace(" ", "").ToLowerInvariant() },
                resultsLimit = 20,
                resultsType = type
            };

            var runId = await _apifyService.StartActorRawAsync(actorId, input);

            string? status = null;
            int attempt = 0;
            while (status != "SUCCEEDED" && attempt < 30)
            {
                await Task.Delay(3000);
                status = await _apifyService.GetRunStatusAsync(runId);
                attempt++;
            }

            if (status != "SUCCEEDED") continue;

            var datasetId = await _apifyService.GetDatasetIdAsync(runId);
            if (string.IsNullOrWhiteSpace(datasetId)) continue;

            var rawItems = await _apifyService.GetDatasetItemsAsync<InstagramDto>(datasetId);

            results.AddRange(
                rawItems
                    .Where(p => !string.IsNullOrWhiteSpace(p.Url) && (!string.IsNullOrWhiteSpace(p.Caption) || !string.IsNullOrWhiteSpace(p.OwnerUsername) || !string.IsNullOrWhiteSpace(p.OwnerFullName)))
                    .Select(p =>
                    {
                        var caption = p.Caption ?? string.Empty;
                        var title = caption.Length > 100 ? caption.Substring(0, 100) : caption;
                        if (string.IsNullOrWhiteSpace(title))
                            title = p.OwnerUsername ?? p.OwnerFullName ?? "Instagram Gönderisi";

                        var url = p.Url ?? string.Empty;

                        return new NewsContent
                        {
                            Id = Guid.NewGuid(),

                            Title = title,
                            Summary = caption,
                            Url = url,

                            Platform = "Instagram",
                            PublishDate = ParseIsoDate(p.Timestamp),
                            SearchKeyword = searchKeyword,

                            ContentHash = HelperService.ComputeMd5(
                                (caption) + HelperService.NormalizeUrl(url)
                            ),

                            Source = Source,
                            Publisher = p.OwnerUsername ?? p.OwnerFullName ?? string.Empty,

                            // Sayaçlar
                            LikeCount     = p.LikesCount    ?? 0,
                            CommentCount  = p.CommentsCount ?? 0,
                            RtCount       = 0,
                            QuoteCount    = 0,
                            BookmarkCount = 0,
                            DislikeCount  = 0,
                            ViewCount     = 0,

                            // BaseEntity
                            CreatedDate = DateTime.UtcNow,
                            CreatedUserName = "system",
                            RecordStatus = 'A'
                        };
                    })
            );
        }

        return results;
    }

    private static DateTime ParseIsoDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.UtcNow;

        if (DateTime.TryParse(raw, out var parsed))
            return parsed;

        return DateTime.UtcNow;
    }
}
