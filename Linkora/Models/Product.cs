using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    public enum ProductStatus
    {
        Active,
        Moderation,
        Rejected,
        Archived,
        Succeeded
    }

    [Table("Products")]
    public class Product : ProductSummaryBase
    {
        public string Description { get; set; }
        public int? Qty { get; set; }

        [StringLength(50)]
        public string Address { get; set; }
        public int? UserId { get; set; }
        [NotMapped] public int? CategoryId { get; set; }
        public string PromotionType { get; set; } = "None";
        [NotMapped] public UserSummary? Seller { get; set; }
        public ProductStatus Status { get; set; } = ProductStatus.Active;
        public DateTime? ArchivedAt { get; set; }
        [NotMapped] public List<ProductMedia> Media { get; set; } = [];
        [NotMapped] public int ViewCount { get; set; }
        [NotMapped] public int FavCount { get; set; }
        [NotMapped] public int CartCount { get; set; }
    }

    public class CategoryRulesDto
    {
        public List<VisibilityRuleDto> VisibilityRules { get; set; } = [];
        public List<ValidationRuleDto> ValidationRules { get; set; } = [];
        public List<string> CustomScriptPaths { get; set; } = [];
    }

    public class VisibilityRuleDto
    {
        public int TargetParamId { get; set; }
        public int TriggerParamId { get; set; }
        public string? TriggerValue { get; set; }
        public string TriggerOperator { get; set; } = "eq";
        public string Action { get; set; } = "show";
    }

    public class ValidationRuleDto
    {
        public int ParamId { get; set; }
        public string RuleType { get; set; } = "";
        public string? RuleValue { get; set; }
        public int? TriggerParamId { get; set; }
        public string? TriggerValue { get; set; }
        public string? ErrorMessageKey { get; set; }
    }

    public class ResolveSelectOptionDto
    {
        public int ParamId { get; set; }
        public string Text { get; set; } = "";
        public bool CreateIfNotFound { get; set; } = true;
    }
}