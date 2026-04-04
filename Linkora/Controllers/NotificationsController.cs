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

        public NotificationsController(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var count = await _notificationRepository.GetUnreadCountAsync(userId);
            return Json(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var notifications = await _notificationRepository.GetByUserAsync(userId, 20);
            return Json(notifications.Select(n => new
            {
                id = n.Id,
                message = n.Message,
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