namespace Linkora.Models
{

    public sealed record SellerShipmentsViewModel(
        List<ShipmentRequestView> Requests,
        string? UserName,
        string? Phone,
        string? Email,
        string? HomeAddress);
}
