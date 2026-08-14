using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Text.Json;
using Linkora.Services;

namespace Linkora.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly IMaksekeskusService _mk;
        private readonly string _connectionString;

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

        public PaymentsController(IMaksekeskusService mk, IConfiguration configuration)
        {
            _mk = mk;
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        [Authorize]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> InitiatePromotion(int productId, string promotionType)
        {
            if (!PromotionPrices.TryGetValue(promotionType, out var amount))
                return BadRequest("Unknown promotion type");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            await using var checkConn = new SqlConnection(_connectionString);
            await checkConn.OpenAsync();
            await using (var ownCmd = new SqlCommand("SELECT UserId FROM Products WHERE Id = @Id", checkConn))
            {
                ownCmd.Parameters.AddWithValue("@Id", productId);
                var owner = await ownCmd.ExecuteScalarAsync();
                if (owner == null || (int)owner != userId) return Forbid();
            }

            var reference = $"PROMO{productId}{DateTime.UtcNow:HHmmss}";
            var paymentId = await CreatePaymentRowAsync(userId, "Promotion", productId, promotionType, null, amount, reference);

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
            var paymentId = await CreatePaymentRowAsync(userId, "Subscription", null, null, subscriptionType, amount, reference);

            return await StartTransactionAsync(paymentId, amount, reference);
        }

        private async Task<int> CreatePaymentRowAsync(int userId, string purpose, int? productId,
            string? promotionType, string? subscriptionType, decimal amount, string reference)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Payments (UserId, PurposeType, ProductId, PromotionType, SubscriptionType, Amount, Currency, Reference, Status, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES (@UserId, @Purpose, @ProductId, @PromotionType, @SubscriptionType, @Amount, 'EUR', @Reference, 'Created', SYSUTCDATETIME())", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Purpose", purpose);
            cmd.Parameters.AddWithValue("@ProductId", (object?)productId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PromotionType", (object?)promotionType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SubscriptionType", (object?)subscriptionType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Reference", reference);
            return (int)(await cmd.ExecuteScalarAsync())!;
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

                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "UPDATE Payments SET TransactionId = @TxId, Status = 'Pending' WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@TxId", transactionId);
                cmd.Parameters.AddWithValue("@Id", paymentId);
                await cmd.ExecuteNonQueryAsync();

                return Ok(new { redirectUrl });
            }
            catch (Exception ex)
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand("UPDATE Payments SET Status = 'Failed' WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", paymentId);
                await cmd.ExecuteNonQueryAsync();
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
            var mkStatus = root.GetProperty("status").GetString(); // COMPLETED, CANCELLED, EXPIRED и т.д.

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int paymentId, userId;
            string currentStatus, purpose;
            int? productId; string? promotionType; string? subscriptionType;

            await using (var selectCmd = new SqlCommand(
                "SELECT Id, Status, PurposeType, ProductId, PromotionType, SubscriptionType, UserId FROM Payments WHERE Reference = @Reference", conn))
            {
                selectCmd.Parameters.AddWithValue("@Reference", reference);
                await using var r = await selectCmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return "not_found";
                paymentId = r.GetInt32(0);
                currentStatus = r.GetString(1);
                purpose = r.GetString(2);
                productId = r.IsDBNull(3) ? null : r.GetInt32(3);
                promotionType = r.IsDBNull(4) ? null : r.GetString(4);
                subscriptionType = r.IsDBNull(5) ? null : r.GetString(5);
                userId = r.GetInt32(6);
            }

            if (currentStatus == "Completed") return "already_completed";

            if (mkStatus != "COMPLETED")
            {
                await using var failCmd = new SqlCommand("UPDATE Payments SET Status = 'Failed' WHERE Id = @Id", conn);
                failCmd.Parameters.AddWithValue("@Id", paymentId);
                await failCmd.ExecuteNonQueryAsync();
                return "failed";
            }

            await using (var doneCmd = new SqlCommand(
                "UPDATE Payments SET Status = 'Completed', CompletedAt = SYSUTCDATETIME() WHERE Id = @Id", conn))
            {
                doneCmd.Parameters.AddWithValue("@Id", paymentId);
                await doneCmd.ExecuteNonQueryAsync();
            }

            if (purpose == "Promotion" && productId.HasValue && promotionType != null)
            {
                await using var promoCmd = new SqlCommand(
                    "UPDATE Products SET PromotionType = @Type WHERE Id = @Id", conn);
                promoCmd.Parameters.AddWithValue("@Type", promotionType);
                promoCmd.Parameters.AddWithValue("@Id", productId.Value);
                await promoCmd.ExecuteNonQueryAsync();
            }
            else if (purpose == "Subscription" && subscriptionType != null)
            {
                await using var subCmd = new SqlCommand(
                    "UPDATE Users SET SubscriptionType = @Type WHERE Id = @Id", conn);
                subCmd.Parameters.AddWithValue("@Type", subscriptionType);
                subCmd.Parameters.AddWithValue("@Id", userId);
                await subCmd.ExecuteNonQueryAsync();
            }

            return "completed";
        }
    }
}