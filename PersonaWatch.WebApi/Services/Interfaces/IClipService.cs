using Microsoft.AspNetCore.Mvc;

namespace PersonaWatch.WebApi.Services.Interfaces;

public interface IClipService
{
    /// <summary>
    /// YouTube videoId + start/end (saniye) alır, MP4 klibi HTTP response’a yazar.
    /// </summary>
    Task<IActionResult> ClipAsync(
        HttpResponse response,
        string videoId,
        int start,
        int end,
        CancellationToken ct = default);
}
