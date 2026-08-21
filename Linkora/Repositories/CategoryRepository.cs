using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class CategoryRepository : SqlRepositoryBase, ICategoryRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CategoryRepository(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) : base(configuration)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        private string GetLang() => _httpContextAccessor.HttpContext?.Request.Cookies["lang"] ?? "en";
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
            else if (lang == "ru")
            {
                var ord = reader.GetOrdinal("NameRU");
                name = !reader.IsDBNull(ord) ? reader.GetString(ord) : nameEn;
            }
            else
                name = nameEn;

            return new Category
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ParentId = reader.IsDBNull(reader.GetOrdinal("ParentId")) ? null : reader.GetInt32(reader.GetOrdinal("ParentId")),
                Name = name,
                NameEn = nameEn,
                Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? null : reader.GetInt32(reader.GetOrdinal("Type")),
            };
        }
        public async Task<List<Category>> GetAllAsync()
        {
            return await QueryAsync(
                "SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category",
                MapRow);
        }
        public async Task<Category?> GetByIdAsync(int id)
        {
            return await QuerySingleAsync(
                "SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE Id = @Id and Type = 1",
                MapRow,
                p => p.AddWithValue("@Id", id));
        }
        public async Task<List<Category>> GetChildrenAsync(int parentId)
        {
            return await QueryAsync(
                "SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE ParentId = @ParentId and Type = 1",
                MapRow,
                p => p.AddWithValue("@ParentId", parentId));
        }
        public async Task<List<Category>> GetBreadcrumbAsync(int categoryId)
        {
            var breadcrumb = await QueryAsync(
                @"WITH cte AS (
                    SELECT Id, ParentId, Name, Type, NameLV, NameRU
                    FROM Category
                    WHERE Id = @Id AND Type = 1
                    UNION ALL
                    SELECT c.Id, c.ParentId, c.Name, c.Type, c.NameLV, c.NameRU
                    FROM Category c
                    INNER JOIN cte ON c.Id = cte.ParentId
                    WHERE c.Type = 1
                )
                SELECT Id, ParentId, Name, Type, NameLV, NameRU
                FROM cte",
                MapRow,
                p => p.AddWithValue("@Id", categoryId));

            breadcrumb.Reverse();
            return breadcrumb;
        }
        public async Task<List<Parameter>> GetParametersAsync(IEnumerable<int> categoryIds)
        {
            var ids = string.Join(",", categoryIds);
            if (string.IsNullOrEmpty(ids)) return new List<Parameter>();

            await using var conn = await OpenConnectionAsync();
            var lang = GetLang();

            await using var cmdP = new SqlCommand(
                $"SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE ParentId IN ({ids}) AND Type IN (2,3,4,5,6,7,8)", conn);

            var parameters = new List<Category>();
            await using (var r = await cmdP.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    parameters.Add(MapRow(r));

            return await LoadParameterOptionsAsync(parameters, conn, lang);
        }
        public async Task<List<Parameter>> GetParametersAsync(int categoryId)
        {
            await using var conn = await OpenConnectionAsync();
            var lang = GetLang();

            await using var cmdP = new SqlCommand(
                "SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE ParentId = @ParentId AND Type IN (2,3,4,5,6,7,8)", conn);
            cmdP.Parameters.AddWithValue("@ParentId", categoryId);

            var parameters = new List<Category>();
            await using (var r = await cmdP.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    parameters.Add(MapRow(r));

            return await LoadParameterOptionsAsync(parameters, conn, lang);
        }
        private async Task<List<Parameter>> LoadParameterOptionsAsync(List<Category> parameters, SqlConnection conn, string lang)
        {
            var result = new List<Parameter>();

            foreach (var p in parameters)
            {
                var vm = new Parameter { Param = p };

                if (p.Type == 2 || p.Type == 4 || p.Type == 8)
                {
                    await using var cmdO = new SqlCommand(
                        "SELECT Id, Value, ValueLV, ValueRU FROM SelectOptions WHERE CategoryId = @Id and IsConf = 1", conn);
                    cmdO.Parameters.AddWithValue("@Id", p.Id);
                    await using var ro = await cmdO.ExecuteReaderAsync();
                    while (await ro.ReadAsync())
                    {
                        string optionText;
                        if (lang == "lv" && !ro.IsDBNull(2))
                            optionText = ro.GetString(2);
                        else if (lang == "ru" && !ro.IsDBNull(3))
                            optionText = ro.GetString(3);
                        else
                            optionText = ro.GetString(1);

                        vm.Options.Add(new SelectOption { Id = ro.GetInt32(0), Text = optionText });
                    }
                }
                else if (p.Type == 5)
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
                else if (p.Type == 6)
                {
                    await using var cmdC = new SqlCommand(
                        "SELECT Id, Name, NameLV, NameRU, HexValue FROM ColorOptions WHERE CategoryId = @Id AND IsConf = 1", conn);
                    cmdC.Parameters.AddWithValue("@Id", p.Id);
                    await using var rc = await cmdC.ExecuteReaderAsync();
                    while (await rc.ReadAsync())
                    {
                        string name;
                        if (lang == "lv" && !rc.IsDBNull(2)) name = rc.GetString(2);
                        else if (lang == "ru" && !rc.IsDBNull(3)) name = rc.GetString(3);
                        else name = rc.GetString(1);

                        vm.ColorOptions.Add(new ColorOption
                        {
                            Id = rc.GetInt32(0),
                            Name = name,
                            HexValue = rc.GetString(4)
                        });
                    }
                }
                result.Add(vm);
            }

            return result;
        }
        public async Task<List<int>> GetDescendantIdsAsync(int categoryId)
        {
            return await QueryAsync(
                @"WITH cte AS (
                    SELECT Id FROM Category WHERE Id = @Id
                    UNION ALL
                    SELECT c.Id FROM Category c
                    INNER JOIN cte ON c.ParentId = cte.Id
                )
                SELECT Id FROM cte",
                r => r.GetInt32(0),
                p => p.AddWithValue("@Id", categoryId));
        }
    }
}