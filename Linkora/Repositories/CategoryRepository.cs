using Linkora.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly string _connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CategoryRepository(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _httpContextAccessor = httpContextAccessor;
        }
        private string GetLang() =>
    _httpContextAccessor.HttpContext?.Request.Cookies["lang"] ?? "en";

        // ── Вспомогательный метод: читает одну строку → Category ──
        private Category MapRow(SqlDataReader reader)
        {
            var lang = GetLang();
            var nameEn = reader.GetString(reader.GetOrdinal("Name"));
            string name;
            if (lang == "lv")
            {
                var ord = reader.GetOrdinal("NameLV");
                name = !reader.IsDBNull(ord) ? reader.GetString(ord) : nameEn;
            }
            else
            {
                name = nameEn;
            }

            return new Category
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ParentId = reader.IsDBNull(reader.GetOrdinal("ParentId")) ? null : reader.GetInt32(reader.GetOrdinal("ParentId")),
                Name = name,
                Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? null : reader.GetInt32(reader.GetOrdinal("Type")),
            };
        }

        // ── Все категории ──
        public async Task<List<Category>> GetAllAsync()
        {
            var result = new List<Category>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("SELECT Id, ParentId, Name, Type, NameLV FROM Category", conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                result.Add(MapRow(reader));

            return result;
        }

        // ── Одна категория по Id ──
        public async Task<Category?> GetByIdAsync(int id)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                "SELECT Id, ParentId, Name, Type, NameLV FROM Category WHERE Id = @Id and Type = 1", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await using var reader = await cmd.ExecuteReaderAsync();

            return await reader.ReadAsync() ? MapRow(reader) : null;
        }

        // ── Дочерние категории ──
        public async Task<List<Category>> GetChildrenAsync(int parentId)
        {
            var result = new List<Category>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                "SELECT Id, ParentId, Name, Type, NameLV FROM Category WHERE ParentId = @ParentId and Type = 1", conn);
            cmd.Parameters.AddWithValue("@ParentId", parentId);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                result.Add(MapRow(reader));

            return result;
        }

        // ── Хлебные крошки: идём вверх по ParentId до корня ──
        public async Task<List<Category>> GetBreadcrumbAsync(int categoryId)
        {
            var breadcrumb = new List<Category>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var currentId = (int?)categoryId;

            while (currentId != null)
            {
                await using var cmd = new SqlCommand(
                    "SELECT Id, ParentId, Name, Type, NameLV FROM Category WHERE Id = @Id and Type = 1", conn);
                cmd.Parameters.AddWithValue("@Id", currentId.Value);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync()) break;

                var category = MapRow(reader);
                breadcrumb.Insert(0, category); // вставляем в начало — чтобы порядок был от корня
                currentId = category.ParentId;
            }

            return breadcrumb;
        }
        // ── Параметры категории (Type 2–5) + их данные ──
        public async Task<List<Parameter>> GetParametersAsync(IEnumerable<int> categoryIds)
        {

            // 1. Получаем параметры
            var result = new List<Parameter>();
            var ids = string.Join(",", categoryIds);
            if (string.IsNullOrEmpty(ids)) return result;
            var lang = GetLang();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmdP = new SqlCommand(
                $"SELECT Id, ParentId, Name, Type, NameLV FROM Category WHERE ParentId IN ({ids}) AND Type IN (2,3,4,5)", conn);

            var parameters = new List<Category>();
            await using (var r = await cmdP.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    parameters.Add(MapRow(r));

            // 2. Для каждого параметра — подгружаем варианты или диапазон
            foreach (var p in parameters)
            {
                var vm = new Parameter { Param = p };

                if (p.Type == 2 || p.Type == 4) // selection / multi → SelectOptions
                {
                    await using var cmdO = new SqlCommand(
                        "SELECT Value, ValueLV FROM SelectOptions WHERE CategoryId = @Id", conn);
                    cmdO.Parameters.AddWithValue("@Id", p.Id);
                    await using var ro = await cmdO.ExecuteReaderAsync();
                    while (await ro.ReadAsync())
                    {
                        if (lang == "lv" && !ro.IsDBNull(1))
                            vm.Options.Add(ro.GetString(1));
                        else
                            vm.Options.Add(ro.GetString(0));
                    }
                }
                else if (p.Type == 5) // range → ParameterRange
                {
                    await using var cmdR = new SqlCommand(
                        "SELECT MinValue, MaxValue, Step FROM ParameterRange WHERE ParamId = @Id", conn);
                    cmdR.Parameters.AddWithValue("@Id", p.Id);
                    await using var rr = await cmdR.ExecuteReaderAsync();
                    if (await rr.ReadAsync())
                    {
                        vm.Min = rr.IsDBNull(0) ? null : rr.GetDecimal(0);
                        vm.Max = rr.IsDBNull(1) ? null : rr.GetDecimal(1);
                        vm.Step = rr.IsDBNull(2) ? null : rr.GetDecimal(2);
                    }
                }

                result.Add(vm);
            }

            return result;
        }

        public async Task<List<Parameter>> GetParametersAsync(int categoryId)
        {
            var result = new List<Parameter>();
            var lang = GetLang();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();


            await using var cmdP = new SqlCommand(
            "SELECT Id, ParentId, Name, Type, NameLV FROM Category WHERE ParentId = @ParentId AND Type IN (2,3,4,5)", conn);
            cmdP.Parameters.AddWithValue("@ParentId", categoryId);
            var parameters = new List<Category>();
            await using (var r = await cmdP.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    parameters.Add(MapRow(r));

            // 2. Для каждого параметра — подгружаем варианты или диапазон
            foreach (var p in parameters)
            {
                var vm = new Parameter { Param = p };

                if (p.Type == 2 || p.Type == 4) // selection / multi → SelectOptions
                {
                    await using var cmdO = new SqlCommand(
                        "SELECT Value, ValueLV FROM SelectOptions WHERE CategoryId = @Id", conn);
                    cmdO.Parameters.AddWithValue("@Id", p.Id);
                    await using var ro = await cmdO.ExecuteReaderAsync();
                    while (await ro.ReadAsync())
                    {
                        if (lang == "lv" && !ro.IsDBNull(1))
                            vm.Options.Add(ro.GetString(1));
                        else
                            vm.Options.Add(ro.GetString(0));
                    }
                }
                else if (p.Type == 5) // range → ParameterRange
                {
                    await using var cmdR = new SqlCommand(
                        "SELECT MinValue, MaxValue, Step FROM ParameterRange WHERE ParamId = @Id", conn);
                    cmdR.Parameters.AddWithValue("@Id", p.Id);
                    await using var rr = await cmdR.ExecuteReaderAsync();
                    if (await rr.ReadAsync())
                    {
                        vm.Min = rr.IsDBNull(0) ? null : rr.GetDecimal(0);
                        vm.Max = rr.IsDBNull(1) ? null : rr.GetDecimal(1);
                        vm.Step = rr.IsDBNull(2) ? null : rr.GetDecimal(2);
                    }
                }

                result.Add(vm);
            }

            return result;
        }
        public async Task<List<int>> GetDescendantIdsAsync(int categoryId)
        {
            var ids = new List<int>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
        WITH cte AS (
            SELECT Id FROM Category WHERE Id = @Id
            UNION ALL
            SELECT c.Id FROM Category c
            INNER JOIN cte ON c.ParentId = cte.Id
        )
        SELECT Id FROM cte", conn);
            cmd.Parameters.AddWithValue("@Id", categoryId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                ids.Add(r.GetInt32(0));
            return ids;
        }
    }
}
