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
        private readonly string _connectionString;

        public ReviewsController(IMessageRepository messageRepository, IConfiguration configuration)
        {
            _messageRepository = messageRepository;
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
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
        [HttpGet("My")]
        public async Task<IActionResult> My(string tab = "about")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var isAbout = tab == "about";
            var whereField = isAbout ? "r.TargetUserId" : "r.AuthorId";
            var joinUserId = isAbout ? "r.AuthorId" : "r.TargetUserId";

            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand($@"
        SELECT r.Rating, r.Comment, r.CreatedAt,
               u.Id, u.UserName, u.AvatarImagePath
        FROM Reviews r
        JOIN Users u ON u.Id = {joinUserId}
        WHERE {whereField} = @UserId
        ORDER BY r.CreatedAt DESC", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            var result = new List<object>();
            while (await r.ReadAsync())
                result.Add(new
                {
                    rating = r.GetInt32(0),
                    comment = r.IsDBNull(1) ? "" : r.GetString(1),
                    createdAt = r.GetDateTime(2).ToString("dd.MM.yyyy"),
                    userId = r.GetInt32(3),
                    userName = r.IsDBNull(4) ? "Unknown" : r.GetString(4),
                    avatarPath = r.IsDBNull(5) ? null : (object)r.GetString(5),
                });

            return Ok(result);
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