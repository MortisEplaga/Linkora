namespace Linkora.Models
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }

    public class AdminBadges
    {
        public int PendingModeration { get; set; }
        public int PendingReports { get; set; }
        public int PendingOptions { get; set; }
    }

    public class AdminStatsApiData
    {
        public List<object> Registrations { get; set; } = new();
        public List<object> Products { get; set; } = new();
    }

    public class BanUserResult
    {
        public List<int> SubscriberIds { get; set; } = new();
        public List<(int UserId, int ProductId)> FavouriteUsers { get; set; } = new();
    }

    public class ApproveOptionResult
    {
        public bool Success { get; set; }
        public int? OwnerId { get; set; }
        public int? ProductId { get; set; }
        public string? ParamName { get; set; }
        public string? ParamNameRu { get; set; }
        public string? ParamNameLv { get; set; }
    }

    public class RejectOptionResult
    {
        public bool Success { get; set; }
        public int? OwnerId { get; set; }
        public string? ParamName { get; set; }
        public string? ParamNameRu { get; set; }
        public string? ParamNameLv { get; set; }
    }

    public class RejectProductResult
    {
        public bool Success { get; set; }
        public bool InvalidReason { get; set; }
        public int OwnerId { get; set; }
        public string ReasonEn { get; set; } = "";
        public string ReasonLv { get; set; } = "";
        public string ReasonRu { get; set; } = "";
        public string Comment { get; set; } = "";
    }
    public class AdminDashboardViewModel : AdminBadges
    {
        public int TotalUsers { get; set; }
        public int TotalProducts { get; set; }
        public int NewUsersToday { get; set; }
        public int NewProductsToday { get; set; }
        public int ActiveProducts { get; set; }
        public Dictionary<string, int> ProductsByStatus { get; set; } = new();
        public List<AdminProductRow> RecentProducts { get; set; } = new();
    }

    public class AdminProductRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? CreatedTime { get; set; }
        public string? ImagePath { get; set; }
        public string UserName { get; set; } = "";
        public int UserId { get; set; }
        public int ReportCount { get; set; }
        public decimal? Price { get; set; }
    }

    public class AdminUserRow
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Role { get; set; } = "user";
        public bool IsCompany { get; set; }
        public string? AvatarPath { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int ProductCount { get; set; }
    }

    public class AdminReportRow
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string? ProductImage { get; set; }
        public string ProductStatus { get; set; } = "";
        public string ReporterName { get; set; } = "";
        public string ReasonText { get; set; } = "";
    }

    public class AdminConfOptionRow
    {
        public int OptionId { get; set; }
        public string OptionValue { get; set; }
        public string OptionValueLV { get; set; }
        public string OptionValueRU { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public DateTime? ProductCreatedTime { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string ParameterName { get; set; } = "";
        public string CategoryNameLV { get; set; }
        public string ParameterNameLV { get; set; } = "";
        public string CategoryNameRU { get; set; }
        public string ParameterNameRU { get; set; } = "";
    }
}