using PersonaWatch.WebApi.Helpers;
using PersonaWatch.WebApi.Services;
using PersonaWatch.WebApi.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

public class XApifyScannerService : IScanner
{
    private readonly ApifyService _apifyService;

    public string Source => "XApify";

    public XApifyScannerService(ApifyService apifyService)
    {
        _apifyService = apifyService;
    }

    public async Task<List<NewsContent>> ScanAsync(string searchKeyword)
    {
        var input = new
        {
            maxItems = 50,
            searchTerms = new[] { $"\"{searchKeyword}\"" },
            sort = "Latest"
        };

        var actorId = "nfp1fpt5gUlBwPcor";
        var runId = await _apifyService.StartActorAsync(actorId, input);

        string? status = null;
        int attempt = 0;
        while (status != "SUCCEEDED" && attempt < 30)
        {
            await Task.Delay(3000);
            status = await _apifyService.GetRunStatusAsync(runId);
            attempt++;
        }

        if (status != "SUCCEEDED")
            return new List<NewsContent>();

        var datasetId = await _apifyService.GetDatasetIdAsync(runId);
        if (string.IsNullOrWhiteSpace(datasetId))
            return new List<NewsContent>();

        var rawTweets = await _apifyService.GetDatasetItemsAsync<XTweetsDto>(datasetId);

        var results = rawTweets
            .Where(t => !string.IsNullOrWhiteSpace(t.Text) && !string.IsNullOrWhiteSpace(t.Url))
            .Select(t => new NewsContent
            {
                Id = Guid.NewGuid(),
                Title = t.Text!.Length > 100 ? t.Text.Substring(0, 100) : t.Text,
                Summary = t.Text,
                Url = t.Url ?? string.Empty,
                Platform = "X",
                PublishDate = ParseApifyDate(t.CreatedAt),
                CreatedDate = DateTime.UtcNow,
                CreatedUserName = "system",
                RecordStatus = 'A',
                SearchKeyword = searchKeyword,
                ContentHash = HelperService.ComputeMd5(t.Text + HelperService.NormalizeUrl(t.Url ?? string.Empty)),
                Source = Source
            })
            .ToList();

        return results;
    }

    private static DateTime ParseApifyDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.UtcNow;

        if (DateTime.TryParseExact(
            raw,
            "ddd MMM dd HH:mm:ss K yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            return parsed;
        }

        return DateTime.UtcNow;
    }
}
