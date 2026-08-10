namespace Linkora.Models
{
    public class ProductCardViewModel
    {
        public Product Product { get; set; } = null!;
        public bool ShowFavActions { get; set; } = true;
        public bool ShowSeller { get; set; } = true;
        public bool IsFav { get; set; }
        public bool IsInCart { get; set; }
    }
}