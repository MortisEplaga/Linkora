namespace Linkora.Models
{
    public class NotificationDispatch
    {
        public int Id { get; set; }
        public int TargetUserId { get; set; }
        public int? FromUserId { get; set; }
        public int? ProductId { get; set; }
        public string Text { get; set; } = "";
        public string? ProductName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}