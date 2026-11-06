namespace Linkora.Models
{
    /// <summary>
    /// Model of the seller's delivery requests page.
    /// The sender fields of the accept form are prefilled from the profile (name, phone, e-mail, home address)
    /// and from the listing address, so the seller only has to check them.
    /// </summary>
    public sealed record SellerShipmentsViewModel(
        List<ShipmentRequestView> Requests,
        string? UserName,
        string? Phone,
        string? Email,
        string? HomeAddress);
}
