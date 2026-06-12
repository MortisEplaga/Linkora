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

            // Fetch cart products with basic info
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
                        JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
                        WHERE m.ProductId = p.Id) AS Price,
                       cat.Name AS CategoryName, u.UserName
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
                    products.Add(new CompareProduct
                    {
                        Id = r.GetInt32(0),
                        Name = r.IsDBNull(1) ? "" : r.GetString(1),
                        Address = r.IsDBNull(2) ? null : r.GetString(2),
                        CreatedTime = r.IsDBNull(3) ? null : r.GetDateTime(3),
                        AvatarImagePath = r.IsDBNull(4) ? null : r.GetString(4),
                        MediaCount = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                        Price = r.IsDBNull(6) ? null : r.GetDecimal(6),
                        CategoryName = r.IsDBNull(7) ? null : r.GetString(7),
                        SellerName = r.IsDBNull(8) ? null : r.GetString(8),
                    });
                }
            }

            if (!products.Any())
            {
                ViewBag.Products = products;
                ViewBag.AllParams = new List<string>();
                ViewBag.ParamMatrix = new Dictionary<string, Dictionary<int, string>>();
                return View();
            }

            // Fetch all parameters for these products
            var productIds = string.Join(",", products.Select(p => p.Id));
            await using var paramCmd = new SqlCommand($@"
    SELECT mpc.ProductId, c.Id AS ParamId, c.Name, c.Type, mpc.Value,
           so.Value AS OptText, so.ValueLV, so.ValueRU
    FROM MapperProductCategory mpc
    JOIN Category c ON c.Id = mpc.CategoryId
    LEFT JOIN SelectOptions so ON c.Type IN (2,4) AND TRY_CAST(mpc.Value AS int) = so.Id
    WHERE mpc.ProductId IN ({productIds})
      AND c.Name != 'Price'
    ORDER BY c.Name", conn);

            // paramMatrix[paramName][productId] = value
            var paramMatrix = new Dictionary<string, Dictionary<int, string>>();
            var multiParts = new Dictionary<(string ParamName, int ProductId), List<string>>();
            await using (var r = await paramCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var productId = r.GetInt32(0);
                    var paramName = r.GetString(2);
                    var paramType = r.IsDBNull(3) ? (int?)null : r.GetInt32(3);
                    var rawValue = r.IsDBNull(4) ? "" : r.GetString(4);

                    string value;
                    if (paramType is 2 or 4)
                    {
                        value = ResolveOptionText(r, lang, rawValue);
                    }
                    else
                    {
                        value = rawValue;
                    }

                    if (paramType == 4)
                    {
                        var key = (paramName, productId);
                        if (!multiParts.ContainsKey(key)) multiParts[key] = new();
                        multiParts[key].Add(value);
                    }
                    else
                    {
                        if (!paramMatrix.ContainsKey(paramName))
                            paramMatrix[paramName] = new Dictionary<int, string>();
                        paramMatrix[paramName][productId] = value;
                    }
                }
            }

            foreach (var ((paramName, productId), parts) in multiParts)
            {
                if (!paramMatrix.ContainsKey(paramName))
                    paramMatrix[paramName] = new Dictionary<int, string>();
                paramMatrix[paramName][productId] = string.Join(", ", parts);
            }
            // Only params that exist for at least one product
            var allParams = paramMatrix.Keys.OrderBy(k => k).ToList();

            ViewBag.Products = products;
            ViewBag.AllParams = allParams;
            ViewBag.ParamMatrix = paramMatrix;
            return View();
        }
        private static string ResolveOptionText(SqlDataReader r, string lang, string fallback)
        {
            if (r.IsDBNull(5)) return fallback; 
            if (lang == "lv" && !r.IsDBNull(6)) return r.GetString(6);
            if (lang == "ru" && !r.IsDBNull(7)) return r.GetString(7);
            return r.GetString(5);
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