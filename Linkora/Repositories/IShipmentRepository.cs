using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IShipmentRepository
    {
        Task<int> CreateRequestAsync(ShipmentRequest request);
        Task<ShipmentRequest?> GetRequestAsync(int id);
        Task<ShipmentRequest?> GetRequestByPaymentAsync(int paymentId);
        Task<List<ShipmentRequestView>> GetSellerRequestsAsync(int sellerId);
        Task<List<ShipmentRequestView>> GetBuyerRequestsAsync(int buyerId);
        Task<bool> AcceptRequestAsync(ShipmentRequest request);
        Task<bool> HasReservedRequestAsync(int productId, int exceptRequestId);
        Task<bool> AttachPaymentAsync(int requestId, int paymentId);
        /// <summary>Returns accepted-but-unpaid requests older than the cutoff to Requested and returns them.</summary>
        Task<List<ReleasedAcceptance>> ResetStaleAcceptancesAsync(DateTime cutoffUtc);
        Task<bool> TransitionRequestAsync(int id, ShipmentRequestStatus from, ShipmentRequestStatus to);
        Task<ProductShippingInfo?> GetProductShippingAsync(int productId);
        Task<bool> SetProductShippingAsync(int productId, int sellerId, bool enabled, string? size, decimal? weightKg);
        Task<int> GetOrCreateShipmentAsync(int requestId, string partnerShipmentId);
        Task<Shipment?> GetShipmentAsync(int id);
        Task<Shipment?> GetShipmentByRequestAsync(int requestId);
        Task<Shipment?> GetShipmentByBarcodeAsync(string barcode);
        Task<List<Shipment>> GetOpenShipmentsAsync();
        Task<List<Shipment>> GetPendingShipmentsAsync(int olderThanMinutes);
        Task<List<Shipment>> GetExpiredHandoverShipmentsAsync();
        Task<bool> MarkHandoverExpiredAsync(int shipmentId);
        Task<bool> TryBeginRegistrationAsync(int shipmentId, int expectedAttempts);
        Task MarkRegisteredAsync(int shipmentId, string barcode, DateTime handoverDeadlineUtc);
        Task MarkRegistrationFailedAsync(int shipmentId, string? errorCode, string? errorMessage);
        Task<bool> ApplyEventAsync(int shipmentId, ShipmentStatus? status, string eventCode, DateTime eventAtUtc);
        Task<List<ShipmentEvent>> SaveEventsAsync(IEnumerable<ShipmentEvent> events);
        Task<List<ShipmentEvent>> GetEventsAsync(int shipmentId);
        Task<long> GetLastEventIdAsync(string provider);
        Task SetLastEventIdAsync(string provider, long eventId);
    }

    /// <summary>A request whose acceptance was released because it was not paid in time.</summary>
    public sealed record ReleasedAcceptance(int RequestId, int BuyerId, int SellerId, int ProductId);
}
