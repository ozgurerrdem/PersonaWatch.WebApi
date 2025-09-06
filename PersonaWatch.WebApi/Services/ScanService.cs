﻿using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PersonaWatch.WebApi.Data;
using PersonaWatch.WebApi.Services.Interfaces;

public class ScanService
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IScanner> _scanners;

    public ScanService(AppDbContext context, IEnumerable<IScanner> scanners)
    {
        _context = context;
        _scanners = scanners;
    }

    public async Task<ScannerResponse> ScanAsync(ScannerRequest request)
    {
        var allNewContents = new List<NewsContent>();
        var exceptions = new List<ScannerExceptions>();

        var existingHashes = new HashSet<string>(
            await _context.NewsContents
                .Where(n => n.RecordStatus == 'A')
                .Select(n => n.ContentHash)
                .ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var scanner in _scanners.Where(scanner => request.ScannerRunCriteria?.Contains(scanner.GetType().Name) == true))
        {
            try
            {
                var contents = await scanner.ScanAsync(request.SearchKeyword ?? string.Empty);

                foreach (var item in contents)
                {
                    if (!existingHashes.Contains(item.ContentHash))
                    {
                        existingHashes.Add(item.ContentHash);
                        allNewContents.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(new ScannerExceptions
                {
                    ScannerName = scanner.GetType().Name,
                    ErrorMessage = ex.Message
                });
            }
        }

        if (allNewContents.Any())
        {
            _context.NewsContents.AddRange(allNewContents);
            await _context.SaveChangesAsync();
        }

        return new ScannerResponse {
            NewContents = allNewContents.OrderByDescending(x => x.PublishDate).ToList(),
            Errors = exceptions.Any() ? exceptions : null
        };
    }

    public List<string> GetScanners()
    {
        var interfaceType = typeof(IScanner);
        return Assembly.GetExecutingAssembly()
                        .GetTypes()
                        .Where(t => interfaceType.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                        .Select(t => t.Name)
                        .ToList();
    }
}
