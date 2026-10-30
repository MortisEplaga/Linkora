using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IShipmentRepository
    {
        Task<int> CreateRequestAsync(ShipmentRequest request);
        Task<ShipmentRequest?> GetRequestAsync(int id);
        Task<ShipmentRequest?> GetRequestByPaymentAsync(int paymentId);
        Task<List<ShipmentRequest>> GetRequestsForSellerAsync(int sellerId);
        Task<List<ShipmentRequest>> GetRequestsForBuyerAsync(int buyerId);
        Task<bool> AcceptRequestAsync(ShipmentRequest request);
        Task<bool> AttachPaymentAsync(int requestId, int paymentId);
        Task<bool> TransitionRequestAsync(int id, ShipmentRequestStatus from, ShipmentRequestStatus to);
        Task<int> GetOrCreateShipmentAsync(int requestId, string partnerShipmentId);
        Task<Shipment?> GetShipmentAsync(int id);
        Task<Shipment?> GetShipmentByRequestAsync(int requestId);
        Task<Shipment?> GetShipmentByBarcodeAsync(string barcode);
        Task<List<Shipment>> GetOpenShipmentsAsync();
        Task<List<Shipment>> GetPendingShipmentsAsync();
        Task<bool> TryBeginRegistrationAsync(int shipmentId, int expectedAttempts);
        Task MarkRegisteredAsync(int shipmentId, string barcode, DateTime handoverDeadlineUtc);
        Task MarkRegistrationFailedAsync(int shipmentId, string? errorCode, string? errorMessage);
        Task<bool> ResetFailedShipmentAsync(int shipmentId);
        Task ApplyStatusAsync(int shipmentId, ShipmentStatus status, string eventCode, DateTime eventAtUtc);
        Task<int> SaveEventsAsync(IEnumerable<ShipmentEvent> events);
        Task<List<ShipmentEvent>> GetEventsAsync(int shipmentId);
        Task<long> GetLastEventIdAsync(string provider);
        Task SetLastEventIdAsync(string provider, long eventId);
    }
}