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
            [(PromotionTier.Highlight, PromotionTermType.Week)] = 740m,
            [(PromotionTier.Highlight, PromotionTermType.Month)] = 1240m,
            [(PromotionTier.Highlight, PromotionTermType.ThreeMonth)] = 3240m,
            [(PromotionTier.Highlight, PromotionTermType.Year)] = 9990m,
            [(PromotionTier.Top, PromotionTermType.Week)] = 1490m,
            [(PromotionTier.Top, PromotionTermType.Month)] = 3490m,
            [(PromotionTier.Top, PromotionTermType.ThreeMonth)] = 7490m,
            [(PromotionTier.Top, PromotionTermType.Year)] = 19990m,
            [(PromotionTier.Vip, PromotionTermType.Week)] = 2990m,
            [(PromotionTier.Vip, PromotionTermType.Month)] = 6990m,
            [(PromotionTier.Vip, PromotionTermType.ThreeMonth)] = 14990m,
            [(PromotionTier.Vip, PromotionTermType.Year)] = 39990m,
        };
    }
}