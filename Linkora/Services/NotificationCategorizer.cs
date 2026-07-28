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
                        "deal_sold" or "deal_bought" or "subscription_sold" => "Deals",
                        "review_received" => "Reviews",
                        "product_approved" or "parameter_approved" or "parameter_rejected"
                            or "rejected_reason" or "report_on_product" => "Moderation",
                        "user_banned" or "user_unbanned" => "Account",
                        "favourite_updated" or "favourite_archived_ban"
                            or "subscription_seller_banned" => "Favourites",
                        _ => "NewListings"
                    };
                }
            }
            catch (JsonException)
            {
            }
            return "NewListings";
        }
    }
}