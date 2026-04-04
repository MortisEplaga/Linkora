namespace Linkora.Models
{
    public class NotificationViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? FromUserId { get; set; }
        public string? FromUserName { get; set; }
        public string? FromUserAvatar { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductImage { get; set; }
        public string Message { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
