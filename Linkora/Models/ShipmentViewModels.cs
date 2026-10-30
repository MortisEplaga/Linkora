namespace Linkora.Models
{
    /// <summary>Model of the seller's delivery requests page.</summary>
    public sealed record SellerShipmentsViewModel(List<ShipmentRequestView> Requests, string? Phone, string? Email);
}
