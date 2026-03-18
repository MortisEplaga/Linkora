using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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

        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        public ReportController(
            IReportRepository reportRepository,
            IProductRepository productRepository,
            IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;

            _reportRepository = reportRepository;
            _productRepository = productRepository;
        }
        // Controllers/ReportController.cs (добавить)
        [HttpGet("reasons")]
        public async Task<IActionResult> GetReportReasons()
        {
            await using var conn = new SqlConnection(_connectionString); // Вам понадобится _connectionString
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT Id, ReasonText FROM ReportReasons WHERE IsActive = 1 ORDER BY ReasonText", conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var reasons = new List<object>();
            while (await reader.ReadAsync())
            {
                reasons.Add(new { id = reader.GetInt32(0), text = reader.GetString(1) });
            }
            return Ok(reasons);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateReport([FromBody] ReportRequest request)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            // Проверяем, существует ли продукт
            var product = await _productRepository.GetByIdAsync(request.ProductId);
            if (product == null)
                return NotFound("Продукт не найден");

            // Нельзя жаловаться на свой продукт
            //if (product.UserId == userId)
            //    return BadRequest("Нельзя жаловаться на свое объявление");

            var report = await _reportRepository.CreateReportAsync(
                request.ProductId,
                userId,
                request.ReportReasonId,
                request.Comment);

            return Ok(new { success = true, reportId = report.Id });
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> My(string tab = "about")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // "about" — отзывы обо мне, "from" — отзывы от меня
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