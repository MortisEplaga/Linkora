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
    }
}