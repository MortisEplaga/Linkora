using Linkora.Models;
using Microsoft.Data.SqlClient;
using System.Text;

namespace Linkora.Repositories
{
    public abstract class SqlRepositoryBase
    {
        protected readonly string ConnectionString;
        protected SqlRepositoryBase(IConfiguration config) => ConnectionString = config.GetConnectionString("DefaultConnection")!;
        protected async Task<SqlConnection> OpenConnectionAsync()
        {
            var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            return conn;
        }
        protected async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, Action<SqlParameterCollection>? bind = null)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand(sql, conn);
            bind?.Invoke(cmd.Parameters);
            await using var r = await cmd.ExecuteReaderAsync();
            var result = new List<T>();
            while (await r.ReadAsync()) result.Add(map(r));
            return result;
        }
        protected async Task<T?> QuerySingleAsync<T>(string sql, Func<SqlDataReader, T> map, Action<SqlParameterCollection>? bind = null) where T : class => (await QueryAsync(sql, map, bind)).FirstOrDefault();
        protected async Task<int> ExecuteAsync(string sql, Action<SqlParameterCollection>? bind = null)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand(sql, conn);
            bind?.Invoke(cmd.Parameters);
            return await cmd.ExecuteNonQueryAsync();
        }
        protected async Task ExecuteInTransactionAsync(Func<SqlConnection, SqlTransaction, Task> action)
        {
            await using var conn = await OpenConnectionAsync();
            await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                await action(conn, transaction);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        protected async Task<T> ExecuteInTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> action)
        {
            await using var conn = await OpenConnectionAsync();
            await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                var result = await action(conn, transaction);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        protected static async Task ExecuteBatchInsertAsync(SqlConnection conn, SqlTransaction tx, string table, string[] columns, IEnumerable<object?[]> rows)
        {
            var rowList = rows as IList<object?[]> ?? rows.ToList();
            if (rowList.Count == 0) return;

            var batchSize = Math.Max(1, 2000 / columns.Length);

            for (int offset = 0; offset < rowList.Count; offset += batchSize)
            {
                var count = Math.Min(batchSize, rowList.Count - offset);
                var sb = new StringBuilder();
                sb.Append("INSERT INTO ").Append(table).Append(" (")
                  .Append(string.Join(",", columns)).Append(") VALUES ");

                await using var cmd = new SqlCommand { Connection = conn, Transaction = tx };

                for (int i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('(');
                    var row = rowList[offset + i];
                    for (int j = 0; j < columns.Length; j++)
                    {
                        if (j > 0) sb.Append(',');
                        var paramName = $"@r{i}_{j}";
                        sb.Append(paramName);
                        cmd.Parameters.AddWithValue(paramName, row[j] ?? DBNull.Value);
                    }
                    sb.Append(')');
                }

                cmd.CommandText = sb.ToString();
                await cmd.ExecuteNonQueryAsync();
            }
        }
        protected static (string Sql, List<SqlParameter> Parameters) BuildInClause(IEnumerable<int> values, string prefix)
        {
            var list = values.ToList();
            var names = new string[list.Count];
            var parameters = new List<SqlParameter>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var name = $"{prefix}{i}";
                names[i] = name;
                parameters.Add(new SqlParameter(name, list[i]));
            }
            return (string.Join(",", names), parameters);
        }
        protected async Task<PagedResult<T>> GetPagedDataAsync<T>(SqlConnection conn, string selectClause, string fromWhereClause,
                                                                  string orderByClause, int page, int pageSize,
                                                                  Action<SqlParameterCollection>? addParameters, Func<SqlDataReader, T> mapRow)
        {
            var offset = (page - 1) * pageSize;

            await using var countCmd = new SqlCommand($"SELECT COUNT(*) {fromWhereClause}", conn);
            addParameters?.Invoke(countCmd.Parameters);
            var total = (int)(await countCmd.ExecuteScalarAsync())!;

            await using var dataCmd = new SqlCommand($@"
                {selectClause}
                {fromWhereClause}
                {orderByClause}
                OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY", conn);
            addParameters?.Invoke(dataCmd.Parameters);

            var items = new List<T>();
            await using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) items.Add(mapRow(reader));

            return new PagedResult<T>
            {
                Items = items,
                Total = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page
            };
        }
        protected async Task<PagedResult<T>> GetPagedDataAsync<T>(string selectClause, string fromWhereClause, string orderByClause, int page,
                                                                  int pageSize, Action<SqlParameterCollection>? addParameters, Func<SqlDataReader, T> mapRow)
        {
            await using var conn = await OpenConnectionAsync();
            return await GetPagedDataAsync(conn, selectClause, fromWhereClause, orderByClause, page, pageSize, addParameters, mapRow);
        }
        public static string Resolve(string lang, string en, string? lv, string? ru) => lang switch
        {
            "lv" => lv ?? en,
            "ru" => ru ?? en,
            _ => en
        };
    }
}