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
        SELECT r.Rating, r.Comment, r.CreatedAt,
               u.Id, u.UserName, u.AvatarUrl
        FROM Reviews r
        JOIN Users u ON u.Id = {joinUserId}
        WHERE {whereField} = @UserId
        ORDER BY r.CreatedAt DESC",
                r => new ReviewRow
                {
                    Rating = r.GetInt32(0),
                    Comment = r.IsDBNull(1) ? "" : r.GetString(1),
                    CreatedAt = r.GetDateTime(2),
                    UserId = r.GetInt32(3),
                    UserName = r.IsDBNull(4) ? "Unknown" : r.GetString(4),
                    AvatarUrl = r.IsDBNull(5) ? null : r.GetString(5),
                },
                p => p.AddWithValue("@UserId", userId));
        }
    }
}