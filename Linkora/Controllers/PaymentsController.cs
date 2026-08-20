using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using Linkora.Services;
using Linkora.Repositories;

namespace Linkora.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly IMaksekeskusService _mk;
        private readonly IPaymentRepository _paymentRepository;

        private static readonly Dictionary<string, decimal> PromotionPrices = new()
        {
            ["Highlight"] = 2.00m,
            ["Top"] = 5.00m,
            ["Vip"] = 10.00m,
        };

        private static readonly Dictionary<string, decimal> SubscriptionPrices = new()
        {
            ["Standard"] = 4.99m,
            ["Premium"] = 9.99m,
        };

        public PaymentsController(IMaksekeskusService mk, IPaymentRepository paymentRepository)
        {
            _mk = mk;
            _paymentRepository = paymentRepository;
        }

        [Authorize]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> InitiatePromotion(int productId, string promotionType)
        {
            if (!PromotionPrices.TryGetValue(promotionType, out var amount))
                return BadRequest("Unknown promotion type");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var owner = await GetProductOwnerAsync(productId);
            if (owner == null || owner != userId) return Forbid();

            var reference = $"PROMO{productId}{DateTime.UtcNow:HHmmss}";
            var paymentId = await _paymentRepository.CreateAsync(userId, "Promotion", productId, promotionType, null, amount, reference);

            return await StartTransactionAsync(paymentId, amount, reference);
        }

        [Authorize]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> InitiateSubscription(string subscriptionType)
        {
            if (!SubscriptionPrices.TryGetValue(subscriptionType, out var amount))
                return BadRequest("Unknown subscription type");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var reference = $"SUB{userId}{DateTime.UtcNow:HHmmss}";
            var paymentId = await _paymentRepository.CreateAsync(userId, "Subscription", null, null, subscriptionType, amount, reference);

            return await StartTransactionAsync(paymentId, amount, reference);
        }
        private async Task<int?> GetProductOwnerAsync(int productId)
        {
            return await _paymentRepository.GetProductOwnerIdAsync(productId);
        }
        private async Task<IActionResult> StartTransactionAsync(int paymentId, decimal amount, string reference)
        {
            var scheme = Request.Scheme;
            var host = Request.Host.Value;
            var returnUrl = $"{scheme}://{host}/Payments/Return";
            var cancelUrl = $"{scheme}://{host}/Payments/Return";
            var notificationUrl = $"{scheme}://{host}/Payments/Notification";

            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) email = "noemail@vena.lv";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var lang = Request.Cookies["lang"] ?? "en";

            try
            {
                var (transactionId, redirectUrl) = await _mk.CreateTransactionAsync(
                    amount, "EUR", reference, email, ip, lang, returnUrl, cancelUrl, notificationUrl);

                await _paymentRepository.SetTransactionIdAsync(paymentId, transactionId);

                return Ok(new { redirectUrl });
            }
            catch (Exception ex)
            {
                await _paymentRepository.SetStatusAsync(paymentId, "Failed");
                return StatusCode(502, "Payment gateway error: " + ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Payments/Notification")]
        public async Task<IActionResult> Notification()
        {
            var json = Request.Form["json"].ToString();
            var mac = Request.Form["mac"].ToString();
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(mac)) return BadRequest();
            if (!_mk.VerifyMac(json, mac)) return Unauthorized();

            await ProcessPaymentMessageAsync(json);
            return Ok();
        }

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Payments/Return")]
        public async Task<IActionResult> Return()
        {
            var json = Request.Form["json"].ToString();
            var mac = Request.Form["mac"].ToString();

            string status = "unknown";
            if (!string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(mac) && _mk.VerifyMac(json, mac))
                status = await ProcessPaymentMessageAsync(json);

            ViewBag.Status = status;
            return View();
        }

        private async Task<string> ProcessPaymentMessageAsync(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var reference = root.GetProperty("reference").GetString();
            var mkStatus = root.GetProperty("status").GetString(); // COMPLETED, CANCELLED, EXPIRED, etc.

            var payment = await _paymentRepository.GetByReferenceAsync(reference!);
            if (payment == null) return "not_found";

            if (payment.Status == "Completed") return "already_completed";

            if (mkStatus != "COMPLETED")
            {
                await _paymentRepository.SetStatusAsync(payment.Id, "Failed");
                return "failed";
            }

            await _paymentRepository.MarkCompletedAsync(payment.Id);

            if (payment.Purpose == "Promotion" && payment.ProductId.HasValue && payment.PromotionType != null)
            {
                await _paymentRepository.ApplyPromotionAsync(payment.ProductId.Value, payment.PromotionType);
            }
            else if (payment.Purpose == "Subscription" && payment.SubscriptionType != null)
            {
                await _paymentRepository.ApplySubscriptionAsync(payment.UserId, payment.SubscriptionType);
            }

            return "completed";
        }
    }
}