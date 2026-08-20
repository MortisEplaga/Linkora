namespace Linkora.Models
{
    public class NotificationViewModel : Base
    {
        public int UserId { get; set; }
        public int? FromUserId { get; set; }
        public string? FromUserName { get; set; }
        public string? FromUserAvatar { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductImage { get; set; }
        public string Text { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationPreferencesDto
    {
        public bool Deals { get; set; } = true;
        public bool Reviews { get; set; } = true;
        public bool Moderation { get; set; } = true;
        public bool Account { get; set; } = true;
        public bool Favourites { get; set; } = true;
        public bool NewListings { get; set; } = true;
    }

    public class NotificationPreferences : NotificationPreferencesDto
    {
        public int UserId { get; set; }
    }
}