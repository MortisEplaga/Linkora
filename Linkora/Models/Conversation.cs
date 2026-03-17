using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Linkora.Models
{
    [Table("Conversations")]
    public class Conversation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int? ProductId { get; set; }
        public int BuyerId { get; set; }
        public int SellerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSystem { get; set; }

        [NotMapped] public string? ProductName { get; set; }
        [NotMapped] public string? ProductImage { get; set; }
        [NotMapped] public string? OtherUserName { get; set; }
        [NotMapped] public string? OtherUserAvatar { get; set; }
        [NotMapped] public int OtherUserId { get; set; }
        [NotMapped] public string? LastMessage { get; set; }
        [NotMapped] public DateTime? LastMessageAt { get; set; }
        [NotMapped] public int UnreadCount { get; set; }
    }

    [Table("Messages")]
    public class Message
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public int? SenderId { get; set; }
        [Required]
        [StringLength(2000)]
        public string Text { get; set; } = "";
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }

        [NotMapped] public string? SenderName { get; set; }
        [NotMapped] public string? SenderAvatar { get; set; }
    }
}