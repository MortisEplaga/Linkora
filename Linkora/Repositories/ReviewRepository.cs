using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly string _connectionString;

        public ReviewRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<List<ReviewRow>> GetUserReviewsAsync(int userId, string tab)
        {
            var isAbout = tab == "about";
            var whereField = isAbout ? "r.TargetUserId" : "r.AuthorId";
            var joinUserId = isAbout ? "r.AuthorId" : "r.TargetUserId";

            var result = new List<ReviewRow>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($@"
        SELECT r.Rating, r.Comment, r.CreatedAt,
               u.Id, u.UserName, u.AvatarUrl
        FROM Reviews r
        JOIN Users u ON u.Id = {joinUserId}
        WHERE {whereField} = @UserId
        ORDER BY r.CreatedAt DESC", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new ReviewRow
                {
                    Rating = r.GetInt32(0),
                    Comment = r.IsDBNull(1) ? "" : r.GetString(1),
                    CreatedAt = r.GetDateTime(2),
                    UserId = r.GetInt32(3),
                    UserName = r.IsDBNull(4) ? "Unknown" : r.GetString(4),
                    AvatarUrl = r.IsDBNull(5) ? null : r.GetString(5),
                });

            return result;
        }
    }
}