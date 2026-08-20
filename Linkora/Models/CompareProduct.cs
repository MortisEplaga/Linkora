namespace Linkora.Models
{
    public class CompareProduct : ProductSummaryBase
    {
        public string? Address { get; set; }
        public int MediaCount { get; set; }
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