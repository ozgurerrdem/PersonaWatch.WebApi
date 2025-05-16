using PersonaWatch.WebApi.Entities;

namespace PersonaWatch.WebApi.Services.Interfaces
{
    public interface IScanner
    {
        Task<List<NewsContent>> ScanAsync(string personName);
    }
}
