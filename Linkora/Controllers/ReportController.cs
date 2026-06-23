using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

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
        private readonly string _connectionString;

        public ReportController(
            IReportRepository reportRepository,
            IProductRepository productRepository,
            IConfiguration configuration,
            INotificationRepository notificationRepository)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _reportRepository = reportRepository;
            _productRepository = productRepository;
            _notificationRepository = notificationRepository;
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
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var isAbout = tab == "about";
            var whereField = isAbout ? "r.TargetUserId" : "r.AuthorId";
            var joinUserId = isAbout ? "r.AuthorId" : "r.TargetUserId";

            await using var cmd = new SqlCommand($@"
        SELECT r.Rating, r.Comment, r.CreatedAt,
               u.Id, u.UserName, u.AvatarImagePath
        FROM Reviews r
        JOIN Users u ON u.Id = {joinUserId}
        WHERE {whereField} = @UserId
        ORDER BY r.CreatedAt DESC", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            var result = new List<object>();
            while (await r.ReadAsync())
                result.Add(new
                {
                    rating = r.GetInt32(0),
                    comment = r.IsDBNull(1) ? "" : r.GetString(1),
                    createdAt = r.GetDateTime(2).ToString("dd.MM.yyyy"),
                    userId = r.GetInt32(3),
                    userName = r.IsDBNull(4) ? "Unknown" : r.GetString(4),
                    avatarPath = r.IsDBNull(5) ? null : r.GetString(5),
                });

            return Json(result);
        }
    }

    public class ReportRequest
    {
        public int ProductId { get; set; }
        public int ReportReasonId { get; set; }
        public string? Comment { get; set; }
    }
}