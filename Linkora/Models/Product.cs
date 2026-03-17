using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
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
        public string? Status { get; set; }
    }
}