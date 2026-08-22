using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly IAdminRepository _adminRepository;
        private readonly IProductRepository _productRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IAdminService _adminService;

        public AdminController(
            IAdminRepository adminRepository,
            IProductRepository productRepository,
            INotificationRepository notificationRepository,
            IAdminService adminService)
        {
            _adminRepository = adminRepository;
            _productRepository = productRepository;
            _notificationRepository = notificationRepository;
            _adminService = adminService;
        }

        private bool IsAdmin() => User.FindFirst(ClaimTypes.Role)?.Value == "admin";

        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return Forbid();

            var stats = await _adminRepository.GetDashboardStatsAsync();
            ViewBag.PendingModeration = stats.PendingModeration;
            ViewBag.PendingReports = stats.PendingReports;
            ViewBag.PendingOptions = stats.PendingOptions;
            ViewBag.Stats = stats;
            return View();
        }

        public async Task<IActionResult> Products(string status = "Moderation", int page = 1, string? search = null)
        {
            if (!IsAdmin()) return Forbid();

            var badges = await _adminRepository.GetSidebarBadgesAsync();
            ViewBag.PendingModeration = badges.PendingModeration;
            ViewBag.PendingReports = badges.PendingReports;
            ViewBag.PendingOptions = badges.PendingOptions;

            var pagedData = await _adminRepository.GetProductsAsync(status, page, search);
            ViewBag.Products = pagedData.Items;
            ViewBag.Status = status;
            ViewBag.Page = pagedData.CurrentPage;
            ViewBag.TotalPages = pagedData.TotalPages;
            ViewBag.Total = pagedData.Total;
            ViewBag.Search = search;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SetProductStatus(int id, string status)
        {
            if (!IsAdmin()) return Forbid();
            var UserId = await _adminRepository.SetProductStatusAsync(id, status);

            if (status == "Active" && UserId.HasValue)
            {
                var msg = System.Text.Json.JsonSerializer.Serialize(new { type = "product_approved" });
                await _notificationRepository.CreateAsync(UserId.Value, null, id, msg);
            }
            return Ok();
        }

        public async Task<IActionResult> Users(int page = 1, string? search = null, string role = "all")
        {
            if (!IsAdmin()) return Forbid();

            var badges = await _adminRepository.GetSidebarBadgesAsync();
            ViewBag.PendingModeration = badges.PendingModeration;
            ViewBag.PendingReports = badges.PendingReports;
            ViewBag.PendingOptions = badges.PendingOptions;

            var pagedData = await _adminRepository.GetUsersAsync(page, search, role);
            ViewBag.Users = pagedData.Items;
            ViewBag.Page = pagedData.CurrentPage;
            ViewBag.TotalPages = pagedData.TotalPages;
            ViewBag.Total = pagedData.Total;
            ViewBag.Search = search;
            ViewBag.Role = role;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SetUserRole(int id, string role)
        {
            if (!IsAdmin()) return Forbid();
            var myId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (id == myId) return BadRequest("Cannot change your own role");

            var (oldRole, banData) = await _adminService.SetUserRoleAsync(id, role);

            if (role == "banned" && oldRole != "banned")
            {
                var msg = System.Text.Json.JsonSerializer.Serialize(new { type = "user_banned" });
                await _notificationRepository.CreateAsync(id, null, null, msg);

                if (banData != null)
                {
                    if (banData.SubscriberIds.Any())
                    {
                        var subBanMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "subscription_seller_banned" });
                        foreach (var subId in banData.SubscriberIds)
                            await _notificationRepository.CreateAsync(subId, id, null, subBanMsg);
                    }

                    if (banData.FavouriteUsers.Any())
                    {
                        var favBanMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "favourite_archived_ban" });
                        foreach (var fav in banData.FavouriteUsers.Where(f => f.UserId != id))
                            await _notificationRepository.CreateAsync(fav.UserId, null, fav.ProductId, favBanMsg);
                    }
                }
            }
            else if (role != "banned" && oldRole == "banned")
            {
                var msg = System.Text.Json.JsonSerializer.Serialize(new { type = "user_unbanned" });
                await _notificationRepository.CreateAsync(id, null, null, msg);
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!IsAdmin()) return Forbid();
            var myId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (id == myId) return BadRequest("Cannot delete yourself");

            await _adminService.DeleteUserCascadeAsync(id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            if (!IsAdmin()) return Forbid();
            await _productRepository.DeleteAsync(id);
            return Ok();
        }

        public async Task<IActionResult> Reports(string status = "Pending", int page = 1)
        {
            if (!IsAdmin()) return Forbid();

            var badges = await _adminRepository.GetSidebarBadgesAsync();
            ViewBag.PendingModeration = badges.PendingModeration;
            ViewBag.PendingReports = badges.PendingReports;
            ViewBag.PendingOptions = badges.PendingOptions;

            var pagedData = await _adminRepository.GetReportsAsync(status, page);
            ViewBag.Reports = pagedData.Items;
            ViewBag.Status = status;
            ViewBag.Page = pagedData.CurrentPage;
            ViewBag.TotalPages = pagedData.TotalPages;
            ViewBag.Total = pagedData.Total;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResolveReport(int id, string action)
        {
            if (!IsAdmin()) return Forbid();
            var newStatus = await _adminRepository.ResolveReportAsync(id, action);
            return Ok(new { status = newStatus });
        }

        [HttpGet]
        public async Task<IActionResult> StatsApi()
        {
            if (!IsAdmin()) return Forbid();
            var data = await _adminRepository.GetStatsApiDataAsync();
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> ConfOptions()
        {
            if (!IsAdmin()) return Forbid();

            var badges = await _adminRepository.GetSidebarBadgesAsync();
            ViewBag.PendingModeration = badges.PendingModeration;
            ViewBag.PendingReports = badges.PendingReports;
            ViewBag.PendingOptions = badges.PendingOptions;

            var (items, totalCount) = await _productRepository.GetUnconfirmedOptionsAsync();
            ViewBag.Options = items;
            ViewBag.Total = totalCount;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOption(int id)
        {
            if (!IsAdmin()) return Forbid();
            var result = await _adminService.ApproveOptionAsync(id);

            if (result.Success && result.UserId.HasValue)
            {
                var msg = System.Text.Json.JsonSerializer.Serialize(new { type = "parameter_approved", paramName = result.ParamName ?? "", paramNameRu = result.ParamNameRu ?? "", paramNameLv = result.ParamNameLv ?? "" });
                await _notificationRepository.CreateAsync(result.UserId.Value, null, result.ProductId, msg);
            }
            return result.Success ? Ok() : BadRequest();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectProductByOption(int optionId, int productId)
        {
            if (!IsAdmin()) return Forbid();
            var result = await _adminService.RejectProductByOptionAsync(optionId, productId);

            if (result.Success && result.UserId.HasValue)
            {
                var msg = System.Text.Json.JsonSerializer.Serialize(new { type = "parameter_rejected", paramName = result.ParamName ?? "", paramNameRu = result.ParamNameRu ?? "", paramNameLv = result.ParamNameLv ?? "" });
                await _notificationRepository.CreateAsync(result.UserId.Value, null, productId, msg);
            }
            return result.Success ? Ok() : BadRequest();
        }

        [HttpPost]
        public async Task<IActionResult> RejectProductWithReason(int id, int reasonId, string? comment = null)
        {
            if (!IsAdmin()) return Forbid();
            var result = await _adminRepository.RejectProductWithReasonAsync(id, reasonId, comment);

            if (result.InvalidReason) return BadRequest("Invalid reason");
            if (!result.Success) return NotFound();

            var message = System.Text.Json.JsonSerializer.Serialize(new { type = "rejected_reason", reasonEn = result.ReasonEn, reasonLv = result.ReasonLv, reasonRu = result.ReasonRu, comment = result.Comment });
            await _notificationRepository.CreateAsync(result.UserId, null, id, message);

            return Ok();
        }
    }
}