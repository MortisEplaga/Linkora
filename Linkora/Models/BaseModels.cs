namespace Linkora.Models
{
    public abstract class NamedEntity : Base
    {
        public string Name { get; set; } = "";
    }

    public abstract class ProductSummaryBase : NamedEntity
    {
        public DateTime? CreatedAt { get; set; }
        public string? AvatarUrl { get; set; }
        public decimal? Price { get; set; }
    }

    public class UserSummary : Base
    {
        public string UserName { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsCompany { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public abstract class ReportBase : Base
    {
        public int ProductId { get; set; }
        public int UserId { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public abstract class Base
    {
        public int Id { get; set; }
    }

    public abstract class OptionModerationResultBase
    {
        public bool Success { get; set; }
        public int? UserId { get; set; }
        public string? ParamName { get; set; }
        public string? ParamNameRu { get; set; }
        public string? ParamNameLv { get; set; }
    }

    public class PaymentBase : Base
    {
        public int UserId { get; set; }
        public string PurposeType { get; set; } = "";
        public int? ProductId { get; set; }
        public string? PromotionType { get; set; }
        public string? SubscriptionType { get; set; }
        public string Status { get; set; } = "";
    }
}