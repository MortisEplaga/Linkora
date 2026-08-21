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
            var nameEn = reader.GetString(reader.GetOrdinal("Name"));

            return new Category
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ParentId = reader.IsDBNull(reader.GetOrdinal("ParentId")) ? null : reader.GetInt32(reader.GetOrdinal("ParentId")),
                Name = Resolve(GetLang(), nameEn, reader.IsDBNull(reader.GetOrdinal("NameLV")) ? null : reader.GetString(reader.GetOrdinal("NameLV")), reader.IsDBNull(reader.GetOrdinal("NameRU")) ? null : reader.GetString(reader.GetOrdinal("NameRU"))),
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

            var parameters = await QueryAsync(
                $"SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE ParentId IN ({ids}) AND Type IN (2,3,4,5,6,7,8)",
                MapRow);

            return await LoadParameterOptionsAsync(parameters);
        }
        public async Task<List<Parameter>> GetParametersAsync(int categoryId)
        {
            var parameters = await QueryAsync(
                "SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE ParentId = @ParentId AND Type IN (2,3,4,5,6,7,8)",
                MapRow,
                p => p.AddWithValue("@ParentId", categoryId));

            return await LoadParameterOptionsAsync(parameters);
        }
        private async Task<List<Parameter>> LoadParameterOptionsAsync(List<Category> parameters)
        {
            var lang = GetLang();
            var result = new List<Parameter>();

            foreach (var p in parameters)
            {
                var vm = new Parameter { Param = p };

                if (p.Type == 2 || p.Type == 4 || p.Type == 8)
                {
                    vm.Options.AddRange(await QueryAsync<SelectOption>(
                        "SELECT Id, Value, ValueLV, ValueRU FROM SelectOptions WHERE CategoryId = @Id and IsConf = 1",
                        r => new SelectOption { Id = r.GetInt32(0), Text = Resolve(lang, r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3)) },
                        pr => pr.AddWithValue("@Id", p.Id)));
                }
                else if (p.Type == 5)
                {
                    await QueryAsync<Parameter>(
                        "SELECT MinValue, MaxValue, Step FROM ParameterRange WHERE ParamId = @Id",
                        r =>
                        {
                            vm.Min = r.IsDBNull(0) ? null : r.GetDecimal(0);
                            vm.Max = r.IsDBNull(1) ? null : r.GetDecimal(1);
                            vm.Step = r.IsDBNull(2) ? null : r.GetDecimal(2);
                            return vm;
                        },
                        pr => pr.AddWithValue("@Id", p.Id));
                }
                else if (p.Type == 6)
                {
                    vm.ColorOptions.AddRange(await QueryAsync<ColorOption>(
                        "SELECT Id, Name, NameLV, NameRU, HexValue FROM ColorOptions WHERE CategoryId = @Id AND IsConf = 1",
                        r => new ColorOption
                        {
                            Id = r.GetInt32(0),
                            Name = Resolve(lang, r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3)),
                            HexValue = r.GetString(4)
                        },
                        pr => pr.AddWithValue("@Id", p.Id)));
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