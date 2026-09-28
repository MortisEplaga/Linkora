namespace Linkora.Models
{
    public record ImportListing(Product Product, Dictionary<int, string> ParamValues, int PublishDurationDays, List<ProductMedia> Media);
    public record ExportListing(int Id, string Title, string? Description, int? Qty, string? Address, decimal? Price, int PublishDurationDays, Dictionary<int, string> ParamValues, List<string> PhotoUrls);
}