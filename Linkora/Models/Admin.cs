namespace Linkora.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalProducts { get; set; }
        public int PendingModeration { get; set; }
        public int PendingReports { get; set; }
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
}
