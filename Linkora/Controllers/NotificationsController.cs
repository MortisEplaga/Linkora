using Linkora.Models;
using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notifications;
        private readonly INotificationPreferencesRepository _preferencesRepository;

        public NotificationsController(INotificationService notifications, INotificationPreferencesRepository preferencesRepository)
        {
            _notifications = notifications;
            _preferencesRepository = preferencesRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var count = await _notifications.GetUnreadCountAsync(userId);
            return Json(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> Preferences()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var prefs = await _preferencesRepository.GetAsync(userId);
            return Json(new
            {
                deals = prefs.Deals,
                reviews = prefs.Reviews,
                moderation = prefs.Moderation,
                account = prefs.Account,
                favourites = prefs.Favourites,
                newListings = prefs.NewListings
            });
        }

        [HttpPost]
        public async Task<IActionResult> SavePreferences([FromBody] NotificationPreferencesDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _preferencesRepository.SaveAsync(new NotificationPreferences
            {
                UserId = userId,
                Deals = dto.Deals,
                Reviews = dto.Reviews,
                Moderation = dto.Moderation,
                Account = dto.Account,
                Favourites = dto.Favourites,
                NewListings = dto.NewListings
            });
            return Ok();
        }
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var notifications = await _notifications.GetByUserAsync(userId, 20);
            return Json(notifications.Select(n => new
            {
                id = n.Id,
                text = n.Text,
                isRead = n.IsRead,
                createdAt = n.CreatedAt.ToString("dd MMM, HH:mm"),
                fromUserId = n.FromUserId,
                fromUserName = n.FromUserName,
                fromUserAvatar = n.FromUserAvatar,
                productId = n.ProductId,
                productName = n.ProductName,
                productImage = n.ProductImage,
            }));
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _notifications.MarkReadAsync(id, userId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _notifications.MarkAllReadAsync(userId);
            return Ok();
        }
    }
}