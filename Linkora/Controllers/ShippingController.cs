using Linkora.Models;
using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Linkora.Controllers
{
    [Authorize][ApiController][Route("[controller]")]
    public class ShippingController(IShipmentRepository shipments, IParcelLockerRepository lockers, IShippingTariffRepository tariffs, IUserRepository users, INotificationService notifications, ILogger<ShippingController> logger, IOmnivaService omniva) : ControllerBase
    {
        private const string Provider = "Omniva";
        private static readonly string[] Countries = ["LV", "LT", "EE"];

        [HttpGet("Lockers")] public async Task<IActionResult> Lockers(string country = "LV", string? q = null)
        {
            country = country.Trim().ToUpperInvariant();
            if (!Countries.Contains(country)) return BadRequest(new { error = "country_unsupported" });

            return Ok((await lockers.SearchAsync(country, q)).Select(l => new { id = l.Id, name = l.Name, city = l.City, address = l.Address, lat = l.Lat, lng = l.Lng }));
        }

        [HttpPost("ProductSettings/{productId:int}")] public async Task<IActionResult> ProductSettings(int productId, [FromBody] ProductShippingSettingsDto dto)
        {
            var sellerId = User.GetUserId();
            if (await users.IsBannedAsync(sellerId)) return Forbid();

            var size = dto.Size?.Trim().ToUpperInvariant();
            var error = ParcelRules.Validate(dto.Enabled, size, dto.WeightKg);
            if (error != null) return BadRequest(new { error });

            if (!dto.Enabled)
            {
                size = null;
                dto.WeightKg = null;
            }

            return await shipments.SetProductShippingAsync(productId, sellerId, dto.Enabled, size, dto.WeightKg) ? Ok() : Forbid();
        }

        [HttpPost("Request")][EnableRateLimiting("chat")] public async Task<IActionResult> Create([FromBody] CreateShipmentRequestDto dto)
        {
            var buyerId = User.GetUserId();
            if (await users.IsBannedAsync(buyerId)) return Forbid();

            var name = dto.ReceiverName?.Trim() ?? "";
            if (name.Length is 0 or > 100) return BadRequest(new { error = "receiver_name_invalid" });

            if (!OmnivaPhone.TryNormalize(dto.ReceiverPhone, out var phone)) return BadRequest(new { error = "phone_invalid" });

            var email = string.IsNullOrWhiteSpace(dto.ReceiverEmail) ? (await users.GetByIdAsync(buyerId))?.Email : dto.ReceiverEmail.Trim();
            if (email is { Length: > 200 }) email = null;
            if (phone == null && string.IsNullOrWhiteSpace(email)) return BadRequest(new { error = "contact_required" });

            var info = await shipments.GetProductShippingAsync(dto.ProductId);
            if (info == null) return NotFound();
            if (info.SellerId == buyerId) return BadRequest(new { error = "own_product" });
            if (info.SellerId == null || !IsActive(info.Status) || !info.ShippingEnabled) return BadRequest(new { error = "shipping_unavailable" });

            var locker = await lockers.GetByIdAsync(dto.LockerId);
            if (locker == null || !locker.IsActive) return BadRequest(new { error = "locker_invalid" });

            int id;
            try
            {
                id = await shipments.CreateRequestAsync(new ShipmentRequest
                {
                    ProductId = info.ProductId,
                    BuyerId = buyerId,
                    SellerId = info.SellerId.Value,
                    LockerId = locker.Id,
                    ReceiverName = name,
                    ReceiverPhone = phone,
                    ReceiverEmail = email
                });
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return Conflict(new { error = "request_exists" });
            }

            await NotifyAsync(info.SellerId.Value, buyerId, info.ProductId, "shipment_requested", id);
            return Ok(new { id });
        }

        /// <summary>The buyer's own requests, optionally for one listing (shown in the buyer's request window).</summary>
        [HttpGet("Requests/Buyer")] public async Task<IActionResult> BuyerRequests(int? productId = null)
        {
            var requests = await shipments.GetBuyerRequestsAsync(User.GetUserId());
            return Ok(requests
                .Where(r => productId == null || r.ProductId == productId)
                .Select(r => new
                {
                    id = r.Id,
                    productId = r.ProductId,
                    status = r.Status.ToString(),
                    lockerName = r.LockerName,
                    lockerCity = r.LockerCity,
                    price = r.Price,
                    createdAt = r.CreatedAt,
                    shipmentStatus = r.ShipmentState?.ToString(),
                    barcode = r.Barcode,
                    handoverDeadline = r.HandoverDeadline,
                    errorCode = r.ErrorCode
                }));
        }

        [HttpGet("Price")] public async Task<IActionResult> Price(int requestId, string size)
        {
            var request = await shipments.GetRequestAsync(requestId);
            if (request == null) return NotFound();
            if (request.SellerId != User.GetUserId()) return Forbid();

            size = size.Trim().ToUpperInvariant();
            if (!ParcelSizes.IsValid(size)) return BadRequest(new { error = "size_invalid" });

            var locker = await lockers.GetByIdAsync(request.LockerId);
            var tariff = locker == null ? null : await tariffs.GetAsync(Provider, locker.Country, size);
            if (tariff == null) return NotFound(new { error = "tariff_missing" });

            return Ok(new { price = tariff.Price, maxWeightKg = tariff.MaxWeightKg });
        }

        [HttpPost("Request/{id:int}/Accept")] public async Task<IActionResult> Accept(int id, [FromBody] AcceptShipmentRequestDto dto)
        {
            var sellerId = User.GetUserId();
            if (await users.IsBannedAsync(sellerId)) return Forbid();

            var request = await shipments.GetRequestAsync(id);
            if (request == null) return NotFound();
            if (request.SellerId != sellerId) return Forbid();
            if (request.Status != ShipmentRequestStatus.Requested) return Conflict(new { error = "status_changed" });

            var info = await shipments.GetProductShippingAsync(request.ProductId);
            if (info == null || !IsActive(info.Status) || !info.ShippingEnabled) return BadRequest(new { error = "shipping_unavailable" });

            var size = (dto.Size ?? info.ParcelSize)?.Trim().ToUpperInvariant();
            var weight = dto.WeightKg ?? info.WeightKg;
            var parcelError = ParcelRules.Validate(true, size, weight);
            if (parcelError != null) return BadRequest(new { error = parcelError });

            var locker = await lockers.GetByIdAsync(request.LockerId);
            if (locker == null || !locker.IsActive) return BadRequest(new { error = "locker_invalid" });

            var tariff = await tariffs.GetAsync(Provider, locker.Country, size!);
            if (tariff == null) return BadRequest(new { error = "tariff_missing" });
            if (tariff.MaxWeightKg.HasValue && weight > tariff.MaxWeightKg) return BadRequest(new { error = "weight_exceeds_limit" });

            var senderName = dto.SenderName?.Trim() ?? "";
            var street = dto.SenderStreet?.Trim() ?? "";
            var city = dto.SenderCity?.Trim() ?? "";
            var postcode = dto.SenderPostcode?.Trim() ?? "";
            var country = (dto.SenderCountry ?? "LV").Trim().ToUpperInvariant();

            if (senderName.Length is 0 or > 100) return BadRequest(new { error = "sender_name_invalid" });
            if (street.Length is 0 or > 120 || city.Length is 0 or > 80 || postcode.Length is 0 or > 10) return BadRequest(new { error = "sender_address_invalid" });
            if (!Countries.Contains(country)) return BadRequest(new { error = "sender_country_invalid" });
            if (!OmnivaPhone.TryNormalize(dto.SenderPhone, out var senderPhone)) return BadRequest(new { error = "phone_invalid" });

            var senderEmail = string.IsNullOrWhiteSpace(dto.SenderEmail) ? (await users.GetByIdAsync(sellerId))?.Email : dto.SenderEmail.Trim();
            if (senderEmail is { Length: > 200 }) senderEmail = null;
            if (senderPhone == null && string.IsNullOrWhiteSpace(senderEmail)) return BadRequest(new { error = "contact_required" });

            bool accepted;
            try
            {
                accepted = await shipments.AcceptRequestAsync(new ShipmentRequest
                {
                    Id = id,
                    SenderName = senderName,
                    SenderPhone = senderPhone,
                    SenderEmail = senderEmail,
                    SenderStreet = street,
                    SenderCity = city,
                    SenderPostcode = postcode,
                    SenderCountry = country,
                    Size = size,
                    WeightKg = weight,
                    Price = tariff.Price
                });
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return Conflict(new { error = "product_reserved" });
            }

            if (!accepted)
            {
                // The request is no longer Requested, or another request for the same listing was accepted first.
                if (await shipments.HasReservedRequestAsync(request.ProductId, id)) return Conflict(new { error = "product_reserved" });
                return Conflict(new { error = "status_changed" });
            }

            await NotifyAsync(request.BuyerId, sellerId, request.ProductId, "shipment_accepted", id);
            return Ok(new { id, size, price = tariff.Price });
        }

        [HttpPost("Request/{id:int}/Decline")] public async Task<IActionResult> Decline(int id)
        {
            var request = await shipments.GetRequestAsync(id);
            if (request == null) return NotFound();
            if (request.SellerId != User.GetUserId()) return Forbid();

            if (!await shipments.TransitionRequestAsync(id, ShipmentRequestStatus.Requested, ShipmentRequestStatus.Declined)) return Conflict(new { error = "status_changed" });

            await NotifyAsync(request.BuyerId, request.SellerId, request.ProductId, "shipment_declined", id);
            return Ok();
        }

        [HttpPost("Request/{id:int}/Cancel")] public async Task<IActionResult> Cancel(int id)
        {
            var request = await shipments.GetRequestAsync(id);
            if (request == null) return NotFound();
            if (request.BuyerId != User.GetUserId()) return Forbid();

            var cancelled = await shipments.TransitionRequestAsync(id, ShipmentRequestStatus.Requested, ShipmentRequestStatus.Cancelled)
                         || await shipments.TransitionRequestAsync(id, ShipmentRequestStatus.Accepted, ShipmentRequestStatus.Cancelled);

            return cancelled ? Ok() : Conflict(new { error = "status_changed" });
        }

        /// <summary>Omniva parcel label (PDF) for the seller of a paid shipment.</summary>
        [HttpGet("Request/{id:int}/Label")] public async Task<IActionResult> Label(int id)
        {
            var request = await shipments.GetRequestAsync(id);
            if (request == null) return NotFound();
            if (request.SellerId != User.GetUserId()) return Forbid();

            var shipment = await shipments.GetShipmentByRequestAsync(id);
            if (shipment?.Barcode == null || shipment.Status == ShipmentStatus.Failed) return BadRequest(new { error = "label_unavailable" });

            var result = await omniva.GetLabelAsync(shipment.Barcode);
            if (!result.Success || result.Pdf == null)
            {
                logger.LogWarning("Label for request {RequestId} (barcode {Barcode}) failed: {Code}", id, shipment.Barcode, result.ErrorCode);
                return BadRequest(new { error = result.ErrorCode ?? "label_failed" });
            }

            return File(result.Pdf, "application/pdf", $"omniva-label-{id}.pdf");
        }

        private static bool IsActive(string status) => string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
        private async Task NotifyAsync(int userId, int fromUserId, int productId, string type, int requestId)
        {
            try { await notifications.CreateAsync(userId, fromUserId, productId, JsonSerializer.Serialize(new { type, requestId })); }
            catch (Exception ex) { logger.LogError(ex, "Failed to create notification {Type} for request {RequestId}", type, requestId); }
        }
    }
}
