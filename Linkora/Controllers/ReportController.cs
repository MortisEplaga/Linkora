using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Linkora.Models;

namespace Linkora.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {
        private readonly IReportRepository _reportRepository;
        private readonly IProductRepository _productRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly string _connectionString;

        public ReportController(
            IReportRepository reportRepository,
            IProductRepository productRepository,
            IConfiguration configuration,
            INotificationRepository notificationRepository,
            IReviewRepository reviewRepository)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _reportRepository = reportRepository;
            _productRepository = productRepository;
            _notificationRepository = notificationRepository;
            _reviewRepository = reviewRepository;
        }

        [HttpGet("reasons")]
        public async Task<IActionResult> GetReportReasons()
        {
            var lang = Request.Cookies["lang"] ?? "en";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, ReasonText, ReasonTextLV, ReasonTextRU FROM ReportReasons WHERE IsActive = 1 ORDER BY ReasonText", conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var reasons = new List<object>();
            while (await reader.ReadAsync())
            {
                var textEn = reader.GetString(1);
                var text = lang switch
                {
                    "lv" => reader.IsDBNull(2) ? textEn : reader.GetString(2),
                    "ru" => reader.IsDBNull(3) ? textEn : reader.GetString(3),
                    _ => textEn
                };
                reasons.Add(new { id = reader.GetInt32(0), text });
            }
            return Ok(reasons);
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateReport([FromBody] ReportRequest request)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var product = await _productRepository.GetByIdAsync(request.ProductId);
            if (product == null)
                return NotFound("Продукт не найден");

            var report = await _reportRepository.CreateReportAsync(
                request.ProductId,
                userId,
                request.ReportReasonId,
                request.Comment);

            if (product.UserId.HasValue && product.UserId.Value != userId)
            {
                string reasonEn = "", reasonLv = "", reasonRu = "";
                await using var rConn = new SqlConnection(_connectionString);
                await rConn.OpenAsync();
                await using var rCmd = new SqlCommand(
                    "SELECT ReasonText, ReasonTextLV, ReasonTextRU FROM ReportReasons WHERE Id = @Id", rConn);
                rCmd.Parameters.AddWithValue("@Id", request.ReportReasonId);
                await using var rR = await rCmd.ExecuteReaderAsync();
                if (await rR.ReadAsync())
                {
                    reasonEn = rR.IsDBNull(0) ? "" : rR.GetString(0);
                    reasonLv = rR.IsDBNull(1) ? reasonEn : rR.GetString(1);
                    reasonRu = rR.IsDBNull(2) ? reasonEn : rR.GetString(2);
                }

                var msg = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "report_on_product",
                    reasonEn,
                    reasonLv,
                    reasonRu
                });
                await _notificationRepository.CreateAsync(product.UserId.Value, null, request.ProductId, msg);
            }

            return Ok(new { success = true, reportId = report.Id });
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> My(string tab = "about")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var reviews = await _reviewRepository.GetUserReviewsAsync(userId, tab);

            var result = reviews.Select(r => new
            {
                rating = r.Rating,
                comment = r.Comment,
                createdAt = r.CreatedAt.ToString("dd.MM.yyyy"),
                userId = r.UserId,
                userName = r.UserName,
                avatarPath = r.AvatarPath,
            });

            return Json(result);
        }
    }
}