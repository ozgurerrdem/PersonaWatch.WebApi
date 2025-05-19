using Microsoft.EntityFrameworkCore;
using PersonaWatch.WebApi.Data;
using PersonaWatch.WebApi.Entities;
using PersonaWatch.WebApi.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

public class ScanService
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IScanner> _scanners;

    public ScanService(AppDbContext context, IEnumerable<IScanner> scanners)
    {
        _context = context;
        _scanners = scanners;
    }

    public async Task<List<NewsContent>> ScanAsync(string personName)
    {
        var allNewContents = new List<NewsContent>();

        // Var olan ContentHash'leri HashSet ile çek (çok hızlı arar)
        var existingHashes = new HashSet<string>(
            await _context.NewsContents
                .Where(n => n.RecordStatus == 'A')
                .Select(n => n.ContentHash)
                .ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var scanner in _scanners)
        {
            var contents = await scanner.ScanAsync(personName);

            foreach (var item in contents)
            {
                if (!existingHashes.Contains(item.ContentHash))
                {
                    existingHashes.Add(item.ContentHash);
                    allNewContents.Add(item);
                }
            }
        }

        if (allNewContents.Any())
        {
            _context.NewsContents.AddRange(allNewContents);
            await _context.SaveChangesAsync();
        }

        return allNewContents.OrderByDescending(x => x.PublishDate).ToList();
    }
}
