using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Linkora.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly string _connectionString;
        private readonly IProductRepository _productRepository;
        private readonly IReportRepository _reportRepository;

        public AdminController(IConfiguration configuration,
            IProductRepository productRepository,
            IReportRepository reportRepository)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _productRepository = productRepository;
            _reportRepository = reportRepository;
        }

        private bool IsAdmin() =>
            User.FindFirst(ClaimTypes.Role)?.Value == "admin";

        // ── Dashboard ──
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var stats = new AdminDashboardViewModel();

            // Total users
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Users", conn))
                stats.TotalUsers = (int)(await cmd.ExecuteScalarAsync())!;

            // Total products
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products", conn))
                stats.TotalProducts = (int)(await cmd.ExecuteScalarAsync())!;

            // Pending moderation
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE Status = 'Moderation'", conn))
                stats.PendingModeration = (int)(await cmd.ExecuteScalarAsync())!;

            // Pending reports
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Reports WHERE Status = 'Pending'", conn))
                stats.PendingReports = (int)(await cmd.ExecuteScalarAsync())!;

            // New users today
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)", conn))
                stats.NewUsersToday = (int)(await cmd.ExecuteScalarAsync())!;

            // New products today
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE CAST(CreatedTime AS DATE) = CAST(GETDATE() AS DATE)", conn))
                stats.NewProductsToday = (int)(await cmd.ExecuteScalarAsync())!;

            // Active products
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE Status = 'Active'", conn))
                stats.ActiveProducts = (int)(await cmd.ExecuteScalarAsync())!;

            // Products by status
            await using (var cmd = new SqlCommand(@"
                SELECT Status, COUNT(*) FROM Products GROUP BY Status", conn))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    stats.ProductsByStatus[r.GetString(0)] = r.GetInt32(1);
            }

            // Recent activity (last 10 products)
            await using (var cmd = new SqlCommand(@"
                SELECT TOP 10 p.Id, p.Name, p.Status, p.CreatedTime, u.UserName
                FROM Products p
                LEFT JOIN Users u ON u.Id = p.UserId
                ORDER BY p.CreatedTime DESC", conn))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    stats.RecentProducts.Add(new AdminProductRow
                    {
                        Id = r.GetInt32(0),
                        Name = r.IsDBNull(1) ? "" : r.GetString(1),
                        Status = r.IsDBNull(2) ? "" : r.GetString(2),
                        CreatedTime = r.IsDBNull(3) ? null : r.GetDateTime(3),
                        UserName = r.IsDBNull(4) ? "" : r.GetString(4),
                    });
            }

            ViewBag.Stats = stats;
            return View();
        }

        // ── Products moderation ──
        public async Task<IActionResult> Products(string status = "Moderation", int page = 1, string? search = null)
        {
            if (!IsAdmin()) return Forbid();

            const int pageSize = 20;
            var offset = (page - 1) * pageSize;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var searchClause = string.IsNullOrEmpty(search) ? "" : "AND p.Name LIKE '%' + @Search + '%'";

            await using var countCmd = new SqlCommand($@"
                SELECT COUNT(*) FROM Products p WHERE p.Status = @Status {searchClause}", conn);
            countCmd.Parameters.AddWithValue("@Status", status);
            if (!string.IsNullOrEmpty(search))
                countCmd.Parameters.AddWithValue("@Search", search);
            var total = (int)(await countCmd.ExecuteScalarAsync())!;

            await using var cmd = new SqlCommand($@"
                SELECT p.Id, p.Name, p.Status, p.CreatedTime,
                       COALESCE(
                           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                           p.AvatarImagePath
                       ) AS Img,
                       u.UserName, u.Id AS UserId,
                       (SELECT COUNT(*) FROM Reports WHERE ProductId = p.Id) AS ReportCount,
                       (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                        FROM MapperProductCategory m
                        JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
                        WHERE m.ProductId = p.Id) AS Price
                FROM Products p
                LEFT JOIN Users u ON u.Id = p.UserId
                WHERE p.Status = @Status {searchClause}
                ORDER BY p.CreatedTime DESC
                OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY", conn);
            cmd.Parameters.AddWithValue("@Status", status);
            if (!string.IsNullOrEmpty(search))
                cmd.Parameters.AddWithValue("@Search", search);

            var products = new List<AdminProductRow>();
            await using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                    products.Add(new AdminProductRow
                    {
                        Id = r.GetInt32(0),
                        Name = r.IsDBNull(1) ? "" : r.GetString(1),
                        Status = r.IsDBNull(2) ? "" : r.GetString(2),
                        CreatedTime = r.IsDBNull(3) ? null : r.GetDateTime(3),
                        ImagePath = r.IsDBNull(4) ? null : r.GetString(4),
                        UserName = r.IsDBNull(5) ? "" : r.GetString(5),
                        UserId = r.IsDBNull(6) ? 0 : r.GetInt32(6),
                        ReportCount = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                        Price = r.IsDBNull(8) ? null : r.GetDecimal(8),
                    });
            }

            ViewBag.Products = products;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Total = total;
            ViewBag.Search = search;
            return View();
        }

        // ── Approve / Reject product ──
        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetProductStatus(int id, string status)
        {
            if (!IsAdmin()) return Forbid();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("UPDATE Products SET Status = @S WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@S", status);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }

        // ── Users ──
        public async Task<IActionResult> Users(int page = 1, string? search = null, string role = "all")
        {
            if (!IsAdmin()) return Forbid();

            const int pageSize = 25;
            var offset = (page - 1) * pageSize;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var roleClause = role == "all" ? "" : "AND Role = @Role";
            var searchClause = string.IsNullOrEmpty(search) ? "" : "AND (UserName LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')";

            await using var countCmd = new SqlCommand($"SELECT COUNT(*) FROM Users WHERE 1=1 {roleClause} {searchClause}", conn);
            if (role != "all") countCmd.Parameters.AddWithValue("@Role", role);
            if (!string.IsNullOrEmpty(search)) countCmd.Parameters.AddWithValue("@Search", search);
            var total = (int)(await countCmd.ExecuteScalarAsync())!;

            await using var cmd = new SqlCommand($@"
                SELECT u.Id, u.UserName, u.Email, u.PhoneNumber, u.Role, u.IsCompany,
                       u.AvatarImagePath, u.CreatedAt,
                       (SELECT COUNT(*) FROM Products WHERE UserId = u.Id) AS ProductCount
                FROM Users u
                WHERE 1=1 {roleClause} {searchClause}
                ORDER BY u.CreatedAt DESC
                OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY", conn);
            if (role != "all") cmd.Parameters.AddWithValue("@Role", role);
            if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@Search", search);

            var users = new List<AdminUserRow>();
            await using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                    users.Add(new AdminUserRow
                    {
                        Id = r.GetInt32(0),
                        UserName = r.IsDBNull(1) ? "" : r.GetString(1),
                        Email = r.IsDBNull(2) ? null : r.GetString(2),
                        Phone = r.IsDBNull(3) ? null : r.GetString(3),
                        Role = r.IsDBNull(4) ? "user" : r.GetString(4),
                        IsCompany = !r.IsDBNull(5) && r.GetBoolean(5),
                        AvatarPath = r.IsDBNull(6) ? null : r.GetString(6),
                        CreatedAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
                        ProductCount = r.IsDBNull(8) ? 0 : r.GetInt32(8),
                    });
            }

            ViewBag.Users = users;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Total = total;
            ViewBag.Search = search;
            ViewBag.Role = role;
            return View();
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetUserRole(int id, string role)
        {
            if (!IsAdmin()) return Forbid();
            var myId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (id == myId) return BadRequest("Cannot change your own role");

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("UPDATE Users SET Role = @R WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@R", role);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!IsAdmin()) return Forbid();
            var myId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (id == myId) return BadRequest("Cannot delete yourself");

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM Users WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }

        // ── Reports ──
        public async Task<IActionResult> Reports(string status = "Pending", int page = 1)
        {
            if (!IsAdmin()) return Forbid();

            const int pageSize = 20;
            var offset = (page - 1) * pageSize;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var countCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Reports WHERE Status = @Status", conn);
            countCmd.Parameters.AddWithValue("@Status", status);
            var total = (int)(await countCmd.ExecuteScalarAsync())!;

            await using var cmd = new SqlCommand($@"
                SELECT r.Id, r.ProductId, r.UserId, r.Comment, r.CreatedAt, r.Status,
                       p.Name AS ProductName,
                       COALESCE(
                           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                           p.AvatarImagePath
                       ) AS ProductImg,
                       p.Status AS ProductStatus,
                       u.UserName AS ReporterName,
                       rr.ReasonText
                FROM Reports r
                LEFT JOIN Products p ON p.Id = r.ProductId
                LEFT JOIN Users u ON u.Id = r.UserId
                LEFT JOIN ReportReasons rr ON rr.Id = r.ReportReasonId
                WHERE r.Status = @Status
                ORDER BY r.CreatedAt DESC
                OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY", conn);
            cmd.Parameters.AddWithValue("@Status", status);

            var reports = new List<AdminReportRow>();
            await using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                    reports.Add(new AdminReportRow
                    {
                        Id = r.GetInt32(0),
                        ProductId = r.GetInt32(1),
                        UserId = r.GetInt32(2),
                        Comment = r.IsDBNull(3) ? null : r.GetString(3),
                        CreatedAt = r.GetDateTime(4),
                        Status = r.GetString(5),
                        ProductName = r.IsDBNull(6) ? "" : r.GetString(6),
                        ProductImage = r.IsDBNull(7) ? null : r.GetString(7),
                        ProductStatus = r.IsDBNull(8) ? "" : r.GetString(8),
                        ReporterName = r.IsDBNull(9) ? "" : r.GetString(9),
                        ReasonText = r.IsDBNull(10) ? "" : r.GetString(10),
                    });
            }

            ViewBag.Reports = reports;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Total = total;
            return View();
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> ResolveReport(int id, string action)
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Update report status
            var newStatus = action == "resolve" ? "Resolved" : "Rejected";
            await using var cmd = new SqlCommand(
                "UPDATE Reports SET Status = @S WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@S", newStatus);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            // If resolving — keep product in moderation; if rejecting report — restore product to Active
            if (action == "reject_report")
            {
                await using var pCmd = new SqlCommand(@"
                    UPDATE Products SET Status = 'Active'
                    WHERE Id = (SELECT ProductId FROM Reports WHERE Id = @Id)
                      AND Status = 'Moderation'", conn);
                pCmd.Parameters.AddWithValue("@Id", id);
                await pCmd.ExecuteNonQueryAsync();
            }

            return Ok(new { status = newStatus });
        }

        // ── Stats API for charts ──
        [HttpGet]
        public async Task<IActionResult> StatsApi()
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Last 7 days registrations
            await using var cmd = new SqlCommand(@"
                SELECT CAST(CreatedAt AS DATE) AS Day, COUNT(*) AS Cnt
                FROM Users
                WHERE CreatedAt >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
                GROUP BY CAST(CreatedAt AS DATE)
                ORDER BY Day", conn);

            var regData = new List<object>();
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    regData.Add(new { day = r.GetDateTime(0).ToString("dd MMM"), count = r.GetInt32(1) });

            // Last 7 days products
            await using var cmd2 = new SqlCommand(@"
                SELECT CAST(CreatedTime AS DATE) AS Day, COUNT(*) AS Cnt
                FROM Products
                WHERE CreatedTime >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
                GROUP BY CAST(CreatedTime AS DATE)
                ORDER BY Day", conn);

            var prodData = new List<object>();
            await using (var r2 = await cmd2.ExecuteReaderAsync())
                while (await r2.ReadAsync())
                    prodData.Add(new { day = r2.GetDateTime(0).ToString("dd MMM"), count = r2.GetInt32(1) });

            return Json(new { registrations = regData, products = prodData });
        }
    }

    // ── ViewModels ──
}
