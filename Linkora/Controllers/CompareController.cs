using Linkora.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Authorize]
    public class CompareController : Controller
    {
        private readonly string _connectionString;

        public CompareController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

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
                SELECT mpc.ProductId, c.Name, mpc.Value
                FROM MapperProductCategory mpc
                JOIN Category c ON c.Id = mpc.CategoryId
                WHERE mpc.ProductId IN ({productIds})
                  AND c.Name != 'Price'
                ORDER BY c.Name", conn);

            // paramMatrix[paramName][productId] = value
            var paramMatrix = new Dictionary<string, Dictionary<int, string>>();
            await using (var r = await paramCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var productId = r.GetInt32(0);
                    var paramName = r.GetString(1);
                    var value = r.IsDBNull(2) ? "" : r.GetString(2);

                    if (!paramMatrix.ContainsKey(paramName))
                        paramMatrix[paramName] = new Dictionary<int, string>();
                    paramMatrix[paramName][productId] = value;
                }
            }

            // Only params that exist for at least one product
            var allParams = paramMatrix.Keys.OrderBy(k => k).ToList();

            ViewBag.Products = products;
            ViewBag.AllParams = allParams;
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