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
    public async Task<IActionResult> Scan(string searchKeyword)
    {
        if (string.IsNullOrEmpty(searchKeyword))
            return BadRequest("Missing searchKeyword parameter");

        var newContents = await _scanService.ScanAsync(searchKeyword);

        return Ok(newContents);
    }
}
