using PersonaWatch.WebApi.Helpers;
using PersonaWatch.WebApi.Services;
using PersonaWatch.WebApi.Services.Interfaces;

public class FacebookApifyScannerService : IScanner
{
    private readonly ApifyService _apifyService;
    public string Source => "FacebookApify";

    public FacebookApifyScannerService(ApifyService apifyService)
    {
        _apifyService = apifyService;
    }

    public async Task<List<NewsContent>> ScanAsync(string searchKeyword)
    {
        var results = new List<NewsContent>();
        var actorId = "4YfcIWyRtJHJ5Ha3a";

        var input = new
        {
            searchQuery = searchKeyword,
            maxPosts = 10
        };

        var runId = await _apifyService.StartActorAsync(actorId, input);

        string? status = null;
        int attempt = 0;
        while (status != "SUCCEEDED" && attempt < 30)
        {
            await Task.Delay(3000);
            status = await _apifyService.GetRunStatusAsync(runId);
            attempt++;
        }

        if (status != "SUCCEEDED") return results;

        var datasetId = await _apifyService.GetDatasetIdAsync(runId);
        if (string.IsNullOrWhiteSpace(datasetId)) return results;

        var rawItems = await _apifyService.GetDatasetItemsAsync<FacebookDto>(datasetId);

        results.AddRange(
            rawItems
                .Where(p => !string.IsNullOrWhiteSpace(p.Text) && !string.IsNullOrWhiteSpace(p.Url))
                .Select(p => new NewsContent
                {
                    Id = Guid.NewGuid(),
                    Title = p.PageName ?? "Facebook Gönderisi",
                    Summary = p.Text ?? string.Empty,
                    Url = p.Url ?? string.Empty,
                    Platform = "Facebook",
                    PublishDate = ConvertFromUnix(p.Timestamp),
                    CreatedDate = DateTime.UtcNow,
                    CreatedUserName = "system",
                    RecordStatus = 'A',
                    SearchKeyword = searchKeyword,
                    ContentHash = HelperService.ComputeMd5(p.Text + HelperService.NormalizeUrl(p.Url ?? string.Empty)),
                    Source = Source
                })
        );

        return results;
    }

    private static DateTime ConvertFromUnix(long? timestamp)
    {
        return timestamp.HasValue && timestamp > 0
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).UtcDateTime
            : DateTime.UtcNow;
    }
}
