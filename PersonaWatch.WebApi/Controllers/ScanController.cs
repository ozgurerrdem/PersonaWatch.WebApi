using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class ScanController : ControllerBase
{
    private readonly ScanService _scanService;

    public ScanController(ScanService scanService)
    {
        _scanService = scanService;
    }

    [HttpGet]
    public async Task<IActionResult> Scan(string personName)
    {
        if (string.IsNullOrEmpty(personName))
            return BadRequest("Missing personName parameter");

        var newContents = await _scanService.ScanAsync(personName);

        return Ok(newContents);
    }
}
