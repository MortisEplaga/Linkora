namespace Linkora.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string PurposeType { get; set; } = "";
        public int? ProductId { get; set; }
        public string? PromotionType { get; set; }
        public string? SubscriptionType { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string? TransactionId { get; set; }
        public string Reference { get; set; } = "";
        public string Status { get; set; } = "Created";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}