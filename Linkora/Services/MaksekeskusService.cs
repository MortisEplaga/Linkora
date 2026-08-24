using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Linkora.Services
{
    public interface IMaksekeskusService
    {
        Task<(string TransactionId, string RedirectUrl)> CreateTransactionAsync(
            decimal price, string currency, string reference,
            string customerEmail, string customerIp, string locale,
            string returnUrl, string cancelUrl, string notificationUrl);
        bool VerifyMac(string json, string mac);
    }

    public class MaksekeskusService : IMaksekeskusService
    {
        private readonly string _shopId;
        private readonly string _secretKey;
        private readonly string _apiBase;
        private readonly HttpClient _http;

        public MaksekeskusService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _shopId = configuration["MakeCommerce:ShopId"]
                ?? throw new InvalidOperationException("MakeCommerce:ShopId is not configured");
            _secretKey = configuration["MakeCommerce:SecretKey"]
                ?? throw new InvalidOperationException("MakeCommerce:SecretKey is not configured");
            var testMode = configuration.GetValue<bool>("MakeCommerce:TestMode", true);
            _apiBase = testMode ? "https://api.test.maksekeskus.ee" : "https://api.maksekeskus.ee";
            _http = httpClientFactory.CreateClient();
        }
        public async Task<(string TransactionId, string RedirectUrl)> CreateTransactionAsync(
            decimal price, string currency, string reference,
            string customerEmail, string customerIp, string locale,
            string returnUrl, string cancelUrl, string notificationUrl)
        {
            var payload = new
            {
                transaction = new
                {
                    price = price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    currency,
                    reference
                },
                customer = new
                {
                    email = customerEmail,
                    ip = string.IsNullOrEmpty(customerIp) ? "127.0.0.1" : customerIp,
                    country = "lv",
                    locale
                },
                transaction_url = new
                {
                    return_url = new { url = returnUrl, method = "POST" },
                    cancel_url = new { url = cancelUrl, method = "POST" },
                    notification_url = new { url = notificationUrl, method = "POST" }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiBase}/v1/transactions");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var authBytes = Encoding.ASCII.GetBytes($"{_shopId}:{_secretKey}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"MakeCommerce error {(int)response.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var transactionId = root.GetProperty("id").GetString()!;

            string? redirectUrl = null;
            if (root.TryGetProperty("payment_methods", out var pm) && pm.TryGetProperty("other", out var other) && other.GetArrayLength() > 0) redirectUrl = other[0].GetProperty("url").GetString();

            if (redirectUrl == null) throw new InvalidOperationException("MakeCommerce response has no redirect URL");

            return (transactionId, redirectUrl);
        }
        public bool VerifyMac(string json, string mac) => string.Equals(Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(json + _secretKey))), mac, StringComparison.OrdinalIgnoreCase);
    }
}