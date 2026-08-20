using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class AdminRepository : IAdminRepository
    {
        private readonly string _connectionString;
        private readonly IProductRepository _productRepository;

        public AdminRepository(IConfiguration configuration, IProductRepository productRepository)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _productRepository = productRepository;
        }

        private async Task<PagedResult<T>> GetPagedDataAsync<T>(
            SqlConnection conn, string selectClause, string fromWhereClause, string orderByClause,
            int page, int pageSize, Action<SqlParameterCollection>? addParameters, Func<SqlDataReader, T> mapRow)
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

        public async Task<AdminBadges> GetSidebarBadgesAsync()
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var badges = new AdminBadges();

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE Status = 'Moderation'", conn))
                badges.PendingModeration = (int)(await cmd.ExecuteScalarAsync())!;
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Reports WHERE Status = 'Pending'", conn))
                badges.PendingReports = (int)(await cmd.ExecuteScalarAsync())!;
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM SelectOptions WHERE IsConf = 0", conn))
                badges.PendingOptions = (int)(await cmd.ExecuteScalarAsync())!;

            return badges;
        }

        public async Task<AdminDashboardViewModel> GetDashboardStatsAsync()
        {
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

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)", conn))
                stats.NewProductsToday = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE Status = 'Active'", conn))
                stats.ActiveProducts = (int)(await cmd.ExecuteScalarAsync())!;

            await using (var cmd = new SqlCommand("SELECT Status, COUNT(*) FROM Products GROUP BY Status", conn))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    stats.ProductsByStatus[r.GetString(0)] = r.GetInt32(1);
            }

            await using (var cmd = new SqlCommand(@"
        SELECT TOP 10 p.Id, p.Name, p.Status, p.CreatedAt, u.UserName
        FROM Products p
        LEFT JOIN Users u ON u.Id = p.UserId
        ORDER BY p.CreatedAt DESC", conn))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    stats.RecentProducts.Add(new AdminProductRow
                    {
                        Id = r.GetInt32(0),
                        Name = r.IsDBNull(1) ? "" : r.GetString(1),
                        Status = r.IsDBNull(2) ? "" : r.GetString(2),
                        CreatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
                        UserName = r.IsDBNull(4) ? "" : r.GetString(4),
                    });
            }

            return stats;
        }
        public async Task<PagedResult<AdminProductRow>> GetProductsAsync(string status, int page, string? search)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var searchClause = string.IsNullOrEmpty(search) ? "" : "AND p.Name LIKE '%' + @Search + '%'";

            return await GetPagedDataAsync(
                conn: conn,
                selectClause: @"
            SELECT p.Id, p.Name, p.Status, p.CreatedAt,
                   COALESCE(
                       (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                       p.AvatarUrl
                   ) AS Img,
                   u.UserName, u.Id AS UserId,
                   (SELECT COUNT(*) FROM Reports WHERE ProductId = p.Id) AS ReportCount,
                   (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                    FROM MapperProductCategory m
                    JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                    WHERE m.ProductId = p.Id) AS Price",
                fromWhereClause: $"FROM Products p LEFT JOIN Users u ON u.Id = p.UserId WHERE p.Status = @Status {searchClause}",
                orderByClause: "ORDER BY p.CreatedAt DESC",
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
                    CreatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    AvatarUrl = r.IsDBNull(4) ? null : r.GetString(4),
                    UserName = r.IsDBNull(5) ? "" : r.GetString(5),
                    UserId = r.IsDBNull(6) ? 0 : r.GetInt32(6),
                    ReportCount = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                    Price = r.IsDBNull(8) ? null : r.GetDecimal(8),
                });
        }
        public async Task<int?> SetProductStatusAsync(int id, string status)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("UPDATE Products SET Status = @S WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@S", status);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (status == "Active")
            {
                await using var ownerCmd = new SqlCommand("SELECT UserId FROM Products WHERE Id = @Id", conn);
                ownerCmd.Parameters.AddWithValue("@Id", id);
                var ownerObj = await ownerCmd.ExecuteScalarAsync();
                if (ownerObj != null && ownerObj != DBNull.Value) return (int)ownerObj;
            }
            return null;
        }

        public async Task<PagedResult<AdminUserRow>> GetUsersAsync(int page, string? search, string role)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var roleClause = role == "all" ? "" : "AND Role = @Role";
            var searchClause = string.IsNullOrEmpty(search) ? "" : "AND (UserName LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')";

            return await GetPagedDataAsync(
                conn: conn,
                selectClause: @"
            SELECT u.Id, u.UserName, u.Email, u.Phone, u.Role, u.IsCompany,
                   u.AvatarUrl, u.CreatedAt,
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
                    AvatarUrl = r.IsDBNull(6) ? null : r.GetString(6),
                    CreatedAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
                    ProductCount = r.IsDBNull(8) ? 0 : r.GetInt32(8),
                });
        }
        public async Task<(string? oldRole, BanUserResult? banData)> SetUserRoleAsync(int id, string role)
        {
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

            BanUserResult? banData = null;
            if (role == "banned" && oldRole != "banned")
            {
                await _productRepository.ArchiveProductsByUserAsync(id);
                banData = new BanUserResult();

                await using var subsCmd = new SqlCommand("SELECT FollowerId FROM Subscriptions WHERE FollowingId = @Id", conn);
                subsCmd.Parameters.AddWithValue("@Id", id);
                await using var subsR = await subsCmd.ExecuteReaderAsync();
                while (await subsR.ReadAsync()) banData.SubscriberIds.Add(subsR.GetInt32(0));
                await subsR.CloseAsync();

                await using var favCmd = new SqlCommand(@"SELECT DISTINCT f.UserId, f.ProductId FROM Favourites f JOIN Products p ON f.ProductId = p.Id WHERE p.UserId = @Id AND f.Can = 1", conn);
                favCmd.Parameters.AddWithValue("@Id", id);
                await using var favR = await favCmd.ExecuteReaderAsync();
                while (await favR.ReadAsync()) banData.FavouriteUsers.Add((favR.GetInt32(0), favR.GetInt32(1)));
            }
            return (oldRole, banData);
        }

        public async Task DeleteUserAsync(int id)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var productIds = new List<int>();
            await using var getProductsCmd = new SqlCommand("SELECT Id FROM Products WHERE UserId = @UserId", conn);
            getProductsCmd.Parameters.AddWithValue("@UserId", id);
            await using var reader = await getProductsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) productIds.Add(reader.GetInt32(0));
            await reader.CloseAsync();

            foreach (var productId in productIds) await _productRepository.DeleteAsync(productId);

            await using var cmd = new SqlCommand("DELETE FROM Users WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<PagedResult<AdminReportRow>> GetReportsAsync(string status, int page)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            return await GetPagedDataAsync(
                conn: conn,
                selectClause: @"
            SELECT r.Id, r.ProductId, r.UserId, r.Comment, r.CreatedAt, r.Status,
                   p.Name AS ProductName,
                   COALESCE(
                       (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                       p.AvatarUrl
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
        }
        public async Task<string> ResolveReportAsync(int id, string action)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var newStatus = action == "resolve" ? "Resolved" : "Rejected";
            await using var cmd = new SqlCommand("UPDATE Reports SET Status = @S WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@S", newStatus);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            if (action == "reject_report")
            {
                await using var pCmd = new SqlCommand(@"UPDATE Products SET ModerationScore = CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END, Status = CASE WHEN Status = 'Moderation' AND (CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END) < 5 THEN 'Active' ELSE Status END WHERE Id = (SELECT ProductId FROM Reports WHERE Id = @Id) AND Status IN ('Active', 'Moderation')", conn);
                pCmd.Parameters.AddWithValue("@Id", id);
                await pCmd.ExecuteNonQueryAsync();
            }
            return newStatus;
        }

        public async Task<AdminStatsApiData> GetStatsApiDataAsync()
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var data = new AdminStatsApiData();

            await using (var cmd = new SqlCommand(@"
        SELECT CAST(CreatedAt AS DATE) AS Day, COUNT(*) AS Cnt
        FROM Users
        WHERE CreatedAt >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
        GROUP BY CAST(CreatedAt AS DATE)
        ORDER BY Day", conn))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    data.Registrations.Add(new { day = r.GetDateTime(0).ToString("dd MMM"), count = r.GetInt32(1) });
            }

            await using (var cmd2 = new SqlCommand(@"
        SELECT CAST(CreatedAt AS DATE) AS Day, COUNT(*) AS Cnt
        FROM Products
        WHERE CreatedAt >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
        GROUP BY CAST(CreatedAt AS DATE)
        ORDER BY Day", conn))
            {
                await using var r2 = await cmd2.ExecuteReaderAsync();
                while (await r2.ReadAsync())
                    data.Products.Add(new { day = r2.GetDateTime(0).ToString("dd MMM"), count = r2.GetInt32(1) });
            }

            return data;
        }
        public async Task<ApproveOptionResult> ApproveOptionAsync(int id)
        {
            var result = new ApproveOptionResult();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var infoCmd = new SqlCommand(@"SELECT TOP 1 p.Id, p.UserId, c.Name, c.NameRU, c.NameLV FROM SelectOptions so JOIN MapperProductCategory mpc ON ',' + mpc.Value + ',' LIKE '%,' + CAST(so.Id AS VARCHAR) + ',%' JOIN Products p ON mpc.ProductId = p.Id JOIN Category c ON so.CategoryId = c.Id WHERE so.Id = @OptionId", conn);
            infoCmd.Parameters.AddWithValue("@OptionId", id);
            await using (var r = await infoCmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    result.ProductId = r.GetInt32(0);
                    result.UserId = r.IsDBNull(1) ? null : r.GetInt32(1);
                    result.ParamName = r.IsDBNull(2) ? null : r.GetString(2);
                    result.ParamNameRu = r.IsDBNull(3) ? result.ParamName : r.GetString(3);
                    result.ParamNameLv = r.IsDBNull(4) ? result.ParamName : r.GetString(4);
                }
            }

            result.Success = await _productRepository.ApproveSelectOptionAsync(id);
            if (result.Success && result.UserId.HasValue && result.ProductId.HasValue)
            {
                await using var scoreCmd = new SqlCommand(@"UPDATE Products SET ModerationScore = CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END, Status = CASE WHEN Status = 'Moderation' AND (CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END) < 5 THEN 'Active' ELSE Status END WHERE Id = @ProductId", conn);
                scoreCmd.Parameters.AddWithValue("@ProductId", result.ProductId.Value);
                await scoreCmd.ExecuteNonQueryAsync();
            }
            return result;
        }

        public async Task<RejectOptionResult> RejectProductByOptionAsync(int optionId, int productId)
        {
            var result = new RejectOptionResult();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var infoCmd = new SqlCommand(@"SELECT p.UserId, c.Name, c.NameRU, c.NameLV FROM SelectOptions so JOIN Category c ON so.CategoryId = c.Id JOIN Products p ON p.Id = @ProductId WHERE so.Id = @OptionId", conn);
            infoCmd.Parameters.AddWithValue("@OptionId", optionId);
            infoCmd.Parameters.AddWithValue("@ProductId", productId);
            await using (var r = await infoCmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    result.UserId = r.IsDBNull(0) ? null : r.GetInt32(0);
                    result.ParamName = r.IsDBNull(1) ? null : r.GetString(1);
                    result.ParamNameRu = r.IsDBNull(2) ? result.ParamName : r.GetString(2);
                    result.ParamNameLv = r.IsDBNull(3) ? result.ParamName : r.GetString(3);
                }
            }
            result.Success = await _productRepository.RejectProductAndOptionAsync(optionId, productId);
            return result;
        }

        public async Task<RejectProductResult> RejectProductWithReasonAsync(int id, int reasonId, string? comment)
        {
            var result = new RejectProductResult();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using (var reasonCmd = new SqlCommand("SELECT ReasonText, ReasonTextLV, ReasonTextRU FROM ReportReasons WHERE Id = @Id", conn))
            {
                reasonCmd.Parameters.AddWithValue("@Id", reasonId);
                await using var r = await reasonCmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) { result.InvalidReason = true; return result; }
                result.ReasonEn = r.IsDBNull(0) ? "" : r.GetString(0);
                result.ReasonLv = r.IsDBNull(1) ? result.ReasonEn : r.GetString(1);
                result.ReasonRu = r.IsDBNull(2) ? result.ReasonEn : r.GetString(2);
            }
            await using (var prodCmd = new SqlCommand("SELECT UserId FROM Products WHERE Id = @Id", conn))
            {
                prodCmd.Parameters.AddWithValue("@Id", id);
                var res = await prodCmd.ExecuteScalarAsync();
                if (res == null || res == DBNull.Value) return result;
                result.UserId = (int)res;
            }
            await using (var updCmd = new SqlCommand("UPDATE Products SET Status = 'Rejected' WHERE Id = @Id", conn))
            {
                updCmd.Parameters.AddWithValue("@Id", id);
                await updCmd.ExecuteNonQueryAsync();
            }
            result.Success = true;
            result.Comment = comment ?? "";
            return result;
        }
    }
}