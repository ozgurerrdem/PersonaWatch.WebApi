using PersonaWatch.WebApi.Helpers;
using PersonaWatch.WebApi.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class SerpApiScannerService : IScanner
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _serpApiKey;

    public string Source => "SerpApi";

    public SerpApiScannerService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _serpApiKey = configuration["SerpApi:ApiKey"] ?? throw new ArgumentNullException("SerpApi:ApiKey is missing");
    }

    public async Task<List<NewsContent>> ScanAsync(string searchKeyword)
    {
        var engines = new[] { "google", "google_news", "google_videos" };

        var tasks = engines.Select(engine => ScanEngineAsync(engine, searchKeyword)).ToArray();

        var allResults = (await Task.WhenAll(tasks))
            .SelectMany(result => result)
            .ToList();

        return allResults;
    }

    private async Task<List<NewsContent>> ScanEngineAsync(string engine, string searchKeyword)
    {
        var results = new List<NewsContent>();
        var client = _httpClientFactory.CreateClient();

        var quotedQuery = $"\"{searchKeyword}\"";
        var searchUrl = $"https://serpapi.com/search.json?engine={engine}&q={Uri.EscapeDataString(quotedQuery)}&hl=tr&gl=tr&num=100&api_key={_serpApiKey}";

        var response = await client.GetStringAsync(searchUrl);
        var json = JsonDocument.Parse(response);

        var excludedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "related_searches",
            "search_information",
            "pagination",
            "ads",
            "inline_images",
            "menu_links",
            "related_topics"
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

                if (!string.IsNullOrEmpty(url) && url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(url))
                    continue;

                var normalizedUrl = HelperService.NormalizeUrl(url ?? string.Empty);
                var contentHash = HelperService.ComputeMd5((title ?? string.Empty).Trim() + normalizedUrl);

                results.Add(new NewsContent
                {
                    Id = Guid.NewGuid(),
                    Title = title ?? string.Empty,
                    Summary = summary ?? string.Empty,
                    Url = url ?? string.Empty,
                    Platform = property.Name,
                    PublishDate = publishDate,
                    CreatedDate = DateTime.UtcNow,
                    CreatedUserName = "system",
                    RecordStatus = 'A',
                    SearchKeyword = searchKeyword,
                    ContentHash = contentHash,
                    Source = Source
                });
            }

        }

        return results;
    }
}
