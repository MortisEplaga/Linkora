using Linkora.Models;

namespace Linkora.Repositories
{
    public interface ICompareRepository
    {
        Task<CompareData> GetCompareDataAsync(int userId, string lang);
    }
}