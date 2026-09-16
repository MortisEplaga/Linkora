using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    public enum PromotionTier : short
    {
        Highlight = 1,
        Top = 2,
        Vip = 3
    }

    public enum PromotionTermType : short
    {
        Week = 1,
        Month = 2,
        ThreeMonth = 3,
        Year = 4
    }

    public enum PromotionStatus : short
    {
        Superseded = 0,
        Active = 1,
        Expired = -1
    }

    [Table("Promotion")]
    public class Promotion : Base
    {
        public int UserId { get; set; }
        public PromotionTier Tier { get; set; }
        public PromotionTermType TermType { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePaid { get; set; }
        public PromotionStatus Status { get; set; } = PromotionStatus.Active;
    }
}