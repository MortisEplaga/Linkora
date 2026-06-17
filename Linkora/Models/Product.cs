using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    public enum ProductStatus
    {
        Active,      // Активное
        Moderation,  // На модерации (после жалобы)
        Rejected,    // Отклонено модератором
        Archived,    // В архиве (скрыто пользователем или автоматически)
        Succeeded    // Успешно завершено
    }

    [Table("Products")]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; }

        public string Description { get; set; }

        public int? Qty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [NotMapped] public decimal? Price { get; set; }

        [StringLength(50)]
        public string Address { get; set; }

        public DateTime? CreatedTime { get; set; }

        public int? UserId { get; set; }
        [NotMapped] public int? CategoryId { get; set; }


        [StringLength(500)]
        public string AvatarImagePath { get; set; }
        [NotMapped] public SellerViewModel? Seller { get; set; }
        public ProductStatus Status { get; set; } = ProductStatus.Active;
        public DateTime? ArchivedAt { get; set; }
        [NotMapped] public List<ProductMedia> Media { get; set; } = new();
        [NotMapped] public int ViewCount { get; set; }
        [NotMapped] public int FavCount { get; set; }
        [NotMapped] public int CartCount { get; set; }
    }
    public class CategoryRulesDto
    {
        public List<VisibilityRuleDto> VisibilityRules { get; set; } = new();
        public List<ValidationRuleDto> ValidationRules { get; set; } = new();
        public List<string> CustomScriptPaths { get; set; } = new();
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