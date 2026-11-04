using System.Text.Json;

namespace Linkora.Services
{
    public static class NotificationCategorizer
    {
        public static string Categorize(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    return type switch
                    {
                        "deal_sold" or "deal_bought" or "subscription_sold" or "shipment_requested" or "shipment_accepted" or "shipment_declined"
                            or "shipment_arrived" or "shipment_delivered" or "shipment_returned" or "shipment_expired" or "shipment_accept_expired"
                            or "shipment_cancelled" => "Deals",
                        "review_received" => "Reviews",
                        "product_approved" or "parameter_approved" or "parameter_rejected" or "rejected_reason" or "report_on_product" => "Moderation",
                        "user_banned" or "user_unbanned" => "Account",
                        "favourite_updated" or "favourite_archived_ban" or "subscription_seller_banned" => "Favourites",
                        "listing_expiring_soon" => "ExpiringSoon",
                        _ => "NewListings"
                    };
                }
            }
            catch (JsonException) {}
            return "NewListings";
        }
    }
}