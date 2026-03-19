using Linkora.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Linkora.Controllers
{
    public class FavouriteController : Controller
    {
        private readonly string _connectionString;

        public FavouriteController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Toggle(int productId, bool can)
        {
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null) return Unauthorized();

                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Проверяем есть ли уже запись
                await using var check = new SqlCommand(
                    "SELECT Id FROM Favourites WHERE ProductId = @P AND UserId = @U AND Can = @C", conn);
                check.Parameters.AddWithValue("@P", productId);
                check.Parameters.AddWithValue("@U", int.Parse(userId));
                check.Parameters.AddWithValue("@C", can);
                var existing = await check.ExecuteScalarAsync();

                if (existing != null)
                {
                    // Уже есть — удаляем
                    await using var del = new SqlCommand(
                        "DELETE FROM Favourites WHERE Id = @Id", conn);
                    del.Parameters.AddWithValue("@Id", existing);
                    await del.ExecuteNonQueryAsync();
                    return Json(new { active = false });
                }
                else
                {
                    // Нет — добавляем
                    await using var ins = new SqlCommand(
                        "INSERT INTO Favourites (ProductId, UserId, Can) VALUES (@P, @U, @C)", conn);
                    ins.Parameters.AddWithValue("@P", productId);
                    ins.Parameters.AddWithValue("@U", int.Parse(userId));
                    ins.Parameters.AddWithValue("@C", can);
                    await ins.ExecuteNonQueryAsync();
                    return Json(new { active = true });
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> UserItems()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Json(new { favs = Array.Empty<int>(), cart = Array.Empty<int>() });

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT ProductId, Can FROM Favourites WHERE UserId = @U", conn);
            cmd.Parameters.AddWithValue("@U", int.Parse(userId));
            await using var r = await cmd.ExecuteReaderAsync();

            var favs = new List<int>();
            var cart = new List<int>();
            while (await r.ReadAsync())
            {
                if (r.GetBoolean(1)) favs.Add(r.GetInt32(0));
                else cart.Add(r.GetInt32(0));
            }
            return Json(new { favs, cart });
        }
        [HttpGet]
        public async Task<IActionResult> Index(string tab = "favs")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return RedirectToAction("Login", "Account");

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
    SELECT f.Can, p.Id, p.Name,
           (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
            FROM MapperProductCategory m
            JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
            WHERE m.ProductId = p.Id) as Price,
           p.Address, p.CreatedTime,
           COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarImagePath
       ) AS AvatarImagePath, u.UserName, u.AvatarImagePath, u.IsCompany
    FROM Favourites f
    JOIN Products p ON p.Id = f.ProductId
    LEFT JOIN Users u ON u.Id = p.UserId
    WHERE f.UserId = @U", conn);
            cmd.Parameters.AddWithValue("@U", int.Parse(userId));
            await using var r = await cmd.ExecuteReaderAsync();

            var favs = new List<Product>();
            var cart = new List<Product>();
            while (await r.ReadAsync())
            {
                var p = new Product
                {
                    Id = r.GetInt32(1),
                    Name = r.IsDBNull(2) ? "" : r.GetString(2),
                    Price = r.IsDBNull(3) ? null : r.GetDecimal(3),
                    Address = r.IsDBNull(4) ? null : r.GetString(4),
                    CreatedTime = r.IsDBNull(5) ? null : r.GetDateTime(5),
                    AvatarImagePath = r.IsDBNull(6) ? null : r.GetString(6),
                    Seller = new SellerViewModel
                    {
                        UserName = r.IsDBNull(7) ? null : r.GetString(7),
                        AvatarPath = r.IsDBNull(8) ? null : r.GetString(8),
                        IsCompany = !r.IsDBNull(9) && r.GetBoolean(9),
                    }
                };
                if (r.GetBoolean(0)) favs.Add(p);
                else cart.Add(p);
            }

            ViewBag.Favs = favs;
            ViewBag.Cart = cart;
            ViewBag.Tab = tab;
            return View();
        }
    }
}