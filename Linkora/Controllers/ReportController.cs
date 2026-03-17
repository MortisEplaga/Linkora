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
    public class ReportController : ControllerBase
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
    }

    public class ReportRequest
    {
        public int ProductId { get; set; }
        public int ReportReasonId { get; set; }
        public string? Comment { get; set; }
    }
}