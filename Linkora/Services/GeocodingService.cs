namespace Linkora.Services
{
    public interface IGeocodingService
    {
        Task<(decimal Lat, decimal Lng)?> GeocodeAsync(string address);
    }

    public class GoogleGeocodingService : IGeocodingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey;

        public GoogleGeocodingService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = configuration["GoogleMaps:ServerApiKey"]
                ?? throw new InvalidOperationException("GoogleMaps:ServerApiKey is not configured");
        }

        public async Task<(decimal Lat, decimal Lng)?> GeocodeAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            var http = _httpClientFactory.CreateClient();
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_apiKey}";

            try
            {
                var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("status").GetString() != "OK") return null;

                var results = root.GetProperty("results");
                if (results.GetArrayLength() == 0) return null;

                var location = results[0].GetProperty("geometry").GetProperty("location");
                return (location.GetProperty("lat").GetDecimal(), location.GetProperty("lng").GetDecimal());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
        }
    }
}