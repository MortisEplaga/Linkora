namespace Linkora.Models
{
    public class Payment : PaymentBase
    {
        public decimal Price { get; set; }
        public string Currency { get; set; } = "EUR";
        public string? TransactionId { get; set; }
        public string Reference { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}