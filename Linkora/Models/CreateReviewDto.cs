namespace Linkora.Models
{
    public class CreateReviewDto
    {
        public int TargetUserId { get; set; }
        public int ProductId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}
