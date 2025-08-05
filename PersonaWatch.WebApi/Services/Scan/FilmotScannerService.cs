using PersonaWatch.WebApi.Entities;
using PersonaWatch.WebApi.Entities.Services.Filmot;
using PersonaWatch.WebApi.Helpers;
using PersonaWatch.WebApi.Services.Interfaces;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public class FilmotScannerService : IScanner
{
    private readonly IHttpClientFactory _httpClientFactory;

    public string Source => "Filmot";

    public FilmotScannerService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<NewsContent>> ScanAsync(string searchKeyword)
    {
        var results = new List<NewsContent>();

        if (string.IsNullOrWhiteSpace(searchKeyword))
            return results;

        var encoded = Uri.EscapeDataString(Regex.Replace(searchKeyword.Trim(), @"\s+", "+"));

        var url = $"https://filmot.com/search/%22{encoded}%22/1?sortField=uploaddate&sortOrder=desc&gridView=1&";

        var client = _httpClientFactory.CreateClient();

        string html;
        try
        {
            html = await client.GetStringAsync(url);
        }
        catch
        {
            return results;
        }

        var match = Regex.Match(html, @"window\.results\s*=\s*(\{.*?\});", RegexOptions.Singleline);
        if (!match.Success)
            return results;

        var jsonStr = match.Groups[1].Value;
        Dictionary<string, FilmotVideoResult>? resultDict;
        try
        {
            resultDict = JsonSerializer.Deserialize<Dictionary<string, FilmotVideoResult>>(jsonStr, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (resultDict == null)
                return results;
        }
        catch
        {
            return results;
        }


        foreach (var video in resultDict.Values)
        {
            foreach (var hit in video.Hits)
            {
                var fullText = $"{hit.CtxBefore?.Trim()} {hit.Token?.Trim()} {hit.CtxAfter?.Trim()}".Trim();
                var urlWithTimestamp = $"https://www.youtube.com/watch?v={video.Vid}&t={(int)hit.Start}s";
                var baseUrl = $"https://www.youtube.com/watch?v={video.Vid}";
                var normalizedUrl = HelperService.NormalizeUrl(baseUrl);
                var contentHash = HelperService.ComputeMd5((hit.Token ?? "") + normalizedUrl);

                results.Add(new NewsContent
                {
                    Id = Guid.NewGuid(),
                    Title = hit.Token ?? string.Empty,
                    Summary = fullText,
                    Url = urlWithTimestamp,
                    Platform = "YouTube",
                    PublishDate = DateTime.UtcNow,
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
