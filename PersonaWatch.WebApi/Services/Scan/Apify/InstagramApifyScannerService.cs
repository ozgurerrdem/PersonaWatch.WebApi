using PersonaWatch.WebApi.Services;
using PersonaWatch.WebApi.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

public class InstagramApifyScannerService : IScanner
{
    private readonly ApifyService _apifyService;
    public string Source => "InstagramApify";

    public InstagramApifyScannerService(ApifyService apifyService)
    {
        _apifyService = apifyService;
    }

    public async Task<List<NewsContent>> ScanAsync(string personName)
    {
        var results = new List<NewsContent>();
        var actorId = "reGe1ST3OBgYZSsZJ";
        var requestTypes = new[] { "posts", "stories" };

        foreach (var type in requestTypes)
        {
            var input = new
            {
                hashtags = new[] { personName.Replace(" ", "").ToLowerInvariant() },
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
                    .Where(p => !string.IsNullOrWhiteSpace(p.Caption) && !string.IsNullOrWhiteSpace(p.Url))
                    .Select(p => new NewsContent
                    {
                        Id = Guid.NewGuid(),
                        Title = p.Caption!.Length > 100 ? p.Caption.Substring(0, 100) : p.Caption,
                        Summary = p.Caption,
                        Url = p.Url,
                        Platform = "Instagram",
                        PublishDate = ParseIsoDate(p.Timestamp),
                        CreatedDate = DateTime.UtcNow,
                        CreatedUserName = "system",
                        RecordStatus = 'A',
                        PersonName = personName,
                        ContentHash = ComputeMd5(p.Caption + NormalizeUrl(p.Url)),
                        Source = Source
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
