using PersonaWatch.WebApi.Entities.Services.Apify;

namespace PersonaWatch.WebApi.Services
{
    public class ApifyService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiToken;

        public ApifyService(IConfiguration config, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _apiToken = config["Apify:ApiKey"] ?? throw new Exception("Apify token not configured.");
        }

        public async Task<string> StartActorAsync(string actorId, object input)
        {
            var uri = $"https://api.apify.com/v2/acts/{actorId}/runs?token={_apiToken}";
            var response = await _httpClient.PostAsJsonAsync(uri, new { input });
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<ApifyRunResponse>();
            return json?.Data?.Id ?? throw new Exception("Run ID boş.");
        }

        public async Task<string> StartActorRawAsync(string actorId, object rawInput)
        {
            var uri = $"https://api.apify.com/v2/acts/{actorId}/runs?token={_apiToken}";
            var response = await _httpClient.PostAsJsonAsync(uri, rawInput);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<ApifyRunResponse>();
            return json?.Data?.Id ?? throw new Exception("Run ID boş.");
        }

        public async Task<string?> GetRunStatusAsync(string runId)
        {
            var uri = $"https://api.apify.com/v2/actor-runs/{runId}?token={_apiToken}";
            var res = await _httpClient.GetFromJsonAsync<ApifyRunResponse>(uri);
            return res?.Data?.Status;
        }

        public async Task<string?> GetDatasetIdAsync(string runId)
        {
            var uri = $"https://api.apify.com/v2/actor-runs/{runId}?token={_apiToken}";
            var res = await _httpClient.GetFromJsonAsync<ApifyRunResponse>(uri);
            return res?.Data?.DefaultDatasetId;
        }

        public async Task<List<T>> GetDatasetItemsAsync<T>(string datasetId)
        {
            var uri = $"https://api.apify.com/v2/datasets/{datasetId}/items?token={_apiToken}&clean=true";
            var jsonString = await _httpClient.GetStringAsync(uri);
            Console.WriteLine(jsonString);
            return await _httpClient.GetFromJsonAsync<List<T>>(uri) ?? new List<T>();
        }
    }
}
