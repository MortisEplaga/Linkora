using Linkora.Models;
using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly INotificationService _notifications;

        public ReportController(
            IReportRepository reportRepository,
            IProductRepository productRepository,
            INotificationService notifications)
        {
            _reportRepository = reportRepository;
            _productRepository = productRepository;
            _notifications = notifications;
        }

        [HttpGet("reasons")]
        public async Task<IActionResult> GetReportReasons()
        {
            var reasons = await _reportRepository.GetActiveReasonsLocalizedAsync();
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
                var reason = await _reportRepository.GetReasonByIdAsync(request.ReportReasonId);

                var reasonEn = reason?.ReasonText ?? "";
                var reasonLv = reason?.ReasonTextLV ?? reasonEn;
                var reasonRu = reason?.ReasonTextRU ?? reasonEn;

                var payload = new
                {
                    type = "report_on_product",
                    reasonEn,
                    reasonLv,
                    reasonRu
                };
                var msg = System.Text.Json.JsonSerializer.Serialize(payload);

                await _notifications.CreateAsync(product.UserId.Value, null, request.ProductId, msg);
            }

            return Ok(new { success = true, reportId = report.Id });
        }
    }
}