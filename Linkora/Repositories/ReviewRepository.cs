using Linkora.Models;

namespace Linkora.Repositories
{
    public class ReviewRepository : SqlRepositoryBase, IReviewRepository
    {
        public ReviewRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<List<ReviewRow>> GetUserReviewsAsync(int userId, string tab)
        {
            var isAbout = tab == "about";
            var whereField = isAbout ? "r.TargetUserId" : "r.AuthorId";
            var joinUserId = isAbout ? "r.AuthorId" : "r.TargetUserId";

            return await QueryAsync($@"
                SELECT r.Rating, r.Comment, r.CreatedAt, u.Id, u.UserName, u.AvatarUrl FROM Reviews r
                JOIN Users u ON u.Id = {joinUserId} WHERE {whereField} = @UserId ORDER BY r.CreatedAt DESC",
                r => new ReviewRow
                {
                    Rating = r.GetInt32(0),
                    Comment = r.GetStringOrDefault(1),
                    CreatedAt = r.GetDateTime(2),
                    UserId = r.GetInt32(3),
                    UserName = r.GetStringOrDefault(4, "Unknown"),
                    AvatarUrl = r.GetStringOrNull(5),
                },
                p => p.AddWithValue("@UserId", userId));
        }
        public async Task<bool> CanReviewAsync(int authorId, int targetUserId, int productId) => (await QueryAsync<int>(@"
                    SELECT COUNT(*) FROM Reviews
                    WHERE AuthorId = @AuthorId AND TargetUserId = @TargetId AND ProductId = @ProductId",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@AuthorId", authorId);
                    p.AddWithValue("@TargetId", targetUserId);
                    p.AddWithValue("@ProductId", productId);
                })).FirstOrDefault() == 0;
    }
}