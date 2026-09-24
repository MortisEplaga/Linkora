namespace Linkora.Models
{
    public class PromotionPrices
    {
        public static readonly Dictionary<(PromotionTier, PromotionTermType), decimal> Prices = new()
        {
            [(PromotionTier.Highlight, PromotionTermType.Week)] = 0.74m,
            [(PromotionTier.Highlight, PromotionTermType.Month)] = 1.24m,
            [(PromotionTier.Highlight, PromotionTermType.ThreeMonth)] = 3.24m,
            [(PromotionTier.Highlight, PromotionTermType.Year)] = 9.99m,
            [(PromotionTier.Top, PromotionTermType.Week)] = 1.49m,
            [(PromotionTier.Top, PromotionTermType.Month)] = 3.49m,
            [(PromotionTier.Top, PromotionTermType.ThreeMonth)] = 7.49m,
            [(PromotionTier.Top, PromotionTermType.Year)] = 19.99m,
            [(PromotionTier.Vip, PromotionTermType.Week)] = 2.99m,
            [(PromotionTier.Vip, PromotionTermType.Month)] = 6.99m,
            [(PromotionTier.Vip, PromotionTermType.ThreeMonth)] = 14.99m,
            [(PromotionTier.Vip, PromotionTermType.Year)] = 39.99m,
        };
        public static readonly Dictionary<(PromotionTier, PromotionTermType), decimal> PointPrices = new()
        {
            [(PromotionTier.Highlight, PromotionTermType.Week)] = 74m,
            [(PromotionTier.Highlight, PromotionTermType.Month)] = 124m,
            [(PromotionTier.Highlight, PromotionTermType.ThreeMonth)] = 324m,
            [(PromotionTier.Highlight, PromotionTermType.Year)] = 999m,
            [(PromotionTier.Top, PromotionTermType.Week)] = 149m,
            [(PromotionTier.Top, PromotionTermType.Month)] = 349m,
            [(PromotionTier.Top, PromotionTermType.ThreeMonth)] = 749m,
            [(PromotionTier.Top, PromotionTermType.Year)] = 1999m,
            [(PromotionTier.Vip, PromotionTermType.Week)] = 2299m,
            [(PromotionTier.Vip, PromotionTermType.Month)] = 699m,
            [(PromotionTier.Vip, PromotionTermType.ThreeMonth)] = 1499m,
            [(PromotionTier.Vip, PromotionTermType.Year)] = 3999m,
        };
    }
}