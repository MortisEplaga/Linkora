using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Linkora.Models;

namespace Linkora.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly IMessageRepository _messageRepository;

        public MessagesController(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
        }

        public async Task<IActionResult> Index(int? id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var conversations = await _messageRepository.GetConversationsAsync(userId);

            string userRole = await _messageRepository.GetUserStatusAsync(userId);

            ViewBag.Conversations = conversations;
            ViewBag.ActiveId = id;
            ViewBag.ActiveConv = null;
            ViewBag.Messages = null;
            ViewBag.CurrentUserIsBanned = userRole == "banned";
            ViewBag.IsCurrentUserAdmin = userRole == "admin";
            if (ViewBag.CurrentUserIsBanned == true)
            {
                conversations = conversations.Where(c => c.IsSupport).ToList();
            }

            if (id.HasValue)
            {
                var conv = await _messageRepository.GetConversationAsync(id.Value, userId);
                if (conv != null)
                {
                    var messages = await _messageRepository.GetMessagesAsync(id.Value, userId);
                    await _messageRepository.MarkReadAsync(id.Value, userId);
                    ViewBag.ActiveConv = conv;
                    ViewBag.Messages = messages;
                }
            }

            return View();
        }
        [HttpPost]
        public async Task<IActionResult> StartSupportChat()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var convId = await _messageRepository.GetOrCreateSupportConversationAsync(userId);
            return Ok(new { conversationId = convId });
        }
        [HttpPost]
        public async Task<IActionResult> Start([FromBody] StartMessageDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var convId = await _messageRepository.GetOrCreateConversationAsync(
                dto.ProductId, userId, dto.SellerId);

            if (!string.IsNullOrWhiteSpace(dto.Text))
                await _messageRepository.SendMessageAsync(convId, userId, dto.Text);

            return Ok(new { conversationId = convId });
        }

        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var count = await _messageRepository.GetUnreadCountAsync(userId);
            return Json(new { count });
        }
    }
}