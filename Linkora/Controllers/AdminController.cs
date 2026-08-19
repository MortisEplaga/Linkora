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
        public class PagedResult<T>
        {
            public List<T> Items { get; set; } = new();
            public int Total { get; set; }
            public int TotalPages { get; set; }
            public int CurrentPage { get; set; }
        }

        private readonly string _connectionString;
        private readonly IProductRepository _productRepository;
        private readonly IReportRepository _reportRepository;
        private readonly INotificationRepository _notificationRepository;

        public AdminController(IConfiguration configuration,
                               IProductRepository productRepository,
                               IReportRepository reportRepository,
                               INotificationRepository notificationRepository)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _productRepository = productRepository;
            _reportRepository = reportRepository;
            _notificationRepository = notificationRepository;
        }

        private bool IsAdmin() => User.FindFirst(ClaimTypes.Role)?.Value == "admin";

        private async Task SetSidebarBadgesAsync(SqlConnection conn)
        {
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE Status = 'Moderation'", conn))
                ViewBag.PendingModeration = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Reports WHERE Status = 'Pending'", conn))
                ViewBag.PendingReports = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM SelectOptions WHERE IsConf = 0", conn))
                ViewBag.PendingOptions = (int)(await cmd.ExecuteScalarAsync())!;
        }

        private async Task<PagedResult<T>> GetPagedDataAsync<T>(
            SqlConnection conn,
            string selectClause,
            string fromWhereClause,
            string orderByClause,
            int page,
            int pageSize,
            Action<SqlParameterCollection>? addParameters,
            Func<SqlDataReader, T> mapRow)
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
            {
                items.Add(mapRow(reader));
            }

            return new PagedResult<T>
            {
                Items = items,
                Total = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page
            };
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var stats = new AdminDashboardViewModel();

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Users", conn))
                stats.TotalUsers = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products", conn))
                stats.TotalProducts = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE Status = 'Moderation'", conn))
                stats.PendingModeration = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Reports WHERE Status = 'Pending'", conn))
                stats.PendingReports = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM SelectOptions WHERE IsConf = 0", conn))
                stats.PendingOptions = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)", conn))
                stats.NewUsersToday = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE CAST(CreatedTime AS DATE) = CAST(GETDATE() AS DATE)", conn))
                stats.NewProductsToday = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE Status = 'Active'", conn))
                stats.ActiveProducts = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand(@"
                SELECT Status, COUNT(*) FROM Products GROUP BY Status", conn))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    stats.ProductsByStatus[r.GetString(0)] = r.GetInt32(1);
            }

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

            ViewBag.PendingModeration = stats.PendingModeration;
            ViewBag.PendingReports = stats.PendingReports;
            ViewBag.PendingOptions = stats.PendingOptions;
            ViewBag.Stats = stats;
            return View();
        }

        public async Task<IActionResult> Products(string status = "Moderation", int page = 1, string? search = null)
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await SetSidebarBadgesAsync(conn);

            var searchClause = string.IsNullOrEmpty(search) ? "" : "AND p.Name LIKE '%' + @Search + '%'";

            var pagedData = await GetPagedDataAsync(
                conn: conn,
                selectClause: @"
                    SELECT p.Id, p.Name, p.Status, p.CreatedTime,
                           COALESCE(
                               (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                               p.AvatarImagePath
                           ) AS Img,
                           u.UserName, u.Id AS UserId,
                           (SELECT COUNT(*) FROM Reports WHERE ProductId = p.Id) AS ReportCount,
                           (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                            FROM MapperProductCategory m
                            JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                            WHERE m.ProductId = p.Id) AS Price",
                fromWhereClause: $"FROM Products p LEFT JOIN Users u ON u.Id = p.UserId WHERE p.Status = @Status {searchClause}",
                orderByClause: "ORDER BY p.CreatedTime DESC",
                page: page,
                pageSize: 20,
                addParameters: parameters =>
                {
                    parameters.AddWithValue("@Status", status);
                    if (!string.IsNullOrEmpty(search)) parameters.AddWithValue("@Search", search);
                },
                mapRow: r => new AdminProductRow
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

            ViewBag.Products = pagedData.Items;
            ViewBag.Status = status;
            ViewBag.Page = pagedData.CurrentPage;
            ViewBag.TotalPages = pagedData.TotalPages;
            ViewBag.Total = pagedData.Total;
            ViewBag.Search = search;

            return View();
        }

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

            if (status == "Active")
            {
                await using var ownerCmd = new SqlCommand(
                    "SELECT UserId FROM Products WHERE Id = @Id", conn);
                ownerCmd.Parameters.AddWithValue("@Id", id);
                var ownerObj = await ownerCmd.ExecuteScalarAsync();
                if (ownerObj != null && ownerObj != DBNull.Value)
                {
                    var msg = System.Text.Json.JsonSerializer.Serialize(new { type = "product_approved" });
                    await _notificationRepository.CreateAsync((int)ownerObj, null, id, msg);
                }
            }

            return Ok();
        }

        public async Task<IActionResult> Users(int page = 1, string? search = null, string role = "all")
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await SetSidebarBadgesAsync(conn);

            var roleClause = role == "all" ? "" : "AND Role = @Role";
            var searchClause = string.IsNullOrEmpty(search) ? "" : "AND (UserName LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')";

            var pagedData = await GetPagedDataAsync(
                conn: conn,
                selectClause: @"
                    SELECT u.Id, u.UserName, u.Email, u.PhoneNumber, u.Role, u.IsCompany,
                           u.AvatarImagePath, u.CreatedAt,
                           (SELECT COUNT(*) FROM Products WHERE UserId = u.Id) AS ProductCount",
                fromWhereClause: $"FROM Users u WHERE 1=1 {roleClause} {searchClause}",
                orderByClause: "ORDER BY u.CreatedAt DESC",
                page: page,
                pageSize: 25,
                addParameters: parameters =>
                {
                    if (role != "all") parameters.AddWithValue("@Role", role);
                    if (!string.IsNullOrEmpty(search)) parameters.AddWithValue("@Search", search);
                },
                mapRow: r => new AdminUserRow
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

            ViewBag.Users = pagedData.Items;
            ViewBag.Page = pagedData.CurrentPage;
            ViewBag.TotalPages = pagedData.TotalPages;
            ViewBag.Total = pagedData.Total;
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

            string? oldRole;
            await using (var getRoleCmd = new SqlCommand("SELECT Role FROM Users WHERE Id = @Id", conn))
            {
                getRoleCmd.Parameters.AddWithValue("@Id", id);
                oldRole = (await getRoleCmd.ExecuteScalarAsync()) as string;
            }

            await using var cmd = new SqlCommand("UPDATE Users SET Role = @R WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@R", role);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (role == "banned" && oldRole != "banned")
            {
                await _productRepository.ArchiveProductsByUserAsync(id);

                var msg = System.Text.Json.JsonSerializer.Serialize(new { type = "user_banned" });
                await _notificationRepository.CreateAsync(id, null, null, msg);

                await using var subsCmd = new SqlCommand(
                    "SELECT FollowerId FROM Subscriptions WHERE FollowingId = @Id", conn);
                subsCmd.Parameters.AddWithValue("@Id", id);
                await using var subsR = await subsCmd.ExecuteReaderAsync();
                var subIds = new List<int>();
                while (await subsR.ReadAsync()) subIds.Add(subsR.GetInt32(0));
                await subsR.CloseAsync();

                if (subIds.Any())
                {
                    var subBanMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "subscription_seller_banned" });
                    foreach (var subId in subIds)
                        await _notificationRepository.CreateAsync(subId, id, null, subBanMsg);
                }

                await using var favCmd = new SqlCommand(@"
                            SELECT DISTINCT f.UserId, f.ProductId 
                            FROM Favourites f 
                            JOIN Products p ON f.ProductId = p.Id 
                            WHERE p.UserId = @Id AND f.Can = 1", conn);
                favCmd.Parameters.AddWithValue("@Id", id);
                await using var favR = await favCmd.ExecuteReaderAsync();
                var favUsers = new List<(int UserId, int ProductId)>();
                while (await favR.ReadAsync())
                {
                    favUsers.Add((favR.GetInt32(0), favR.GetInt32(1)));
                }
                await favR.CloseAsync();

                if (favUsers.Any())
                {
                    var favBanMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "favourite_archived_ban" });
                    foreach (var fav in favUsers.Where(f => f.UserId != id))
                    {
                        await _notificationRepository.CreateAsync(fav.UserId, null, fav.ProductId, favBanMsg);
                    }
                }
            }
            else if (role != "banned" && oldRole == "banned")
            {
                var msg = System.Text.Json.JsonSerializer.Serialize(new { type = "user_unbanned" });
                await _notificationRepository.CreateAsync(id, null, null, msg);
            }

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

            var productIds = new List<int>();
            await using var getProductsCmd = new SqlCommand(
                "SELECT Id FROM Products WHERE UserId = @UserId", conn);
            getProductsCmd.Parameters.AddWithValue("@UserId", id);
            await using var reader = await getProductsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                productIds.Add(reader.GetInt32(0));
            await reader.CloseAsync();

            foreach (var productId in productIds)
            {
                await _productRepository.DeleteAsync(productId);
            }

            await using var cmd = new SqlCommand("DELETE FROM Users WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            return Ok();
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            if (!IsAdmin()) return Forbid();

            await _productRepository.DeleteAsync(id);
            return Ok();
        }

        public async Task<IActionResult> Reports(string status = "Pending", int page = 1)
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await SetSidebarBadgesAsync(conn);

            var pagedData = await GetPagedDataAsync(
                conn: conn,
                selectClause: @"
                    SELECT r.Id, r.ProductId, r.UserId, r.Comment, r.CreatedAt, r.Status,
                           p.Name AS ProductName,
                           COALESCE(
                               (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                               p.AvatarImagePath
                           ) AS ProductImg,
                           p.Status AS ProductStatus,
                           u.UserName AS ReporterName,
                           rr.ReasonText",
                fromWhereClause: @"
                    FROM Reports r
                    LEFT JOIN Products p ON p.Id = r.ProductId
                    LEFT JOIN Users u ON u.Id = r.UserId
                    LEFT JOIN ReportReasons rr ON rr.Id = r.ReportReasonId
                    WHERE r.Status = @Status",
                orderByClause: "ORDER BY r.CreatedAt DESC",
                page: page,
                pageSize: 20,
                addParameters: parameters => parameters.AddWithValue("@Status", status),
                mapRow: r => new AdminReportRow
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

            ViewBag.Reports = pagedData.Items;
            ViewBag.Status = status;
            ViewBag.Page = pagedData.CurrentPage;
            ViewBag.TotalPages = pagedData.TotalPages;
            ViewBag.Total = pagedData.Total;

            return View();
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> ResolveReport(int id, string action)
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var newStatus = action == "resolve" ? "Resolved" : "Rejected";
            await using var cmd = new SqlCommand(
                "UPDATE Reports SET Status = @S WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@S", newStatus);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (action == "reject_report")
            {
                await using var pCmd = new SqlCommand(@"
                    UPDATE Products 
                    SET ModerationScore = CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END,
                        Status = CASE 
                            WHEN Status = 'Moderation' AND (CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END) < 5 
                            THEN 'Active' 
                            ELSE Status 
                        END
                    WHERE Id = (SELECT ProductId FROM Reports WHERE Id = @Id)
                      AND Status IN ('Active', 'Moderation')", conn);
                pCmd.Parameters.AddWithValue("@Id", id);
                await pCmd.ExecuteNonQueryAsync();
            }
            return Ok(new { status = newStatus });
        }

        [HttpGet]
        public async Task<IActionResult> StatsApi()
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

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

        [HttpGet]
        public async Task<IActionResult> ConfOptions()
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await SetSidebarBadgesAsync(conn);

            var (items, totalCount) = await _productRepository.GetUnconfirmedOptionsAsync();

            ViewBag.Options = items;
            ViewBag.Total = totalCount;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOption(int id)
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var infoCmd = new SqlCommand(@"
                SELECT TOP 1 p.Id, p.UserId, c.Name, c.NameRU, c.NameLV
                FROM SelectOptions so
                JOIN MapperProductCategory mpc
                    ON ',' + mpc.Value + ',' LIKE '%,' + CAST(so.Id AS VARCHAR) + ',%'
                JOIN Products p ON mpc.ProductId = p.Id
                JOIN Category c ON so.CategoryId = c.Id
                WHERE so.Id = @OptionId", conn);
            infoCmd.Parameters.AddWithValue("@OptionId", id);

            int? productId = null, ownerId = null;
            string? paramName = null, paramNameRu = null, paramNameLv = null;
            await using (var r = await infoCmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    productId = r.GetInt32(0);
                    ownerId = r.IsDBNull(1) ? null : r.GetInt32(1);
                    paramName = r.IsDBNull(2) ? null : r.GetString(2);
                    paramNameRu = r.IsDBNull(3) ? paramName : r.GetString(3);
                    paramNameLv = r.IsDBNull(4) ? paramName : r.GetString(4);
                }
            }

            bool success = await _productRepository.ApproveSelectOptionAsync(id);

            if (success && ownerId.HasValue)
            {
                await using var scoreCmd = new SqlCommand(@"
                    UPDATE Products 
                    SET ModerationScore = CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END,
                        Status = CASE 
                            WHEN Status = 'Moderation' AND (CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END) < 5 
                            THEN 'Active' 
                            ELSE Status 
                        END
                    WHERE Id = @ProductId", conn);
                scoreCmd.Parameters.AddWithValue("@ProductId", productId.Value);
                await scoreCmd.ExecuteNonQueryAsync();

                var msg = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "parameter_approved",
                    paramName = paramName ?? "",
                    paramNameRu = paramNameRu ?? "",
                    paramNameLv = paramNameLv ?? ""
                });
                await _notificationRepository.CreateAsync(ownerId.Value, null, productId, msg);
            }

            if (success) return Ok();
            return BadRequest();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectProductByOption(int optionId, int productId)
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var infoCmd = new SqlCommand(@"
        SELECT p.UserId, c.Name, c.NameRU, c.NameLV
        FROM SelectOptions so
        JOIN Category c ON so.CategoryId = c.Id
        JOIN Products p ON p.Id = @ProductId
        WHERE so.Id = @OptionId", conn);
            infoCmd.Parameters.AddWithValue("@OptionId", optionId);
            infoCmd.Parameters.AddWithValue("@ProductId", productId);

            int? ownerId = null;
            string? paramName = null, paramNameRu = null, paramNameLv = null;
            await using (var r = await infoCmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    ownerId = r.IsDBNull(0) ? null : r.GetInt32(0);
                    paramName = r.IsDBNull(1) ? null : r.GetString(1);
                    paramNameRu = r.IsDBNull(2) ? paramName : r.GetString(2);
                    paramNameLv = r.IsDBNull(3) ? paramName : r.GetString(3);
                }
            }

            bool success = await _productRepository.RejectProductAndOptionAsync(optionId, productId);

            if (success && ownerId.HasValue)
            {
                var msg = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "parameter_rejected",
                    paramName = paramName ?? "",
                    paramNameRu = paramNameRu ?? "",
                    paramNameLv = paramNameLv ?? ""
                });
                await _notificationRepository.CreateAsync(ownerId.Value, null, productId, msg);
            }

            if (success) return Ok();
            return BadRequest();
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> RejectProductWithReason(int id, int reasonId, string? comment = null)
        {
            if (!IsAdmin()) return Forbid();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string reasonEn, reasonLv, reasonRu;
            await using (var reasonCmd = new SqlCommand(
                "SELECT ReasonText, ReasonTextLV, ReasonTextRU FROM ReportReasons WHERE Id = @Id", conn))
            {
                reasonCmd.Parameters.AddWithValue("@Id", reasonId);
                await using var r = await reasonCmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return BadRequest("Invalid reason");
                reasonEn = r.IsDBNull(0) ? "" : r.GetString(0);
                reasonLv = r.IsDBNull(1) ? reasonEn : r.GetString(1);
                reasonRu = r.IsDBNull(2) ? reasonEn : r.GetString(2);
            }

            int ownerId;
            await using (var prodCmd = new SqlCommand("SELECT UserId FROM Products WHERE Id = @Id", conn))
            {
                prodCmd.Parameters.AddWithValue("@Id", id);
                var result = await prodCmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value) return NotFound();
                ownerId = (int)result;
            }

            await using (var updCmd = new SqlCommand("UPDATE Products SET Status = 'Rejected' WHERE Id = @Id", conn))
            {
                updCmd.Parameters.AddWithValue("@Id", id);
                await updCmd.ExecuteNonQueryAsync();
            }

            var payload = new
            {
                type = "rejected_reason",
                reasonEn,
                reasonLv,
                reasonRu,
                comment = comment ?? ""
            };
            var message = System.Text.Json.JsonSerializer.Serialize(payload);

            await _notificationRepository.CreateAsync(ownerId, null, id, message);

            return Ok();
        }
    }
}