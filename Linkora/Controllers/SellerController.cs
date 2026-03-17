using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Linkora.Controllers
{
    public class SellerController : Controller
    {
        private readonly string _connectionString;
        private readonly ICategoryRepository _categoryRepository;

        public SellerController(IConfiguration configuration, ICategoryRepository categoryRepository)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _categoryRepository = categoryRepository;
        }

        public async Task<IActionResult> Index(int id, int? categoryId, string sort = "new")
        {
            // Загружаем продавца
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var userCmd = new SqlCommand(
                "SELECT Id, UserName, AvatarImagePath, PhoneNumber, Email, IsCompany, CreatedAt FROM Users WHERE Id = @Id", conn);
            userCmd.Parameters.AddWithValue("@Id", id);
            await using var ur = await userCmd.ExecuteReaderAsync();
            if (!await ur.ReadAsync()) return NotFound();

            var seller = new SellerViewModel
            {
                Id = ur.GetInt32(0),
                UserName = ur.IsDBNull(1) ? null : ur.GetString(1),
                AvatarPath = ur.IsDBNull(2) ? null : ur.GetString(2),
                PhoneNumber = ur.IsDBNull(3) ? null : ur.GetString(3),
                Email = ur.IsDBNull(4) ? null : ur.GetString(4),
                IsCompany = !ur.IsDBNull(5) && ur.GetBoolean(5),
                CreatedAt = ur.IsDBNull(6) ? null : ur.GetDateTime(6),
            };
            await ur.CloseAsync();

            // Рейтинг и кол-во отзывов
            await using var revCmd = new SqlCommand(
                "SELECT COUNT(*), AVG(CAST(Rating AS float)) FROM Reviews WHERE TargetUserId = @Id", conn);
            revCmd.Parameters.AddWithValue("@Id", id);
            await using var rr = await revCmd.ExecuteReaderAsync();
            int reviewCount = 0;
            double reviewAvg = 0;
            if (await rr.ReadAsync())
            {
                reviewCount = rr.IsDBNull(0) ? 0 : rr.GetInt32(0);
                reviewAvg = rr.IsDBNull(1) ? 0 : rr.GetDouble(1);
            }
            await rr.CloseAsync();

            // Категории продавца (только те где есть товары)
            await using var catCmd = new SqlCommand(@"
                SELECT DISTINCT c.Id, c.Name, COUNT(p.Id) as Cnt
                FROM Products p
                JOIN Category c ON c.Id = p.CategoryId
                WHERE p.UserId = @UserId
                GROUP BY c.Id, c.Name
                ORDER BY Cnt DESC", conn);
            catCmd.Parameters.AddWithValue("@UserId", id);
            await using var cr = await catCmd.ExecuteReaderAsync();
            var categories = new List<CategoryCount>();
            while (await cr.ReadAsync())
                categories.Add(new CategoryCount { Id = cr.GetInt32(0), Name = cr.GetString(1), Count = cr.GetInt32(2) });
            await cr.CloseAsync();

            // Товары продавца
            var order = sort switch
            {
                "cheap" => @"(SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                                  FROM MapperProductCategory m
                                  JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
                                  WHERE m.ProductId = p.Id) ASC",
                "expensive" => @"(SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                                  FROM MapperProductCategory m
                                  JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
                                  WHERE m.ProductId = p.Id) DESC",
                _ => "p.CreatedTime DESC"
            };

            var catFilter = categoryId.HasValue ? "AND p.CategoryId = @CatId" : "";

            await using var prodCmd = new SqlCommand($@"
                SELECT p.Id, p.Name, p.Address, p.CreatedTime, p.AvatarImagePath,
                       (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                        FROM MapperProductCategory m
                        JOIN Category c2 ON c2.Id = m.CategoryId AND c2.Name = 'Price'
                        WHERE m.ProductId = p.Id) as Price
                FROM Products p
                WHERE p.UserId = @UserId
                  AND (p.Status = 'active' OR p.Status IS NULL)
                  {catFilter}
                ORDER BY {order}", conn);
            prodCmd.Parameters.AddWithValue("@UserId", id);
            if (categoryId.HasValue)
                prodCmd.Parameters.AddWithValue("@CatId", categoryId.Value);

            await using var pr = await prodCmd.ExecuteReaderAsync();
            var products = new List<Product>();
            while (await pr.ReadAsync())
                products.Add(new Product
                {
                    Id = pr.GetInt32(0),
                    Name = pr.IsDBNull(1) ? "" : pr.GetString(1),
                    Address = pr.IsDBNull(2) ? null : pr.GetString(2),
                    CreatedTime = pr.IsDBNull(3) ? null : pr.GetDateTime(3),
                    AvatarImagePath = pr.IsDBNull(4) ? null : pr.GetString(4),
                    Price = pr.IsDBNull(5) ? null : pr.GetDecimal(5),
                });
            await pr.CloseAsync();

            // Отзывы (последние 50)
            await using var reviewsCmd = new SqlCommand(@"
                SELECT r.Id, r.Rating, r.Comment, r.CreatedAt,
                       u.UserName, u.AvatarImagePath
                FROM Reviews r
                JOIN Users u ON u.Id = r.AuthorId
                WHERE r.TargetUserId = @Id
                ORDER BY r.CreatedAt DESC
                OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY", conn);
            reviewsCmd.Parameters.AddWithValue("@Id", id);
            await using var rvr = await reviewsCmd.ExecuteReaderAsync();
            var reviews = new List<dynamic>();
            while (await rvr.ReadAsync())
                reviews.Add(new
                {
                    Id = rvr.GetInt32(0),
                    Rating = rvr.GetInt32(1),
                    Comment = rvr.IsDBNull(2) ? "" : rvr.GetString(2),
                    CreatedAt = rvr.GetDateTime(3),
                    AuthorName = rvr.IsDBNull(4) ? "Unknown" : rvr.GetString(4),
                    AuthorAvatar = rvr.IsDBNull(5) ? null : rvr.GetString(5),
                });

            ViewBag.Seller = seller;
            ViewBag.ReviewCount = reviewCount;
            ViewBag.ReviewAvg = reviewAvg;
            ViewBag.Categories = categories;
            ViewBag.Products = products;
            ViewBag.Reviews = reviews;
            ViewBag.Sort = sort;
            ViewBag.CategoryId = categoryId;

            return View();
        }
    }
}