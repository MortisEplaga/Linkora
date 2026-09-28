using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public interface ISelectOptionRepository
    {
        Task<int?> FindIdAsync(int paramId, string text, string lang);
        Task<int> CreateAsync(int paramId, string text);
        /// <summary>Варианты для выполнения внутри внешней транзакции (массовый импорт).</summary>
        Task<int?> FindIdAsync(SqlConnection conn, SqlTransaction tx, int paramId, string text, string lang);
        Task<int> CreateAsync(SqlConnection conn, SqlTransaction tx, int paramId, string text);
        Task<List<(int Id, string Text)>> GetConfirmedAsync(int paramId, string lang);
    }
}