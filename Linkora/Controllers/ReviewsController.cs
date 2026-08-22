using Linkora.Models;
using Linkora.Repositories;
using Linkora.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly IMessageRepository _messageRepository;
        private readonly INotificationService _notifications;
        private readonly IReviewRepository _reviewRepository;

        public ReviewsController(IMessageRepository messageRepository,
            INotificationService notifications, IReviewRepository reviewRepository)
        {
            _messageRepository = messageRepository;
            _notifications = notifications;
            _reviewRepository = reviewRepository;
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var reviewId = await _messageRepository.CreateReviewAsync(
                authorId: userId,
                targetUserId: dto.TargetUserId,
                productId: dto.ProductId,
                rating: dto.Rating,
                comment: dto.Comment
            );
            var msg = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "review_received",
                rating = dto.Rating,
                reviewId
            });
            await _notifications.CreateAsync(dto.TargetUserId, userId, dto.ProductId, msg);

            return Ok(new { reviewId });
        }

        [HttpGet("CanReview")]
        public async Task<IActionResult> CanReview(int targetUserId, int productId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var canReview = await _reviewRepository.CanReviewAsync(userId, targetUserId, productId);
            return Ok(new { canReview });
        }
        [HttpGet("My")]
        public async Task<IActionResult> My(string tab = "about")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var reviews = await _reviewRepository.GetUserReviewsAsync(userId, tab);

            var result = reviews.Select(r => new
            {
                rating = r.Rating,
                comment = r.Comment,
                createdAt = r.CreatedAt.ToString("dd.MM.yyyy"),
                userId = r.UserId,
                userName = r.UserName,
                avatarUrl = (object?)r.AvatarUrl,
            });

            return Ok(result);
        }
    }
}