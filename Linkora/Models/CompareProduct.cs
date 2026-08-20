namespace Linkora.Models
{
    public class CompareProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Address { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? AvatarUrl { get; set; }
        public int MediaCount { get; set; }
        public decimal? Price { get; set; }
        public string? CategoryName { get; set; }
        public string? SellerName { get; set; }
    }
    public class CompareData
    {
        public List<CompareProduct> Products { get; set; } = [];
        public List<int> AllParamIds { get; set; } = [];
        public Dictionary<int, string> ParamLabels { get; set; } = [];
        public Dictionary<int, Dictionary<int, string>> ParamMatrix { get; set; } = [];
    }
}