using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Linkora.Services
{
    public record OmnivaParty(string Name, string? Phone, string? Email, string? Street, string? City, string? Postcode, string Country);
    /// <param name="PartnerShipmentId">Platform identifier of the shipment, up to 30 characters.</param>
    /// <param name="LockerCode">Locker identifier sent as offloadPostcode.</param>
    public record OmnivaShipmentRequest(string PartnerShipmentId, OmnivaParty Sender, OmnivaParty Receiver, string LockerCode, decimal? WeightKg = null, string? Comment = null);
    public record OmnivaRegisterResult(bool Success, string? Barcode, string? ErrorCode, string? ErrorMessage);
    public record OmnivaLabelResult(bool Success, byte[]? Pdf, string? ErrorCode);
    public record OmnivaEvent(long EventId, string Barcode, string Code, DateTime OccurredAtUtc, string? Location, string RawJson);
    public record OmnivaEventsPage(IReadOnlyList<OmnivaEvent> Events, long LastEventId, bool HasMore);
    public record OmnivaLocker(string Code, string Name, string Country, string? City, string? Address, decimal? Lat, decimal? Lng);
    public interface IOmnivaService
    {
        Task<OmnivaRegisterResult> RegisterShipmentAsync(OmnivaShipmentRequest request, CancellationToken ct = default);
        Task<OmnivaLabelResult> GetLabelAsync(string barcode, CancellationToken ct = default);
        Task<OmnivaEventsPage> GetEventsAsync(long fromEventId, int size = 100, CancellationToken ct = default);
        Task<IReadOnlyList<OmnivaLocker>> GetLockersAsync(CancellationToken ct = default);
        /// <summary>Best-effort cancellation of a registered parcel. Returns false when Omniva did not confirm it.</summary>
        Task<bool> CancelShipmentAsync(string barcode, CancellationToken ct = default);
    }
    /// <summary>Non-success HTTP response from the OMX API.</summary>
    public sealed class OmnivaApiException(int statusCode, string message) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
        public bool IsRateLimited => StatusCode == 429;
    }
    public class OmnivaService : IOmnivaService
    {
        private const string ShipmentsPath = "/api/v01/omx/shipments";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _customerCode;

        public OmnivaService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            var section = configuration.GetSection("Omniva");
            _baseUrl = (section["BaseUrl"] ?? throw new InvalidOperationException("Omniva:BaseUrl is not configured")).TrimEnd('/');
            _customerCode = section["CustomerCode"] ?? throw new InvalidOperationException("Omniva:CustomerCode is not configured");
            var user = section["Username"] ?? throw new InvalidOperationException("Omniva:Username is not configured");
            var password = section["Password"] ?? throw new InvalidOperationException("Omniva:Password is not configured");

            _http = httpClientFactory.CreateClient();
            _http.Timeout = TimeSpan.FromSeconds(30);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{password}")));

            var agentId = section["IntegrationAgentId"];
            if (!string.IsNullOrWhiteSpace(agentId)) _http.DefaultRequestHeaders.Add("X-Integration-Agent-Id", agentId);
        }
        public async Task<OmnivaRegisterResult> RegisterShipmentAsync(OmnivaShipmentRequest request, CancellationToken ct = default)
        {
            if (!OmnivaPhone.TryNormalize(request.Sender.Phone, out var senderPhone) || !OmnivaPhone.TryNormalize(request.Receiver.Phone, out var receiverPhone))
                return new(false, null, "INVALID_PHONE", "Phone must include the country code and contain 9 to 11 digits.");

            if (receiverPhone == null && string.IsNullOrWhiteSpace(request.Receiver.Email)) return new(false, null, "RECEIVER_CONTACT_REQUIRED", "The receiver needs a mobile phone or an email.");

            var sender = request.Sender with { Phone = senderPhone };
            var receiver = request.Receiver with { Phone = receiverPhone };

            var payload = new
            {
                customerCode = _customerCode,
                shipments = new[]
                {
                    new
                    {
                        partnerShipmentId = request.PartnerShipmentId,
                        mainService = "PARCEL",
                        deliveryChannel = "PARCEL_MACHINE",
                        paidByReceiver = false,
                        shipmentComment = request.Comment,
                        measurement = request.WeightKg.HasValue ? new { weight = request.WeightKg.Value } : null,
                        senderAddressee = ToParty(sender),
                        receiverAddressee = ToParty(receiver, request.LockerCode)
                    }
                }
            };

            int status;
            string body;
            try { (status, body) = await PostAsync($"{ShipmentsPath}/business-to-client", payload, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return new(false, null, "NETWORK", ex.Message); }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("savedShipments", out var saved) && saved.ValueKind == JsonValueKind.Array && saved.GetArrayLength() > 0)
                {
                    var barcode = Str(saved[0], "barcode");
                    if (!string.IsNullOrEmpty(barcode)) return new(true, barcode, null, null);
                }

                if (root.TryGetProperty("failedShipments", out var failed) && failed.ValueKind == JsonValueKind.Array && failed.GetArrayLength() > 0)
                    return new(false, null, Str(failed[0], "messageCode"), Str(failed[0], "message"));
            }
            catch (JsonException) { }

            return new(false, null, $"HTTP_{status}", Truncate(body, 500));
        }
        public async Task<OmnivaLabelResult> GetLabelAsync(string barcode, CancellationToken ct = default)
        {
            var payload = new
            {
                customerCode = _customerCode,
                barcodes = new[] { barcode },
                sendAddressCardTo = "RESPONSE"
            };

            int status;
            string body;
            try { (status, body) = await PostAsync($"{ShipmentsPath}/package-labels", payload, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return new(false, null, "NETWORK"); }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("successAddressCards", out var ok) && ok.ValueKind == JsonValueKind.Array && ok.GetArrayLength() > 0)
                {
                    var file = Str(ok[0], "filedata");
                    if (!string.IsNullOrEmpty(file)) return new(true, Convert.FromBase64String(file), null);
                }

                if (root.TryGetProperty("failedAddressCards", out var failed) && failed.ValueKind == JsonValueKind.Array && failed.GetArrayLength() > 0)
                    return new(false, null, failed[0].ValueKind == JsonValueKind.Object ? Str(failed[0], "messageCode") : failed[0].ToString());
            }
            catch (Exception ex) when (ex is JsonException or FormatException) { }

            return new(false, null, $"HTTP_{status}");
        }
        public async Task<OmnivaEventsPage> GetEventsAsync(long fromEventId, int size = 100, CancellationToken ct = default)
        {
            size = Math.Clamp(size, 1, 100);
            var (status, body) = await GetAsync($"{ShipmentsPath}?size={size}&fromTrackEventId={fromEventId}", ct);
            if (status != 200) throw new OmnivaApiException(status, $"Omniva events request failed with HTTP {status}: {Truncate(body, 300)}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                throw new OmnivaApiException(status, "Omniva events response has no content array");

            var events = new List<OmnivaEvent>();
            long lastId = fromEventId;
            int count = 0;
            foreach (var item in content.EnumerateArray())
            {
                count++;
                // The cursor advances over every event that has an id, even when the event itself is skipped below.
                if (!item.TryGetProperty("eventId", out var idEl) || !idEl.TryGetInt64(out var eventId)) continue;
                if (eventId > lastId) lastId = eventId;

                var barcode = Str(item, "shipmentBarcode");
                var code = Str(item, "eventCode");
                var occurredAt = ParseUtc(Str(item, "eventDate"));
                if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(code) || occurredAt == null) continue;

                string? location = null;
                if (item.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object)
                    location = Str(loc, "locationName") ?? Str(loc, "zip");

                events.Add(new OmnivaEvent(eventId, barcode, code, occurredAt.Value, location, item.GetRawText()));
            }

            bool hasMore;
            if (root.TryGetProperty("last", out var lastEl) && lastEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
                hasMore = lastEl.ValueKind == JsonValueKind.False;
            else
                hasMore = count >= size;

            return new OmnivaEventsPage(events, lastId, hasMore);
        }
        public async Task<bool> CancelShipmentAsync(string barcode, CancellationToken ct = default)
        {
            try
            {
                var (status, body) = await PostAsync($"{ShipmentsPath}/cancel", new { customerCode = _customerCode, barcode }, ct);
                if (status != 200) return false;
                if (string.IsNullOrWhiteSpace(body)) return true;

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("resultCode", out var rc) && rc.ValueKind == JsonValueKind.String)
                    return string.Equals(rc.GetString(), "OK", StringComparison.OrdinalIgnoreCase);
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return false; }
        }
        public Task<IReadOnlyList<OmnivaLocker>> GetLockersAsync(CancellationToken ct = default) => throw new NotSupportedException("Locker list loading is not implemented: the source and format must be confirmed with Omniva.");
        private async Task<(int Status, string Body)> GetAsync(string pathAndQuery, CancellationToken ct)
        {
            using var response = await _http.GetAsync(_baseUrl + pathAndQuery, ct);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
        }
        private async Task<(int Status, string Body)> PostAsync(string path, object payload, CancellationToken ct)
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(_baseUrl + path, content, ct);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
        }
        private static object ToParty(OmnivaParty p, string? offloadPostcode = null) => new
        {
            personName = p.Name,
            contactMobile = p.Phone,
            contactEmail = p.Email,
            address = new
            {
                street = offloadPostcode == null ? p.Street : null,
                deliverypoint = offloadPostcode == null ? p.City : null,
                postcode = offloadPostcode == null ? p.Postcode : null,
                offloadPostcode,
                country = p.Country
            }
        };
        private static string? Str(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value.GetString() : null;
        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
        private static DateTime? ParseUtc(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : null;
    }
    public static class OmnivaPhone
    {
        public const string DefaultCountryCode = "371";

        public static bool TryNormalize(string? raw, out string? normalized, string defaultCountryCode = DefaultCountryCode)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(raw)) return true;

            var digits = new string(raw.Where(c => c is >= '0' and <= '9').ToArray());
            if (digits.StartsWith("00")) digits = digits[2..];
            if (digits.Length == 8) digits = defaultCountryCode + digits;
            if (digits.Length < 9 || digits.Length > 11) return false;

            normalized = "+" + digits;
            return true;
        }
    }
}