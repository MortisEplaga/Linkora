using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    [Table("Parameter")]
    public class Parameter
    {
        [Required]
        public Category Param { get; set; } = null!;
        public List<string> Options { get; set; } = [];

       [Column(TypeName = "decimal(18,2)")]
        public decimal? Min { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Max { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Step { get; set; }
    }
}