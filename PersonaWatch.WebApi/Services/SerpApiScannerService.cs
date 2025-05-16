using PersonaWatch.WebApi.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class SerpApiScannerService : IScanner
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _serpApiKey;

    public SerpApiScannerService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _serpApiKey = configuration["SerpApi:ApiKey"] ?? throw new ArgumentNullException("SerpApi:ApiKey is missing");
    }

    public async Task<List<NewsContent>> ScanAsync(string personName)
    {
        var engines = new[] { "google", "google_news", "google_videos" };

        var tasks = engines.Select(engine => ScanEngineAsync(engine, personName)).ToArray();

        var allResults = (await Task.WhenAll(tasks))
            .SelectMany(result => result)
            .ToList();

        return allResults;
    }

    private async Task<List<NewsContent>> ScanEngineAsync(string engine, string personName)
    {
        var results = new List<NewsContent>();
        var client = _httpClientFactory.CreateClient();

        var searchUrl = $"https://serpapi.com/search.json?engine={engine}&q={Uri.EscapeDataString(personName)}&hl=tr&gl=tr&num=100&api_key={_serpApiKey}";

        var response = await client.GetStringAsync(searchUrl);
        var json = JsonDocument.Parse(response);

        var excludedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "related_searches",
            "search_information",
            "pagination",
            "ads",
            "inline_images"
        };

        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (excludedSections.Contains(property.Name) || property.Value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in property.Value.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() :
                            item.TryGetProperty("question", out var q) ? q.GetString() : "";

                var summary = item.TryGetProperty("snippet", out var s) ? s.GetString() : "";
                var url = item.TryGetProperty("link", out var l) ? l.GetString() : "";
                var publishDate = item.TryGetProperty("date", out var d) && DateTime.TryParse(d.GetString(), out var parsedDate)
                                ? parsedDate
                                : DateTime.UtcNow;

                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(url))
                    continue;

                var normalizedUrl = NormalizeUrl(url);
                var contentHash = ComputeMd5(title.Trim() + normalizedUrl);

                results.Add(new NewsContent
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Summary = summary,
                    Url = url,
                    Platform = property.Name,
                    PublishDate = publishDate,
                    CreatedDate = DateTime.UtcNow,
                    CreatedUserName = "system",
                    RecordStatus = 'A',
                    PersonName = personName,
                    ContentHash = contentHash
                });
            }
        }

        return results;
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

            // Querystring'i temizleyebilirsin ama istersen bırak
            // uri.Query = "";

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
