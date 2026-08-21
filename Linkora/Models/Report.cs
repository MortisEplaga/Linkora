using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    public class Report : ReportBase
    {
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Required]
        public int ReportReasonId { get; set; }
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
    }

    public enum ReportStatus
    {
        Pending,
        Reviewed,
        Resolved,
        Rejected
    }

    [Table("ReportReasons")]
    public class ReportReason : Base
    {
        [Required]
        [StringLength(100)]
        public string ReasonText { get; set; } = string.Empty;
        public string ReasonTextLV { get; set; } = string.Empty;
        public string ReasonTextRU { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class ReportRequest
    {
        public int ProductId { get; set; }
        public int ReportReasonId { get; set; }
        public string? Comment { get; set; }
    }
    public class ReportReasonLocalized
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
    }
}