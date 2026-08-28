namespace Linkora.Models
{
    public class ProfileSaveDto
    {
        public string UserName { get; set; } = "";
        public string? Phone { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public int? PreferredAdDuration { get; set; }
        public string? SubscriptionType { get; set; }
        public string? TelegramUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? WebsiteUrl { get; set; }
    }

}
