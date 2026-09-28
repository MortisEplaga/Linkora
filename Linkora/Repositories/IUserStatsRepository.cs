namespace Linkora.Repositories
{
    public interface IUserStatsRepository
    {
        Task<(int Views, int Likes, int Carts)> GetTotalsAsync(int userId);
    }
}