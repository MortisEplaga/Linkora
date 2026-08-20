using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly INotificationPreferencesRepository _preferencesRepository;

        public NotificationsController(INotificationRepository notificationRepository,
            INotificationPreferencesRepository preferencesRepository)
        {
            _notificationRepository = notificationRepository;
            _preferencesRepository = preferencesRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var count = await _notificationRepository.GetUnreadCountAsync(userId);
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
        [IgnoreAntiforgeryToken]
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
            var notifications = await _notificationRepository.GetByUserAsync(userId, 20);
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
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _notificationRepository.MarkReadAsync(id, userId);
            return Ok();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _notificationRepository.MarkAllReadAsync(userId);
            return Ok();
        }
    }
}