using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    [Table("Category")]
    public class BaseCat : NamedEntity
    {
        public string? NameLV { get; set; }
        public string? NameEn { get; set; }
        public string? NameRU { get; set; }
    }
    public class Category : BaseCat
    {
        public int? ParentId { get; set; }
        public bool? HasPrice { get; set; }
    }
    public class Param : BaseCat
    {
        public int? CategoryId { get; set; }
        public int? Type { get; set; }
    }

}