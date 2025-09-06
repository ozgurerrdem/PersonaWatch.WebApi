﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ScanController : ControllerBase
{
    private readonly ScanService _scanService;

    public ScanController(ScanService scanService)
    {
        _scanService = scanService;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Scan(ScannerRequest request)
    {
        if (string.IsNullOrEmpty(request.SearchKeyword))
            return BadRequest("Missing searchKeyword parameter");
        if (request.ScannerRunCriteria == null || !request.ScannerRunCriteria.Any())
            return BadRequest("Missing scanners parameter");

        var response = await _scanService.ScanAsync(request);
        
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("scanners")]
    public IActionResult GetScanners()
    {
        try
        {
            var scanners = _scanService.GetScanners();
            if (scanners == null || !scanners.Any())
                return NotFound("No scanners available.");

            return Ok(scanners);
        }
        catch (System.Exception)
        {
            return StatusCode(500, "An error occurred while retrieving the scanners.");
        }
    }
}
