using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        // Страница сообщений (список + опционально открытый диалог)
        public async Task<IActionResult> Index(int? id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var conversations = await _messageRepository.GetConversationsAsync(userId);

            ViewBag.Conversations = conversations;
            ViewBag.ActiveId = id;
            ViewBag.ActiveConv = null;
            ViewBag.Messages = null;

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

        // Создать диалог и отправить первое сообщение (из деталей объявления)
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Start([FromBody] StartMessageDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            //if (userId == dto.SellerId)
            //    return BadRequest(new { error = "Cannot message yourself" });

            var convId = await _messageRepository.GetOrCreateConversationAsync(
                dto.ProductId, userId, dto.SellerId);

            if (!string.IsNullOrWhiteSpace(dto.Text))
                await _messageRepository.SendMessageAsync(convId, userId, dto.Text);

            return Ok(new { conversationId = convId });
        }

        // Получить счётчик непрочитанных (для polling fallback)
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var count = await _messageRepository.GetUnreadCountAsync(userId);
            return Json(new { count });
        }
    }

    public class StartMessageDto
    {
        public int ProductId { get; set; }
        public int SellerId { get; set; }
        public string? Text { get; set; }
    }
}