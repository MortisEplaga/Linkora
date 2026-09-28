namespace Linkora.Repositories
{
    public class UserStatsRepository(IConfiguration configuration) : SqlRepositoryBase(configuration), IUserStatsRepository
    {
        public async Task<(int Views, int Likes, int Carts)> GetTotalsAsync(int userId) => (await QueryAsync(
            @"SELECT
                (SELECT ISNULL(SUM(ISNULL(ViewCount, 0)), 0) FROM Products WHERE UserId = @U),
                (SELECT COUNT(*) FROM Favourites f JOIN Products p ON p.Id = f.ProductId WHERE p.UserId = @U AND f.Can = 1),
                (SELECT COUNT(*) FROM Favourites f JOIN Products p ON p.Id = f.ProductId WHERE p.UserId = @U AND f.Can = 0)",
            r => (Views: r.GetInt32(0), Likes: r.GetInt32(1), Carts: r.GetInt32(2)),
            p => p.AddWithValue("@U", userId))).FirstOrDefault();
    }
}