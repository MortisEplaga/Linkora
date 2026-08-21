using Linkora.Models;
using Microsoft.Data.SqlClient;

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
        protected async Task<T?> QuerySingleAsync<T>(string sql, Func<SqlDataReader, T> map, Action<SqlParameterCollection>? bind = null) where T : class
        {
            var list = await QueryAsync(sql, map, bind);
            return list.FirstOrDefault();
        }
        protected async Task<int> ExecuteAsync(string sql, Action<SqlParameterCollection>? bind = null)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand(sql, conn);
            bind?.Invoke(cmd.Parameters);
            return await cmd.ExecuteNonQueryAsync();
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
            while (await reader.ReadAsync())
                items.Add(mapRow(reader));

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
            return await GetPagedDataAsync(conn, selectClause, fromWhereClause, orderByClause,
                page, pageSize, addParameters, mapRow);
        }
        protected static string Resolve(string lang, string en, string? lv, string? ru) => lang switch
        {
            "lv" => lv ?? en,
            "ru" => ru ?? en,
            _ => en
        };
    }
}