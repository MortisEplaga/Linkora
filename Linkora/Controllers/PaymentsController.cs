using Linkora.Models;
using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace Linkora.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly IMaksekeskusService _mk;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPromotionRepository _promotionRepository;
        private readonly IPromotionPricingService _pricing;

        private static (decimal FinalPrice, int PointsSpent) ApplyPointsDiscount(decimal price, int availablePoints)
        {
            var discountPercent = Math.Min(availablePoints, 100);
            var finalPrice = Math.Round(price * (100 - discountPercent) / 100m, 2);
            return (finalPrice, discountPercent);
        }
        public PaymentsController(IMaksekeskusService mk, IPaymentRepository paymentRepository, IUserRepository userRepository,
            IPromotionRepository promotionRepository, IPromotionPricingService pricing)
        {
            _mk = mk;
            _paymentRepository = paymentRepository;
            _userRepository = userRepository;
            _promotionRepository = promotionRepository;
            _pricing = pricing;
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> InitiatePromotion(int productId, string promotionType)
        {
            if (!Enum.TryParse<PromotionTier>(promotionType, true, out var tier)) return BadRequest("Unknown promotion type");

            var userId = User.GetUserId();

            var owner = await GetProductOwnerAsync(productId);
            if (owner == null || owner != userId) return Forbid();

            var subscription = await _promotionRepository.GetActiveAsync(userId);
            var price = _pricing.CalculateListingBoostPayable(tier, subscription);

            if (price <= 0) return Ok(new { redirectUrl = (string?)null, coveredBySubscription = true });

            var user = await _userRepository.GetByIdAsync(userId);
            var (finalPrice, pointsSpent) = ApplyPointsDiscount(price, user?.PromotionPoints ?? 0);

            var reference = $"PROMO{productId}{DateTime.UtcNow:HHmmss}";
            var paymentId = await _paymentRepository.CreateAsync(userId, "Promotion", productId, promotionType, null, finalPrice, reference, pointsSpent);

            return await StartTransactionAsync(paymentId, finalPrice, reference);
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> InitiateSubscription(string subscriptionType, string termType)
        {
            if (!Enum.TryParse<PromotionTier>(subscriptionType, true, out var tier)) return BadRequest("Unknown subscription tier");
            if (!Enum.TryParse<PromotionTermType>(termType, true, out var term)) return BadRequest("Unknown term type");

            var userId = User.GetUserId();

            var user = await _userRepository.GetByIdAsync(userId);
            var current = await _promotionRepository.GetActiveAsync(userId);

            var price = current != null
                ? _pricing.CalculateUpgrade(current, tier, term, DateTime.UtcNow).Payable
                : _pricing.GetPrice(tier, term);

            var (finalPrice, pointsSpent) = ApplyPointsDiscount(price, user?.PromotionPoints ?? 0);

            var reference = $"SUB{userId}{DateTime.UtcNow:HHmmss}";
            var paymentId = await _paymentRepository.CreateAsync(userId, "Subscription", null, null, $"{tier}:{term}", finalPrice, reference, pointsSpent);

            return await StartTransactionAsync(paymentId, finalPrice, reference);
        }
        private async Task<int?> GetProductOwnerAsync(int productId) => await _paymentRepository.GetProductUserIdAsync(productId);
        private async Task<IActionResult> StartTransactionAsync(int paymentId, decimal price, string reference)
        {
            var scheme = Request.Scheme;
            var host = "vena.lv";//Request.Host.Value;
            var returnUrl = $"{scheme}://{host}/Payments/Return";
            var cancelUrl = $"{scheme}://{host}/Payments/Return";
            var notificationUrl = $"{scheme}://{host}/Payments/Notification";

            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) email = "noemail@vena.lv";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            try
            {
                var (transactionId, redirectUrl) = await _mk.CreateTransactionAsync(price, "EUR", reference, email, ip, Request.GetLang(), returnUrl, cancelUrl, notificationUrl);

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
            var mkStatus = root.GetProperty("status").GetString();

            var payment = await _paymentRepository.GetByReferenceAsync(reference!);
            if (payment == null) return "not_found";

            if (payment.Status == "Completed") return "already_completed";

            if (mkStatus != "COMPLETED")
            {
                await _paymentRepository.SetStatusAsync(payment.Id, "Failed");
                return "failed";
            }

            await _paymentRepository.MarkCompletedAsync(payment.Id);

            if (payment.PurposeType == "Promotion" && payment.ProductId.HasValue && payment.PromotionTier != null)
            {
                if (!Enum.TryParse<PromotionTier>(payment.PromotionTier, out var tier)) return "bad_promotion_payload";

                var expiresAt = _pricing.CalculateExpiry(PromotionTermType.Week, DateTime.UtcNow);
                await _paymentRepository.ApplyPromotionAsync(payment.ProductId.Value, tier, expiresAt);

                var earnedPoints = UserRepository.PromotionPoints(payment.PromotionTier);
                var netDelta = earnedPoints - payment.PointsSpent;
                if (netDelta != 0) await _userRepository.AdjustPromotionPointsAsync(payment.UserId, netDelta);
            }
            else if (payment.PurposeType == "Subscription" && payment.SubscriptionTier != null)
            {
                var parts = payment.SubscriptionTier.Split(':');
                if (parts.Length != 2
                    || !Enum.TryParse<PromotionTier>(parts[0], out var tier)
                    || !Enum.TryParse<PromotionTermType>(parts[1], out var term))
                    return "bad_subscription_payload";

                var current = await _promotionRepository.GetActiveAsync(payment.UserId);
                if (current != null) await _promotionRepository.SupersedeAsync(current.Id);

                var startedAt = DateTime.UtcNow;
                var expiresAt = _pricing.CalculateExpiry(term, startedAt);
                await _promotionRepository.CreateAsync(payment.UserId, tier, term, startedAt, expiresAt, payment.Price);

                var earnedPoints = UserRepository.PromotionPoints(tier.ToString()) * 5;
                var netDelta = earnedPoints - payment.PointsSpent;
                if (netDelta != 0) await _userRepository.AdjustPromotionPointsAsync(payment.UserId, netDelta);
            }

            return "completed";
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Quote(string type, string? promotionType = null, string? subscriptionType = null, string? termType = null)
        {
            var userId = User.GetUserId();
            var user = await _userRepository.GetByIdAsync(userId);
            decimal basePrice;

            if (type == "promotion")
            {
                if (promotionType == null || !Enum.TryParse<PromotionTier>(promotionType, true, out var tier)) return BadRequest("Unknown promotion type");
                var subscription = await _promotionRepository.GetActiveAsync(userId);
                basePrice = _pricing.CalculateListingBoostPayable(tier, subscription);
            }
            else if (type == "subscription")
            {
                if (subscriptionType == null || !Enum.TryParse<PromotionTier>(subscriptionType, true, out var tier)) return BadRequest("Unknown subscription type");
                if (termType == null || !Enum.TryParse<PromotionTermType>(termType, true, out var term)) return BadRequest("Unknown term type");
                var current = await _promotionRepository.GetActiveAsync(userId);
                basePrice = current != null
                    ? _pricing.CalculateUpgrade(current, tier, term, DateTime.UtcNow).Payable
                    : _pricing.GetPrice(tier, term);
            }
            else return BadRequest("Unknown type");

            var (finalPrice, pointsSpent) = ApplyPointsDiscount(basePrice, user?.PromotionPoints ?? 0);

            return Ok(new
            {
                originalPrice = basePrice,
                discountPercent = pointsSpent,
                finalPrice,
                pointsAvailable = user?.PromotionPoints ?? 0
            });
        }
    }
}