using Linkora.Repositories;
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

        public ReviewsController(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
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
            return Ok(new { reviewId });
        }
    }

    public class CreateReviewDto
    {
        public int TargetUserId { get; set; }
        public int ProductId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}