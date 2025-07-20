using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonaWatch.WebApi.Services.Interfaces;

[ApiController]
[Route("[controller]")]
public class ScanController : ControllerBase
{
    private readonly ScanService _scanService;

    public ScanController(ScanService scanService)
    {
        _scanService = scanService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Scan(string personName)
    {
        if (string.IsNullOrEmpty(personName))
            return BadRequest("Missing personName parameter");

        var newContents = await _scanService.ScanAsync(personName);

        return Ok(newContents);
    }

    [AllowAnonymous]
    [HttpGet("youtube")]
    public async Task<ActionResult<List<NewsContent>>> ScanYoutube([FromQuery] string personName)
    {
        if (string.IsNullOrWhiteSpace(personName))
            return BadRequest("personName parametresi zorunludur.");

        var ytScanner = _scanService
            .GetScanners()
            .FirstOrDefault(s => s.Source == "YouTubeApi");

        if (ytScanner is null)
            return StatusCode(500, "XScannerService bulunamadı");

        var results = await ytScanner.ScanAsync(personName);

        return Ok(results);
    }

    [AllowAnonymous]
    [HttpGet("filmot")]
    public async Task<ActionResult<List<NewsContent>>> FilmotTestScanner([FromQuery] string personName)
    {
        if (string.IsNullOrWhiteSpace(personName))
            return BadRequest("personName parametresi zorunludur.");

        var filmotScanner = _scanService
            .GetScanners()
            .FirstOrDefault(s => s.Source == "Filmot");

        if (filmotScanner is null)
            return StatusCode(500, "FilmotScannerService bulunamadı");

        var results = await filmotScanner.ScanAsync(personName);

        return Ok(results);
    }

}
