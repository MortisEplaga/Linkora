using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    [Table("Parameter")]
    public class Parameter
    {
        [Required]
        public Category Param { get; set; } = null!;
        public List<SelectOption> Options { get; set; } = [];
        public List<ColorOption> ColorOptions { get; set; } = [];

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Min { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Max { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Step { get; set; }
    }
    public class SelectOption
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
    }
    public class ColorOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string HexValue { get; set; } = "";
    }
}