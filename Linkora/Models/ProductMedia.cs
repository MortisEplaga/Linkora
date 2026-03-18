using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    [Table("ProductMedia")]
    public class ProductMedia
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ProductId { get; set; }

        [Required, StringLength(500)]
        public string FilePath { get; set; } = "";

        [Required, StringLength(10)]
        public string MediaType { get; set; } = "image"; // "image" | "video"

        public int SortOrder { get; set; }
    }
}