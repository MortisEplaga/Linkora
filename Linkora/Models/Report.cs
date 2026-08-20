using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    public class Report
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Required]
        public int ReportReasonId { get; set; } 

        [StringLength(500)]
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
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
    public class ReportReason
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ReasonText { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
    public class ReportRequest
    {
        public int ProductId { get; set; }
        public int ReportReasonId { get; set; }
        public string? Comment { get; set; }
    }

}