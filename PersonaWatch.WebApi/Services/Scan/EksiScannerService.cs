using PersonaWatch.WebApi.Entities;
using PersonaWatch.WebApi.Helpers;
using PersonaWatch.WebApi.Services.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.RegularExpressions;

public class EksiScannerService : IScanner
{
    public string Source => "EkşiSelenium";

    // Gerekirse Cookie ve User-Agent dışarıdan da alınabilir, şimdilik örnek olarak hardcode.
    private readonly string _cookie = "\r\nASP.NET_SessionId=cl5etmyliwtxsogbtvbdkd2p; channel-filter-preference-cookie=W3siSWQiOjEsIlByZWYiOnRydWV9LHsiSWQiOjIsIlByZWYiOnRydWV9LHsiSWQiOjQsIlByZWYiOnRydWV9LHsiSWQiOjUsIlByZWYiOnRydWV9LHsiSWQiOjEwLCJQcmVmIjpmYWxzZX0seyJJZCI6MTEsIlByZWYiOmZhbHNlfV0=; iq=3449a42f97c44e9fba4834bd6447367f"; // Postman'dan güncel olanı buraya yaz!
    private readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    public async Task<List<NewsContent>> ScanAsync(string searchKeyword)
    {
        var results = new List<NewsContent>();
        if (string.IsNullOrWhiteSpace(searchKeyword))
            return results;

        var cleaned = Regex.Replace(searchKeyword.Trim(), @"\s+", "+");
        var searchUrl = $"https://eksisozluk.com/?q={cleaned}";

        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument($"--user-agent={_userAgent}");

        using var driver = new ChromeDriver(options);

        // 1. Siteye gidip cookie ekle
        driver.Navigate().GoToUrl("https://eksisozluk.com/");
        await Task.Delay(1000);
        foreach (var pair in _cookie.Split(';'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                driver.Manage().Cookies.AddCookie(
                    new Cookie(parts[0].Trim(), parts[1].Trim(), ".eksisozluk.com", "/", DateTime.Now.AddDays(1))
                );
            }
        }

        // 2. Arama sayfasına git
        driver.Navigate().GoToUrl(searchUrl);
        await Task.Delay(1500);

        // 3. pageSource al
        var pageSource = driver.PageSource;

        // 4. dataLayer'dan başlık bilgilerini çek
        var titleMatch = Regex.Match(pageSource, @"'etitle':\s*'([^']+)'");
        var idMatch = Regex.Match(pageSource, @"'econtentid':\s*'([^']+)'");
        string baslikUrl = null;
        if (titleMatch.Success && idMatch.Success)
        {
            var etitle = titleMatch.Groups[1].Value;
            var econtentid = idMatch.Groups[1].Value;
            baslikUrl = $"https://eksisozluk.com/{etitle}--{econtentid}";
        }
        else
        {
            // Fallback: çoklu başlık
            var baslikNode = driver.FindElements(By.CssSelector("ul.topic-list li a"))
                .FirstOrDefault(e => Regex.IsMatch(e.GetAttribute("href") ?? "", @"--\d+$"));
            if (baslikNode != null)
                baslikUrl = "https://eksisozluk.com" + baslikNode.GetAttribute("href");
            else
                return results;
        }

        // 5. pageSource üzerinden son sayfa numarasını bul
        var lastPageMatch = Regex.Match(pageSource, @"<a href=""\?p=(\d+)""[^>]*class=""last""[^>]*>");
        int lastPage = 1;
        if (lastPageMatch.Success && int.TryParse(lastPageMatch.Groups[1].Value, out int page))
            lastPage = page;

        // 6. Son sayfa ve (varsa) sondan bir önceki sayfa için entry'leri çek
        var targetPages = new List<int>();
        if (lastPage > 1)
            targetPages.Add(lastPage - 1);
        targetPages.Add(lastPage); // Her durumda son sayfa

        foreach (var pageNum in targetPages.Distinct())
        {
            var pageUrl = baslikUrl + (pageNum > 1 ? $"?p={pageNum}" : "");
            driver.Navigate().GoToUrl(pageUrl);
            await Task.Delay(1500);

            var entries = driver.FindElements(By.CssSelector("li[id^='entry-']"));
            foreach (var entry in entries)
            {
                try
                {
                    var contentNode = entry.FindElement(By.CssSelector("div.content"));
                    var entryText = contentNode?.Text.Trim() ?? "";

                    var authorNode = entry.FindElement(By.CssSelector("a.entry-author"));
                    var author = authorNode?.Text.Trim() ?? "anonim";

                    var dateNode = entry.FindElement(By.CssSelector("a.entry-date"));
                    var dateStr = dateNode?.Text.Trim();
                    DateTime publishDate = DateTime.UtcNow;
                    DateTime.TryParse(dateStr, out publishDate);

                    var entryId = entry.GetAttribute("data-id");
                    var entryUrl = $"https://eksisozluk.com/entry/{entryId}";

                    var normalizedUrl = HelperService.NormalizeUrl(entryUrl);
                    var contentHash = HelperService.ComputeMd5(entryText + normalizedUrl);

                    results.Add(new NewsContent
                    {
                        Id = Guid.NewGuid(),
                        Title = author,
                        Summary = entryText,
                        Url = entryUrl,
                        Platform = "Ekşi Sözlük",
                        PublishDate = publishDate,
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
        }

        return results;
    }
}