using PersonaWatch.WebApi.Entities;
using PersonaWatch.WebApi.Helpers;
using PersonaWatch.WebApi.Services.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.RegularExpressions;

public class SikayetvarScannerService : IScanner
{
    public string Source => "ŞikayetvarSelenium";
    private readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    public async Task<List<NewsContent>> ScanAsync(string searchKeyword)
    {
        var results = new List<NewsContent>();
        if (string.IsNullOrWhiteSpace(searchKeyword))
            return results;

        var cleaned = Regex.Replace(searchKeyword.Trim(), @"\s+", "-");
        var searchUrl = $"https://www.sikayetvar.com/{cleaned}";

        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument($"--user-agent={_userAgent}");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");

        using var driver = new ChromeDriver(options);

        driver.Navigate().GoToUrl(searchUrl);
        await Task.Delay(3000);

        ((IJavaScriptExecutor)driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
        await Task.Delay(2000);

        var pageSource = driver.PageSource;
        Console.WriteLine(pageSource);

        var entries = driver.FindElements(By.CssSelector("article.card-v2"));
        foreach (var entry in entries)
        {
            try
            {
                var commentElement = entry.FindElement(By.CssSelector("p.complaint-description"));
                var commentText = commentElement?.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(commentText))
                    continue;

                var userElement = entry.FindElement(By.CssSelector("span.username"));
                var rawName = userElement?.GetAttribute("title")?.Trim() ?? userElement?.Text?.Trim();
                var title = string.IsNullOrWhiteSpace(rawName) ? "Şikayet Var Kullanıcısı" : $"Şikayet Var Kullanıcısı: {rawName}";

                var dataUrl = commentElement?.GetAttribute("data-url")?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(dataUrl))
                    continue;

                string decodedPath = string.Empty;

                try
                {
                    var bytes = Convert.FromBase64String(dataUrl);
                    decodedPath = System.Text.Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    continue;
                }

                var entryUrl = "https://www.sikayetvar.com" + decodedPath;
                var normalizedUrl = HelperService.NormalizeUrl(entryUrl);
                var contentHash = HelperService.ComputeMd5(commentText + normalizedUrl);

                results.Add(new NewsContent
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Summary = commentText,
                    Url = entryUrl,
                    Platform = "Şikayetvar",
                    PublishDate = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    CreatedUserName = "system",
                    RecordStatus = 'A',
                    SearchKeyword = searchKeyword,
                    ContentHash = contentHash,
                    Source = Source
                });
            }
            catch
            {
                continue;
            }
        }

        return results;
    }
}
