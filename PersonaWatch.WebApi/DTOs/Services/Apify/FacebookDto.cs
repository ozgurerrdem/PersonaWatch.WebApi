// PATH: WebApi/Services/Dtos/FacebookDto.cs
public class FacebookDto
{
    // Kaynak & kimlikler
    public string? FacebookUrl { get; set; }   // sayfa URL'si
    public string? PageId { get; set; }
    public string? PostId { get; set; }
    public string? PageName { get; set; }

    // Post URL'leri
    public string? Url { get; set; }          // gönderi URL'si
    public string? TopLevelUrl { get; set; }  // (bazı aktörlerde üst seviye link)
    public string? Link { get; set; }         // ekli dış bağlantı
    public string? Thumb { get; set; }        // thumbnail

    // Zaman
    public string? Time { get; set; }         // formatlı tarih/saat
    public long? Timestamp { get; set; }      // Unix (saniye)

    // İçerik
    public string? Text { get; set; }

    // Sayaçlar
    public int? Likes { get; set; }
    public int? Comments { get; set; }
    public int? Shares { get; set; }
}
