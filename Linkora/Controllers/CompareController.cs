using Linkora.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Authorize]
    public class CompareController : Controller
    {
        private readonly string _connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CompareController(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _httpContextAccessor = httpContextAccessor;
        }
        private string GetLang() => _httpContextAccessor.HttpContext?.Request.Cookies["lang"] ?? "en";
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var lang = GetLang();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT p.Id, p.Name, p.Address, p.CreatedTime,
                       COALESCE(
                           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
                            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                           p.AvatarImagePath
                       ) AS AvatarImagePath,
                       (SELECT COUNT(*) FROM ProductMedia pm2 WHERE pm2.ProductId = p.Id) AS MediaCount,
                       (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                        FROM MapperProductCategory m
                        JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                        WHERE m.ProductId = p.Id) AS Price,
                        cat.Name AS CategoryName, cat.NameLV AS CategoryNameLV, cat.NameRU AS CategoryNameRU,
                        u.UserName
                FROM Favourites f
                JOIN Products p ON p.Id = f.ProductId
                LEFT JOIN Category cat ON cat.Id = p.CategoryId
                LEFT JOIN Users u ON u.Id = p.UserId
                WHERE f.UserId = @U AND f.Can = 0
                ORDER BY f.Id", conn);
            cmd.Parameters.AddWithValue("@U", userId);

            var products = new List<CompareProduct>();
            await using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var catNameEn = r.IsDBNull(7) ? null : r.GetString(7);
                    var catNameLv = r.IsDBNull(8) ? catNameEn : r.GetString(8);
                    var catNameRu = r.IsDBNull(9) ? catNameEn : r.GetString(9);
                    var catName = lang switch
                    {
                        "lv" => catNameLv,
                        "ru" => catNameRu,
                        _ => catNameEn
                    };

                    products.Add(new CompareProduct
                    {
                        Id = r.GetInt32(0),
                        Name = r.IsDBNull(1) ? "" : r.GetString(1),
                        Address = r.IsDBNull(2) ? null : r.GetString(2),
                        CreatedTime = r.IsDBNull(3) ? null : r.GetDateTime(3),
                        AvatarImagePath = r.IsDBNull(4) ? null : r.GetString(4),
                        MediaCount = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                        Price = r.IsDBNull(6) ? null : r.GetDecimal(6),
                        CategoryName = catName,
                        SellerName = r.IsDBNull(10) ? null : r.GetString(10),
                    });
                }
            }

            if (!products.Any())
            {
                ViewBag.Products = products;
                ViewBag.AllParamIds = new List<int>();
                ViewBag.ParamLabels = new Dictionary<int, string>();
                ViewBag.ParamMatrix = new Dictionary<int, Dictionary<int, string>>();
                return View();
            }

            var selectOptionsDict = new Dictionary<int, (string Value, string ValueLV, string ValueRU)>();
            await using (var optCmd = new SqlCommand("SELECT Id, Value, ValueLV, ValueRU FROM SelectOptions WHERE IsConf = 1", conn))
            await using (var optR = await optCmd.ExecuteReaderAsync())
            {
                while (await optR.ReadAsync())
                {
                    var id = optR.GetInt32(0);
                    var value = optR.GetString(1);
                    var valueLv = optR.IsDBNull(2) ? value : optR.GetString(2);
                    var valueRu = optR.IsDBNull(3) ? value : optR.GetString(3);
                    selectOptionsDict[id] = (value, valueLv, valueRu);
                }
            }

            var productIds = string.Join(",", products.Select(p => p.Id));
            await using var paramCmd = new SqlCommand($@"
    SELECT mpc.ProductId, c.Id AS ParamId, c.Name, c.NameLV, c.NameRU, c.Type, mpc.Value,
           co.Name AS ColorName, co.NameLV AS ColorNameLV, co.NameRU AS ColorNameRU
    FROM MapperProductCategory mpc
    JOIN Category c ON c.Id = mpc.CategoryId
    LEFT JOIN ColorOptions co ON c.Type = 6 AND TRY_CAST(mpc.Value AS int) = co.Id
    WHERE mpc.ProductId IN ({productIds})
      AND c.Name != 'Price, €'
      AND c.Type IN (2,3,4,5,6,7,8)
    ORDER BY c.Name", conn);

            var paramLabels = new Dictionary<int, string>();
            var paramMatrix = new Dictionary<int, Dictionary<int, string>>();

            await using (var r = await paramCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var productId = r.GetInt32(0);
                    var paramId = r.GetInt32(1);
                    var nameEn = r.GetString(2);
                    var nameLv = r.IsDBNull(3) ? nameEn : r.GetString(3);
                    var nameRu = r.IsDBNull(4) ? nameEn : r.GetString(4);
                    var label = lang switch
                    {
                        "lv" => nameLv,
                        "ru" => nameRu,
                        _ => nameEn
                    };
                    paramLabels[paramId] = label;

                    var paramType = r.IsDBNull(5) ? (int?)null : r.GetInt32(5);
                    var rawValue = r.IsDBNull(6) ? "" : r.GetString(6);

                    string value;
                    if (paramType == 4)
                    {
                        var ids = rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        var texts = new List<string>();
                        foreach (var idStr in ids)
                        {
                            if (int.TryParse(idStr.Trim(), out int optId) && selectOptionsDict.TryGetValue(optId, out var textsTuple))
                            {
                                texts.Add(lang switch
                                {
                                    "lv" => textsTuple.ValueLV,
                                    "ru" => textsTuple.ValueRU,
                                    _ => textsTuple.Value
                                });
                            }
                            else
                            {
                                texts.Add(idStr);
                            }
                        }
                        value = string.Join(", ", texts);
                    }
                    else if (paramType == 2 || paramType == 8)
                    {
                        if (int.TryParse(rawValue, out int optId) && selectOptionsDict.TryGetValue(optId, out var textsTuple))
                        {
                            value = lang switch
                            {
                                "lv" => textsTuple.ValueLV,
                                "ru" => textsTuple.ValueRU,
                                _ => textsTuple.Value
                            };
                        }
                        else
                        {
                            value = rawValue;
                        }
                    }
                    else if (paramType == 6)
                    {
                        if (r.IsDBNull(7))
                        {
                            value = rawValue;
                        }
                        else
                        {
                            if (lang == "lv" && !r.IsDBNull(8)) value = r.GetString(8);
                            else if (lang == "ru" && !r.IsDBNull(9)) value = r.GetString(9);
                            else value = r.GetString(7);
                        }
                    }
                    else
                    {
                        value = rawValue;
                    }

                    if (!paramMatrix.ContainsKey(paramId))
                        paramMatrix[paramId] = new Dictionary<int, string>();
                    paramMatrix[paramId][productId] = value;
                }
            }

            var allParamIds = paramMatrix.Keys.OrderBy(id => paramLabels[id]).ToList();

            ViewBag.Products = products;
            ViewBag.AllParamIds = allParamIds;
            ViewBag.ParamLabels = paramLabels;
            ViewBag.ParamMatrix = paramMatrix;
            return View();
        }
    }
}
namespace Linkora.Models
{
    public class CompareProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Address { get; set; }
        public DateTime? CreatedTime { get; set; }
        public string? AvatarImagePath { get; set; }
        public int MediaCount { get; set; }
        public decimal? Price { get; set; }
        public string? CategoryName { get; set; }
        public string? SellerName { get; set; }
    }
}