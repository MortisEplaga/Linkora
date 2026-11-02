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
    /// <param name="ParcelSize">Platform parcel size (XS..L), mapped to OMX measurement.length/width/height.</param>
    public record OmnivaShipmentRequest(string PartnerShipmentId, OmnivaParty Sender, OmnivaParty Receiver, string LockerCode,
                                        decimal? WeightKg = null, string? Comment = null, string? ParcelSize = null);
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
        /// <summary>Public Omniva location feed. Overridable with Omniva:LocationsUrl.</summary>
        public const string DefaultLocationsUrl = "https://www.omniva.ee/locations.json";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private readonly HttpClient _http;
        // The public location feed must never receive the OMX Basic credentials.
        private readonly HttpClient _locationsHttp;
        // null until the OMX credentials are configured: the public locker feed still works without them.
        private readonly string? _baseUrl;
        private readonly string _customerCode;
        private readonly string _locationsUrl;

        public OmnivaService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            var section = configuration.GetSection("Omniva");
            var baseUrl = section["BaseUrl"];
            _customerCode = section["CustomerCode"] ?? "";
            var user = section["Username"];
            var password = section["Password"];
            var locationUrl = section["LocationsUrl"];
            _locationsUrl = string.IsNullOrWhiteSpace(locationUrl) ? DefaultLocationsUrl : locationUrl!.Trim();

            _http = httpClientFactory.CreateClient();
            _http.Timeout = TimeSpan.FromSeconds(30);

            if (!string.IsNullOrWhiteSpace(baseUrl) && _customerCode.Length > 0 &&
                !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            {
                _baseUrl = baseUrl!.TrimEnd('/');
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{password}")));
                var agentId = section["IntegrationAgentId"];
                if (!string.IsNullOrWhiteSpace(agentId)) _http.DefaultRequestHeaders.Add("X-Integration-Agent-Id", agentId);
            }

            _locationsHttp = httpClientFactory.CreateClient();
            _locationsHttp.Timeout = TimeSpan.FromSeconds(30);
        }
        public async Task<OmnivaRegisterResult> RegisterShipmentAsync(OmnivaShipmentRequest request, CancellationToken ct = default)
        {
            if (_baseUrl == null) return new(false, null, "CONFIG_MISSING", "Omniva OMX credentials are not configured");

            // The calling code of a short local number depends on the party's country (LV/ LT/ EE), not on a global default.
            if (!OmnivaPhone.TryNormalize(request.Sender.Phone, request.Sender.Country, out var senderPhone) ||
                !OmnivaPhone.TryNormalize(request.Receiver.Phone, request.Receiver.Country, out var receiverPhone))
                return new(false, null, "INVALID_PHONE", "Phone must include the country code and contain 9 to 11 digits.");

            if (string.IsNullOrWhiteSpace(request.Sender.Name)) return new(false, null, "SENDER_NAME_REQUIRED", "The sender name is required.");
            if (string.IsNullOrWhiteSpace(request.Receiver.Name)) return new(false, null, "RECEIVER_NAME_REQUIRED", "The receiver name is required.");
            if (senderPhone == null && string.IsNullOrWhiteSpace(request.Sender.Email))
                return new(false, null, "SENDER_CONTACT_REQUIRED", "The sender needs a mobile phone or an email.");
            if (receiverPhone == null && string.IsNullOrWhiteSpace(request.Receiver.Email))
                return new(false, null, "RECEIVER_CONTACT_REQUIRED", "The receiver needs a mobile phone or an email.");
            // XL and any other unknown size cannot be registered for a parcel machine.
            if (!string.IsNullOrWhiteSpace(request.ParcelSize) &&
                !OmnivaParcelDimensions.TryGet(request.ParcelSize, out _, out _, out _))
                return new(false, null, "PARCEL_SIZE_INVALID", "Only XS, S, M and L can be delivered to a parcel machine.");

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
                        shipmentComment = request.Comment,
                        // OMX derives the locker compartment from measurement.length/width/height (metres), not from a "size" field.
                        measurement = BuildMeasurement(request.WeightKg, request.ParcelSize),
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
            catch (Exception ex) when (ex is JsonException or InvalidOperationException) { }

            return new(false, null, $"HTTP_{status}", Truncate(body, 500));
        }
        public async Task<OmnivaLabelResult> GetLabelAsync(string barcode, CancellationToken ct = default)
        {
            if (_baseUrl == null) return new(false, null, "CONFIG_MISSING");

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

                if (root.TryGetProperty("successAddressCards", out var ok))
                {
                    // The manual calls the field "filedata", real responses use "fileData"; accept both, array or single object.
                    var card = ok.ValueKind == JsonValueKind.Array && ok.GetArrayLength() > 0 ? ok[0] : ok;
                    var file = Str(card, "fileData") ?? Str(card, "filedata");
                    if (!string.IsNullOrEmpty(file)) return new(true, Convert.FromBase64String(file), null);
                }

                if (root.TryGetProperty("failedAddressCards", out var failed) && failed.ValueKind == JsonValueKind.Array && failed.GetArrayLength() > 0)
                    return new(false, null, failed[0].ValueKind == JsonValueKind.Object ? Str(failed[0], "messageCode") : failed[0].ToString());
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException) { }

            return new(false, null, $"HTTP_{status}");
        }
        public async Task<OmnivaEventsPage> GetEventsAsync(long fromEventId, int size = 100, CancellationToken ct = default)
        {
            if (_baseUrl == null) throw new OmnivaApiException(0, "Omniva OMX credentials are not configured");

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
                if (!TryGetInt64(item, "eventId", out var eventId)) continue;
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
            if (_baseUrl == null) return false;

            try
            {
                var (status, body) = await PostAsync($"{ShipmentsPath}/cancel", new { customerCode = _customerCode, barcode }, ct);
                if (status != 200) return false;
                if (string.IsNullOrWhiteSpace(body)) return true;

                using var doc = JsonDocument.Parse(body);
                var resultCode = Str(doc.RootElement, "resultCode");
                if (resultCode != null)
                    return string.Equals(resultCode, "OK", StringComparison.OrdinalIgnoreCase);
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException) { return false; }
        }
        /// <summary>
        /// Reads the public Omniva locations feed (TYPE 0 = parcel machine, TYPE 1 = post office).
        /// ZIP is the offloadPostcode, X_COORDINATE is longitude and Y_COORDINATE is latitude.
        /// </summary>
        public async Task<IReadOnlyList<OmnivaLocker>> GetLockersAsync(CancellationToken ct = default)
        {
            using var response = await _locationsHttp.GetAsync(_locationsUrl, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var array = root;
            if (array.ValueKind != JsonValueKind.Array && root.ValueKind == JsonValueKind.Object)
            {
                array = default;
                foreach (var property in root.EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.Array) { array = property.Value; break; }
            }

            var result = new List<OmnivaLocker>();
            if (array.ValueKind != JsonValueKind.Array) return result;

            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (Str(item, "TYPE") is not "0") continue; // 1 = post office, not a parcel machine

                var code = Str(item, "ZIP")?.Trim();
                var name = Str(item, "NAME")?.Trim();
                var country = Str(item, "A0_NAME")?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;
                if (country is not ("LV" or "LT" or "EE")) continue;
                // "1. eelistus / Picapac" is a delivery preference, not a physical locker with an address.
                if (IsPreferencePoint(name!)) continue;
                if (!seenCodes.Add(code!)) continue; // offloadPostcode must be unique for (Provider, Code)

                result.Add(new OmnivaLocker(
                    Truncate(code!, 50),
                    Truncate(name!, 200),
                    country!,
                    FirstNonEmpty(item, "A3_NAME", "A2_NAME", "A1_NAME"),
                    JoinAddress(item),
                    ParseCoordinate(Str(item, "Y_COORDINATE")),
                    ParseCoordinate(Str(item, "X_COORDINATE"))));
            }
            return result;
        }
        private static bool IsPreferencePoint(string name) =>
            name.Contains("eelistus", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Picapac", StringComparison.OrdinalIgnoreCase);
        private static string? FirstNonEmpty(JsonElement item, params string[] names)
        {
            foreach (var name in names)
            {
                var value = Str(item, name)?.Trim();
                if (!string.IsNullOrEmpty(value)) return Truncate(value, 120);
            }
            return null;
        }
        private static string? JoinAddress(JsonElement item)
        {
            var parts = new List<string>();
            foreach (var name in new[] { "A5_NAME", "A7_NAME", "A8_NAME", "A4_NAME", "A6_NAME" })
            {
                var value = Str(item, name)?.Trim();
                if (!string.IsNullOrEmpty(value) && !parts.Any(p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase)))
                    parts.Add(value);
            }
            return parts.Count == 0 ? null : Truncate(string.Join(", ", parts), 300);
        }
        private static decimal? ParseCoordinate(string? value) =>
            decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed is >= -180m and <= 180m
                ? parsed
                : null;
        /// <summary>OMX has no "size" attribute: the compartment is derived from the parcel dimensions in metres.</summary>
        private static object? BuildMeasurement(decimal? weightKg, string? parcelSize)
        {
            OmnivaParcelDimensions.TryGet(parcelSize, out var length, out var width, out var height);
            if (weightKg == null && length == null) return null;
            return new { weight = weightKg, length, width, height };
        }
        private async Task<(int Status, string Body)> GetAsync(string pathAndQuery, CancellationToken ct)
        {
            using var response = await _http.GetAsync(_baseUrl! + pathAndQuery, ct);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
        }
        private async Task<(int Status, string Body)> PostAsync(string path, object payload, CancellationToken ct)
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(_baseUrl! + path, content, ct);
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
        /// <summary>
        /// Reads a scalar field without assuming its JSON type. JsonElement.GetString() throws on numbers and booleans,
        /// which used to abort the whole response parse; the carrier may return e.g. a numeric barcode or message code.
        /// </summary>
        private static string? Str(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return null;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
                _ => null
            };
        }
        /// <summary>Event ids may arrive as JSON numbers or as strings.</summary>
        private static bool TryGetInt64(JsonElement element, string name, out long result)
        {
            result = 0;
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return false;
            if (value.ValueKind == JsonValueKind.Number) return value.TryGetInt64(out result);
            if (value.ValueKind == JsonValueKind.String)
                return long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
            return false;
        }
        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
        private static DateTime? ParseUtc(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : null;
    }
    /// <summary>
    /// Maximum dimensions of Omniva parcel-machine lockers, in metres (OMX measurement.length/width/height).
    /// XL is not listed: Omniva delivers XL only through a post office or a courier, never a parcel machine.
    /// </summary>
    public static class OmnivaParcelDimensions
    {
        public static bool TryGet(string? size, out decimal? length, out decimal? width, out decimal? height)
        {
            length = width = height = null;
            switch (size?.Trim().ToUpperInvariant())
            {
                case "XS": length = 0.64m; width = 0.38m; height = 0.05m; break;
                case "S": length = 0.64m; width = 0.38m; height = 0.09m; break;
                case "M": length = 0.64m; width = 0.38m; height = 0.19m; break;
                case "L": length = 0.64m; width = 0.38m; height = 0.39m; break;
                default: return false;
            }
            return true;
        }
    }
    public static class OmnivaPhone
    {
        public const string DefaultCountryCode = "371";
        private static readonly string[] KnownCallingCodes = ["371", "370", "372"];

        /// <summary>Calling code used for a short local number of the given Baltic country.</summary>
        public static string CallingCodeFor(string? country) => country?.Trim().ToUpperInvariant() switch
        {
            "LT" => "370",
            "EE" => "372",
            "LV" => "371",
            _ => DefaultCountryCode
        };
        public static bool TryNormalize(string? raw, string? country, out string? normalized) =>
            TryNormalize(raw, out normalized, CallingCodeFor(country));

        public static bool TryNormalize(string? raw, out string? normalized, string defaultCountryCode = DefaultCountryCode)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(raw)) return true;

            var trimmed = raw.Trim();
            var digits = new string(trimmed.Where(c => c is >= '0' and <= '9').ToArray());
            var hasInternationalPrefix = trimmed.StartsWith('+') || digits.StartsWith("00", StringComparison.Ordinal);
            if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];

            // A short local number is completed with the calling code of the party's own country.
            if (!hasInternationalPrefix && (digits.Length == 7 || digits.Length == 8))
                digits = defaultCountryCode + digits;

            if (digits.Length < 9 || digits.Length > 11) return false;
            // A long number without "+" / "00" is only accepted when it already carries a known Baltic calling code.
            if (!hasInternationalPrefix && !KnownCallingCodes.Any(code => digits.StartsWith(code, StringComparison.Ordinal))) return false;

            normalized = "+" + digits;
            return true;
        }
    }
}
