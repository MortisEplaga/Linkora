using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly string _connectionString;

        public ReportRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
        public async Task<List<ReportReason>> GetActiveReportReasonsAsync()
        {
            var result = new List<ReportReason>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, ReasonText, IsActive FROM ReportReasons WHERE IsActive = 1 ORDER BY ReasonText", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new ReportReason
                {
                    Id = reader.GetInt32(0),
                    ReasonText = reader.GetString(1),
                    IsActive = reader.GetBoolean(2)
                });
            }
            return result;
        }
        public async Task<Report> CreateReportAsync(int productId, int userId, int reportReasonId, string? comment)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // 1. Создаем жалобу
            var sql = @"
        INSERT INTO Reports (ProductId, UserId, ReportReasonId, Comment, CreatedAt, Status)
        OUTPUT INSERTED.Id
        VALUES (@ProductId, @UserId, @ReportReasonId, @Comment, @CreatedAt, @Status)";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ProductId", productId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@ReportReasonId", reportReasonId); // Новый параметр
            command.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            command.Parameters.AddWithValue("@Status", ReportStatus.Pending.ToString());

            var id = (int)await command.ExecuteScalarAsync();

            // 2. Обновляем статус продукта на Moderation
            var updateProductSql = @"
        UPDATE Products 
        SET Status = 'Moderation' 
        WHERE Id = @ProductId AND Status != 'Moderation'";

            await using var updateCommand = new SqlCommand(updateProductSql, connection);
            updateCommand.Parameters.AddWithValue("@ProductId", productId);
            await updateCommand.ExecuteNonQueryAsync();

            // Возвращаем созданный отчет
            return new Report
            {
                Id = id,
                ProductId = productId,
                UserId = userId,
                ReportReasonId = reportReasonId,
                Comment = comment,
                CreatedAt = DateTime.Now,
                Status = ReportStatus.Pending
            };
        }
        public async Task<IEnumerable<Report>> GetReportsByProductIdAsync(int productId)
        {
            var reports = new List<Report>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT * FROM Reports WHERE ProductId = @ProductId ORDER BY CreatedAt DESC";
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ProductId", productId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                reports.Add(MapReport(reader));
            }
            return reports;
        }

        public async Task<IEnumerable<Report>> GetPendingReportsAsync()
        {
            var reports = new List<Report>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT * FROM Reports WHERE Status = 'Pending' ORDER BY CreatedAt ASC";
            using var command = new SqlCommand(sql, connection);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                reports.Add(MapReport(reader));
            }
            return reports;
        }

        public async Task UpdateReportStatusAsync(int reportId, ReportStatus status)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "UPDATE Reports SET Status = @Status WHERE Id = @Id";
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Status", status.ToString());
            command.Parameters.AddWithValue("@Id", reportId);
            await command.ExecuteNonQueryAsync();
        }

        private Report MapReport(SqlDataReader reader)
        {
            return new Report
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                ReportReasonId = reader.GetInt32(reader.GetOrdinal("ReportReasonId")),
                Comment = reader.IsDBNull(reader.GetOrdinal("Comment")) ? null : reader.GetString(reader.GetOrdinal("Comment")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                Status = Enum.Parse<ReportStatus>(reader.GetString(reader.GetOrdinal("Status")))
            };
        }
    }
}