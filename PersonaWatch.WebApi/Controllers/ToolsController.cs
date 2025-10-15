using Microsoft.AspNetCore.Mvc;
using PersonaWatch.WebApi.Services.Interfaces;

namespace PersonaWatch.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToolsController : ControllerBase
{
    private readonly IConfiguration _cfg;
    private readonly IClipService _clipService;

    public ToolsController(IConfiguration cfg, IClipService clipService)
    {
        _cfg = cfg;
        _clipService = clipService;
    }

    [HttpGet("youtube/clip")]
    public async Task<IActionResult> Clip(
        [FromQuery] string videoId,
        [FromQuery] int start,
        [FromQuery] int end,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return BadRequest("videoId gereklidir.");
        if (start < 0 || end <= 0 || end <= start)
            return BadRequest("Geçersiz zaman aralığı.");

        var max = _cfg.GetValue<int?>("Tools:MaxClipSeconds") ?? 300;
        var duration = end - start;
        if (duration > max)
            return BadRequest($"Maksimum süre {max} sn.");

        return await _clipService.ClipAsync(Response, videoId, start, end, ct);
    }
}
