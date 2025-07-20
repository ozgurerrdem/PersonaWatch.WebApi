using HtmlAgilityPack;
using System.Text;

public static class HtmlParserService
{
    public static List<NewsContent> ExtractTweets(string html, string personName)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tweets = new List<NewsContent>();
        var articles = doc.DocumentNode.SelectNodes("//article");

        if (articles == null) return tweets;

        foreach (var article in articles.Take(10))
        {
            var contentNode = article.SelectSingleNode(".//div[@data-testid='tweetText']");
            var timeNode = article.SelectSingleNode(".//time");
            var linkNode = timeNode?.ParentNode;

            if (contentNode == null || timeNode == null || linkNode == null) continue;

            var text = contentNode.InnerText.Trim();
            var date = DateTime.Parse(timeNode.Attributes["datetime"].Value);
            var link = "https://twitter.com" + linkNode.Attributes["href"].Value;

            tweets.Add(new NewsContent
            {
                Title = "Tweet",
                Summary = text,
                Url = link,
                Platform = "X",
                PublishDate = date,
                PersonName = personName,
                Source = "ScrapFly",
                ContentHash = GenerateHash(text + link),
                RecordStatus = 'A'
            });
        }

        return tweets;
    }

    private static string GenerateHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }
}
