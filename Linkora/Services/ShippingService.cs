using Linkora.Models;
using Linkora.Repositories;
using System.Text.RegularExpressions;

namespace Linkora.Services
{
    public interface IShippingService
    {
        Task OnPaymentCompletedAsync(int paymentId, string reference, int payerId, decimal amount);
        Task<bool> TryRegisterAsync(int shipmentId, CancellationToken ct = default);
    }

    public partial class ShippingService(IShipmentRepository shipments, IParcelLockerRepository lockers, IOmnivaService omniva, IConfiguration configuration, ILogger<ShippingService> logger) : IShippingService
    {
        private readonly int _handoverDays = configuration.GetValue("Shipping:HandoverDays", 3);

        [GeneratedRegex(@"^SHIP(\d+)-")]
        private static partial Regex ReferencePattern();
        public static string BuildReference(int requestId) => $"SHIP{requestId}-{DateTime.UtcNow:HHmmssfff}";
        public async Task OnPaymentCompletedAsync(int paymentId, string reference, int payerId, decimal amount)
        {
            var match = ReferencePattern().Match(reference);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var requestId))
            {
                logger.LogWarning("Shipping payment {PaymentId}: reference '{Reference}' has no request id", paymentId, reference);
                return;
            }

            var request = await shipments.GetRequestAsync(requestId);
            if (request == null || request.SellerId != payerId)
            {
                logger.LogWarning("Shipping payment {PaymentId}: request {RequestId} not found or belongs to another seller", paymentId, requestId);
                return;
            }

            if (request.Price.HasValue && request.Price.Value != amount)
                logger.LogWarning("Shipping payment {PaymentId}: amount {Amount} differs from the quoted price {Price} of request {RequestId}", paymentId, amount, request.Price, requestId);

            if (request.Status == ShipmentRequestStatus.Accepted)
                await shipments.TransitionRequestAsync(request.Id, ShipmentRequestStatus.Accepted, ShipmentRequestStatus.Paid);
            else if (request.Status != ShipmentRequestStatus.Paid)
            {
                logger.LogWarning("Shipping payment {PaymentId} completed for request {RequestId} in status {Status}; a manual refund is required", paymentId, requestId, request.Status);
                return;
            }
            else if (request.PaymentId.HasValue && request.PaymentId.Value != paymentId)
                logger.LogWarning("Shipping payment {PaymentId} is a duplicate for request {RequestId} (already paid by payment {PaidPaymentId}); a manual refund is required", paymentId, requestId, request.PaymentId);

            var shipmentId = await shipments.GetOrCreateShipmentAsync(request.Id, $"VENA{request.Id}");

            try { await TryRegisterAsync(shipmentId); }
            catch (Exception ex) { logger.LogError(ex, "Shipment {ShipmentId} registration failed unexpectedly; it stays Pending for the retry job", shipmentId); }
        }
        public async Task<bool> TryRegisterAsync(int shipmentId, CancellationToken ct = default)
        {
            var shipment = await shipments.GetShipmentAsync(shipmentId);
            if (shipment == null || shipment.Status != ShipmentStatus.Pending || shipment.Barcode != null) return false;
            if (!await shipments.TryBeginRegistrationAsync(shipment.Id, shipment.Attempts)) return false;

            var request = await shipments.GetRequestAsync(shipment.RequestId);
            var locker = request == null ? null : await lockers.GetByIdAsync(request.LockerId);
            if (request == null || locker == null)
            {
                await shipments.MarkRegistrationFailedAsync(shipment.Id, "DATA_MISSING", "Shipment request or parcel locker not found");
                return false;
            }

            var sender = new OmnivaParty(request.SenderName ?? "", request.SenderPhone, request.SenderEmail, request.SenderStreet, request.SenderCity, request.SenderPostcode, request.SenderCountry ?? "LV");
            var receiver = new OmnivaParty(request.ReceiverName, request.ReceiverPhone, request.ReceiverEmail, null, null, null, locker.Country);

            var result = await omniva.RegisterShipmentAsync(new OmnivaShipmentRequest(shipment.PartnerShipmentId, sender, receiver, locker.Code, request.WeightKg), ct);

            if (result.Success && !string.IsNullOrEmpty(result.Barcode))
            {
                await shipments.MarkRegisteredAsync(shipment.Id, result.Barcode, shipment.CreatedAt.AddDays(_handoverDays));
                return true;
            }

            logger.LogWarning("Shipment {ShipmentId} registration error {Code}: {Message}", shipment.Id, result.ErrorCode, result.ErrorMessage);

            if (result.ErrorCode != "NETWORK") await shipments.MarkRegistrationFailedAsync(shipment.Id, result.ErrorCode, result.ErrorMessage);
            return false;
        }
    }
}