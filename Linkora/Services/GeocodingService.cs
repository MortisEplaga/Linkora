using Microsoft.Extensions.Caching.Memory;

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
        private readonly IMemoryCache _cache;
        private readonly ILogger<GoogleGeocodingService> _logger;

        public GoogleGeocodingService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache, ILogger<GoogleGeocodingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = configuration["GoogleMaps:ServerApiKey"] ?? throw new InvalidOperationException("GoogleMaps:ServerApiKey is not configured");
            _cache = cache;
            _logger = logger;
        }

        public async Task<(decimal Lat, decimal Lng)?> GeocodeAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                _logger.LogInformation("Geocode skipped: address is empty");
                return null;
            }

            var key = address.Trim().ToLowerInvariant();
            if (_cache.TryGetValue(key, out (decimal, decimal) cached))
            {
                _logger.LogInformation("Geocode cache hit for '{Address}': Lat={Lat}, Lng={Lng}", address, cached.Item1, cached.Item2);
                return cached;
            }

            var http = _httpClientFactory.CreateClient();
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_apiKey}";

            _logger.LogInformation("Geocode request for address '{Address}'", address);

            try
            {
                var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Geocode HTTP error for '{Address}': {StatusCode}", address, response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.GetProperty("status").GetString();
                if (status != "OK")
                {
                    _logger.LogWarning("Geocode API status '{Status}' for address '{Address}'. Raw response: {Json}", status, address, json);
                    return null;
                }

                var results = root.GetProperty("results");
                if (results.GetArrayLength() == 0)
                {
                    _logger.LogWarning("Geocode returned OK but zero results for address '{Address}'", address);
                    return null;
                }

                var location = results[0].GetProperty("geometry").GetProperty("location");
                var lat = location.GetProperty("lat").GetDecimal();
                var lng = location.GetProperty("lng").GetDecimal();
                var result = (lat, lng);

                _cache.Set(key, result, TimeSpan.FromDays(30));

                _logger.LogInformation("Geocode success for '{Address}': Lat={Lat}, Lng={Lng}", address, lat, lng);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Geocode exception for address '{Address}'", address);
                return null;
            }
        }
    }
}