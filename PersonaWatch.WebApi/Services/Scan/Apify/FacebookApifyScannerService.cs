using PersonaWatch.WebApi.Services;
using PersonaWatch.WebApi.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

public class FacebookApifyScannerService : IScanner
{
    private readonly ApifyService _apifyService;
    public string Source => "FacebookApify";

    public FacebookApifyScannerService(ApifyService apifyService)
    {
        _apifyService = apifyService;
    }

    public async Task<List<NewsContent>> ScanAsync(string personName)
    {
        var results = new List<NewsContent>();
        var actorId = "4YfcIWyRtJHJ5Ha3a";

        var input = new
        {
            searchQuery = personName,
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
                    Summary = p.Text,
                    Url = p.Url,
                    Platform = "Facebook",
                    PublishDate = ConvertFromUnix(p.Timestamp),
                    CreatedDate = DateTime.UtcNow,
                    CreatedUserName = "system",
                    RecordStatus = 'A',
                    PersonName = personName,
                    ContentHash = ComputeMd5(p.Text + NormalizeUrl(p.Url)),
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
