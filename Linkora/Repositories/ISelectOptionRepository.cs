using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public interface ISelectOptionRepository
    {
        Task<int> CreateAsync(int paramId, string text);
        Task<int> CreateAsync(SqlConnection conn, SqlTransaction tx, int paramId, string text);
        Task<int?> FindIdAsync(int paramId, string text, string lang, bool includeFilterOnly = false);
        Task<int?> FindIdAsync(SqlConnection conn, SqlTransaction tx, int paramId, string text, string lang);
        Task<List<(int Id, string Text)>> GetConfirmedAsync(int paramId, string lang, bool forFilter = false);
        Task<Dictionary<int, (string Value, string ValueLV, string ValueRU)>> GetConfirmedTextsAsync(IEnumerable<int> paramIds);
    }
}