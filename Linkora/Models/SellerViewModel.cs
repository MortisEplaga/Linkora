namespace Linkora.Models
{
    public class SellerViewModel
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsCompany { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}