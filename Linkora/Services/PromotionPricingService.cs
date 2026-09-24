using Linkora.Models;

namespace Linkora.Services
{
    public interface IPromotionPricingService
    {
        decimal GetPrice(PromotionTier tier, PromotionTermType termType);
        DateTime CalculateExpiry(PromotionTermType termType, DateTime from);
        (decimal Credit, decimal Payable) CalculateUpgrade(Promotion current, PromotionTier newTier, PromotionTermType newTermType, DateTime now);
        decimal CalculateListingBoostPayable(PromotionTier targetTier, Promotion? activeSubscription);
        int GetPointsEarned(decimal eurPaid);
        int GetPointsCost(decimal eurPrice);
    }

    public class PromotionPricingService : IPromotionPricingService
    {
        private const int PointsPerCentOfPrice = 10;
        public int GetPointsEarned(decimal eurPaid) => (int)Math.Round(eurPaid * 100m, MidpointRounding.AwayFromZero);
        public int GetPointsCost(decimal eurPrice) => GetPointsEarned(eurPrice) * PointsPerCentOfPrice;
        public decimal GetPrice(PromotionTier tier, PromotionTermType termType) => PromotionPrices.Prices[(tier, termType)];

        public DateTime CalculateExpiry(PromotionTermType termType, DateTime from) => termType switch
        {
            PromotionTermType.Week => from.AddDays(7),
            PromotionTermType.Month => from.AddMonths(1),
            PromotionTermType.ThreeMonth => from.AddMonths(3),
            PromotionTermType.Year => from.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(nameof(termType))
        };

        public (decimal Credit, decimal Payable) CalculateUpgrade(Promotion current, PromotionTier newTier, PromotionTermType newTermType, DateTime now)
        {
            var totalDays = (CalculateExpiry(current.TermType, current.StartedAt) - current.StartedAt).Days;
            var remainingDays = Math.Max(0, (current.ExpiresAt - now).Days);
            var credit = totalDays > 0 ? current.PricePaid * remainingDays / totalDays : 0m;
            var payable = Math.Max(0m, GetPrice(newTier, newTermType) - credit);
            return (Math.Round(credit, 2), Math.Round(payable, 2));
        }
        public decimal CalculateListingBoostPayable(PromotionTier targetTier, Promotion? activeSubscription)
        {
            if (activeSubscription != null && activeSubscription.Tier >= targetTier) return 0m;

            var basePrice = activeSubscription != null ? GetPrice(activeSubscription.Tier, PromotionTermType.Week) : 0m;

            return Math.Max(0m, GetPrice(targetTier, PromotionTermType.Week) - basePrice);
        }
    }
}