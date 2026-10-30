using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IParcelLockerRepository
    {
        Task<ParcelLocker?> GetByIdAsync(int id);
        Task<List<ParcelLocker>> SearchAsync(string country, string? query, int limit = 50);
        Task<int> UpsertAsync(string provider, IReadOnlyCollection<ParcelLocker> lockers);
    }
}