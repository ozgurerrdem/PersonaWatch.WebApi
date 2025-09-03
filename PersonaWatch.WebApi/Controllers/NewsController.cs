using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonaWatch.WebApi.Data;
using System.Threading.Tasks;

namespace PersonaWatch.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NewsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetNews(
                                                [FromQuery] string? search,
                                                [FromQuery] DateTime? dateFrom,
                                                [FromQuery] DateTime? dateTo)
        {
            var query = _context.NewsContents
                .Where(n => n.RecordStatus == 'A');

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(n => n.SearchKeyword == search);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(n => n.PublishDate.Date >= dateFrom.Value.Date);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(n => n.PublishDate.Date <= dateTo.Value.Date);
            }

            var newsList = await query
                .OrderByDescending(n => n.PublishDate)
                .Select(n => new
                {
                    title = n.Title,
                    content = n.Summary,
                    link = n.Url,
                    platform = n.Platform,
                    publishDate = n.PublishDate,
                    Source = n.Source ?? string.Empty
                })
                .ToListAsync();

            return Ok(newsList);
        }
    }
}
