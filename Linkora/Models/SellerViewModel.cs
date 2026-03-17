namespace Linkora.Models
{
    public class SellerViewModel
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? AvatarPath { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public bool IsCompany { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}