namespace Linkora.Models
{
    public class ReviewRow
    {
        public int Rating { get; set; }
        public string Comment { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = "Unknown";
        public string? AvatarUrl { get; set; }
    }
}
