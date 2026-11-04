namespace Linkora.Models
{
    public enum ShipmentRequestStatus
    {
        Requested,
        Accepted,
        Paid,
        Declined,
        Cancelled
    }

    public enum ShipmentStatus
    {
        Pending,
        Registered,
        InTransit,
        ArrivedAtLocker,
        Delivered,
        Returned,
        Failed
    }

    /// <summary>Destination countries supported by the parcel-machine delivery flow.</summary>
    public static class ShippingCountries
    {
        public static readonly string[] All = ["LV", "LT", "EE"];
        public static bool IsSupported(string? country) =>
            country != null && All.Contains(country.Trim().ToUpperInvariant());
    }

    public static class ParcelSizes
    {
        /// <summary>
        /// XL is intentionally absent: Omniva does not deliver XL parcels through parcel machines
        /// (post office / courier only), and this integration uses deliveryChannel = PARCEL_MACHINE.
        /// </summary>
        public static readonly string[] All = ["XS", "S", "M", "L"];
        public static bool IsValid(string? size) => size != null && All.Contains(size);
    }

    public static class ParcelRules
    {
        public const decimal MaxWeightKg = 100m;

        /// <summary>
        /// Returns an error code, or null when the delivery settings are acceptable.
        /// Size and weight are only checked when delivery is enabled.
        /// </summary>
        public static string? Validate(bool enabled, string? size, decimal? weightKg)
        {
            if (!enabled) return null;
            if (!ParcelSizes.IsValid(size)) return "size_invalid";
            if (!weightKg.HasValue || weightKg.Value <= 0 || weightKg.Value > MaxWeightKg) return "weight_invalid";
            return null;
        }
    }

    public class ParcelLocker : Base
    {
        public string Provider { get; set; } = "Omniva";
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Country { get; set; } = "LV";
        public string? City { get; set; }
        public string? Address { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
    }

    public class ShippingTariff : Base
    {
        public string Provider { get; set; } = "Omniva";
        public string DestinationCountry { get; set; } = "LV";
        public string Size { get; set; } = "";
        public decimal OmnivaPrice { get; set; }
        public decimal Markup { get; set; }
        public decimal Price { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
    }

    public class ShipmentRequest : Base
    {
        public int ProductId { get; set; }
        public int BuyerId { get; set; }
        public int SellerId { get; set; }
        public int LockerId { get; set; }
        public ShipmentRequestStatus Status { get; set; } = ShipmentRequestStatus.Requested;
        public string ReceiverName { get; set; } = "";
        public string? ReceiverPhone { get; set; }
        public string? ReceiverEmail { get; set; }
        public string? SenderName { get; set; }
        public string? SenderPhone { get; set; }
        public string? SenderEmail { get; set; }
        public string? SenderStreet { get; set; }
        public string? SenderCity { get; set; }
        public string? SenderPostcode { get; set; }
        public string? SenderCountry { get; set; }
        public string? Size { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? Price { get; set; }
        public int? PaymentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Shipment : Base
    {
        public int RequestId { get; set; }
        public string Provider { get; set; } = "Omniva";
        public string PartnerShipmentId { get; set; } = "";
        public string? Barcode { get; set; }
        public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;
        public string? LastEventCode { get; set; }
        public DateTime? LastEventAt { get; set; }
        public int Attempts { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? HandoverDeadline { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ShipmentEvent
    {
        public long Id { get; set; }
        public int ShipmentId { get; set; }
        public long ExternalEventId { get; set; }
        public string EventCode { get; set; } = "";
        public DateTime EventAt { get; set; }
        public string? Location { get; set; }
        public string? RawJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Delivery settings of a listing, as needed by the shipping flow.</summary>
    public sealed record ProductShippingInfo(int ProductId, int? SellerId, string Status, bool ShippingEnabled, string? ParcelSize, decimal? WeightKg);

    /// <summary>Shipment request joined with listing, parties and shipment data for the seller and buyer screens.</summary>
    public class ShipmentRequestView
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string? ProductSize { get; set; }
        public decimal? ProductWeightKg { get; set; }
        public int BuyerId { get; set; }
        public string BuyerName { get; set; } = "";
        public int SellerId { get; set; }
        public ShipmentRequestStatus Status { get; set; }
        public string LockerName { get; set; } = "";
        public string? LockerCity { get; set; }
        public string ReceiverName { get; set; } = "";
        public string? ReceiverPhone { get; set; }
        public string? ReceiverEmail { get; set; }
        public string? Size { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? Price { get; set; }
        public int? PaymentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? ShipmentId { get; set; }
        public ShipmentStatus? ShipmentState { get; set; }
        public string? Barcode { get; set; }
        public DateTime? HandoverDeadline { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class ProductShippingSettingsDto
    {
        public bool Enabled { get; set; }
        public string? Size { get; set; }
        public decimal? WeightKg { get; set; }
    }

    public class CreateShipmentRequestDto
    {
        public int ProductId { get; set; }
        public int LockerId { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public string? ReceiverEmail { get; set; }
    }

    public class AcceptShipmentRequestDto
    {
        public string? Size { get; set; }
        public decimal? WeightKg { get; set; }
        public string? SenderName { get; set; }
        public string? SenderPhone { get; set; }
        public string? SenderEmail { get; set; }
        public string? SenderStreet { get; set; }
        public string? SenderCity { get; set; }
        public string? SenderPostcode { get; set; }
        public string? SenderCountry { get; set; }
    }

    /// <summary>Delivery block of the listing form.</summary>
    public sealed record ProductDeliveryFormModel(bool Enabled, string? ParcelSize, decimal? WeightKg);

    /// <summary>One row of the admin tariff editor (country + size identify the row).</summary>
    public class ShippingTariffEditDto
    {
        public string? DestinationCountry { get; set; }
        public string? Size { get; set; }
        public decimal OmnivaPrice { get; set; }
        public decimal Markup { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public bool IsActive { get; set; }
    }
}
