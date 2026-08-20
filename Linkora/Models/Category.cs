using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    [Table("Category")]
    public class Category : NamedEntity
    {
        public int? ParentId { get; set; }
        public string? NameLV { get; set; }
        public string? NameEn { get; set; }
        public string? NameRU { get; set; }
        public int? Type { get; set; }
    }
}