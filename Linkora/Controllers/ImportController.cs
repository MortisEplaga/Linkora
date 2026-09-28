using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Authorize]
    public class ImportController(IListingImportService import, IUserRepository users) : Controller
    {
        [HttpGet] public IActionResult Index() => View();
        [HttpGet] public async Task<IActionResult> Template(int categoryId, string format = "xlsx")
        {
            if (await users.IsBannedAsync(User.GetUserId())) return Forbid();
            if (format != "csv" && format != "xlsx") return BadRequest();
            try
            {
                var (data, type, name) = await import.BuildTemplateAsync(categoryId, format, Request.GetLang());
                return File(data, type, name);
            }
            catch (ArgumentException) { return NotFound(); }
        }
        [HttpPost] [EnableRateLimiting("chat")] [RequestSizeLimit(ListingImportService.MaxFileBytes)] public async Task<IActionResult> Upload(IFormFile file)
        {
            var userId = User.GetUserId();
            if (await users.IsBannedAsync(userId)) return Forbid();
            if (file == null || file.Length == 0 || file.Length > ListingImportService.MaxFileBytes) return BadRequest("Invalid file size");

            var result = await import.ImportAsync(userId, User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown", file, Request.GetLang());
            return Json(new { created = result.Created, errors = result.Errors, ids = result.ProductIds });
        }
    }
}