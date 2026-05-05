using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    [Table("Category")]
    public class Category
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? ParentId { get; set; }

        [Required]
        [StringLength(75)]
        public string Name { get; set; }
        public string? NameLV { get; set; }
        public string? NameEn { get; set; }

        public int? Type { get; set; }
    }
}