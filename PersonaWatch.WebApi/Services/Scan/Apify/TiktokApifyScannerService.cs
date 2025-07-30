using PersonaWatch.WebApi.Services;
using PersonaWatch.WebApi.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

public class TiktokApifyScannerService : IScanner
{
    private readonly ApifyService _apifyService;
    public string Source => "TiktokApify";

    public TiktokApifyScannerService(ApifyService apifyService)
    {
        _apifyService = apifyService;
    }

    public async Task<List<NewsContent>> ScanAsync(string personName)
    {
        var results = new List<NewsContent>();
        var actorId = "GdWCkxBtKWOsKjdch";

        var input = new
        {
            excludePinnedPosts = false,
            hashtags = new[] { personName },
            proxyCountryCode = "None",
            resultsPerPage = 100,
            scrapeRelatedVideos = false,
            shouldDownloadAvatars = false,
            shouldDownloadCovers = false,
            shouldDownloadMusicCovers = false,
            shouldDownloadSlideshowImages = false,
            shouldDownloadSubtitles = false,
            shouldDownloadVideos = false,
            profileScrapeSections = new[] { "videos" },
            profileSorting = "latest",
            searchSection = string.Empty,
            maxProfilesPerQuery = 10
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

        if (status != "SUCCEEDED") return results;

        var datasetId = await _apifyService.GetDatasetIdAsync(runId);
        if (string.IsNullOrWhiteSpace(datasetId)) return results;

        var rawItems = await _apifyService.GetDatasetItemsAsync<TiktokDto>(datasetId);

        results.AddRange(
            rawItems
                .Where(p => !string.IsNullOrWhiteSpace(p.Text) && !string.IsNullOrWhiteSpace(p.WebVideoUrl))
                .Select(p => new NewsContent
                {
                    Id = Guid.NewGuid(),
                    Title = p.AuthorMeta?.Name ?? "TikTok Gönderisi",
                    Summary = p.Text,
                    Url = NormalizeUrl(p.WebVideoUrl),
                    Platform = "TikTok",
                    PublishDate = ConvertToUtc(p.CreateTimeISO),
                    CreatedDate = DateTime.UtcNow,
                    CreatedUserName = "system",
                    RecordStatus = 'A',
                    PersonName = personName,
                    ContentHash = ComputeMd5(p.Text + NormalizeUrl(p.WebVideoUrl)),
                    Source = Source
                })
        );

        return results;
    }

    private static DateTime ConvertToUtc(string? isoDate)
    {
        if (DateTime.TryParse(isoDate, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
            return dt.ToUniversalTime();

        return DateTime.UtcNow;
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";

        try
        {
            var uri = new UriBuilder(url)
            {
                Scheme = "https",
                Port = -1
            };

            var host = uri.Host.Replace("www.", "").Replace("m.", "");
            uri.Host = host;

            return uri.Uri.AbsoluteUri.TrimEnd('/');
        }
        catch
        {
            return url;
        }
    }

    private static string ComputeMd5(string input)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input.ToLowerInvariant().Trim());
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes);
    }
}
