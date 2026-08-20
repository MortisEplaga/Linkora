namespace Linkora.Models
{
    public class PaymentRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = "";
        public string Purpose { get; set; } = "";
        public int? ProductId { get; set; }
        public string? PromotionType { get; set; }
        public string? SubscriptionType { get; set; }
    }
}
