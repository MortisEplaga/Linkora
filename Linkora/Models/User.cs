using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string UserName { get; set; }

        [StringLength(256)]
        public string Email { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        [StringLength(500)]
        public string Address { get; set; }

        [StringLength(50)]
        public string Role { get; set; }

        [StringLength(500)]
        public string AvatarUrl { get; set; }
        public string? PasswordHash { get; set; }
        public bool IsCompany { get; set; }
        public bool EmailConfirmed { get; set; }

        [StringLength(100)]
        public string? ConfirmationToken { get; set; }
        public string? FacebookId { get; set; }
        public int? PreferredAdDuration { get; set; }
        public string SubscriptionType { get; set; } = "Free";
        public int PromotionPoints { get; set; }
        [StringLength(500)]
        public string? TelegramUrl { get; set; }

        [StringLength(500)]
        public string? WhatsAppUrl { get; set; }

        [StringLength(500)]
        public string? WebsiteUrl { get; set; }
        [StringLength(500)]
        public string? HomeAddress { get; set; }
        public decimal? HomeLat { get; set; }
        public decimal? HomeLng { get; set; }

    }
    public class FacebookLoginModel
    {
        public string AccessToken { get; set; }
        public string ReturnUrl { get; set; }
    }

    public class FacebookUserInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
    public class UserSession
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime LoginAt { get; set; }
        public DateTime? LogoutAt { get; set; }
    }
}