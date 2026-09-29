namespace Linkora.Models
{
    public enum PointsLedgerEventType
    {
        EmailConfirmed,
        ProfileCompleted,
        ListingPosted,
        ReferralFirstListing,
        ReferralFiveListings
    }

    public enum PointsLedgerStatus
    {
        Pending,
        Available,
        Rejected
    }

    public class PointsLedgerEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int Points { get; set; }
        public PointsLedgerEventType EventType { get; set; }
        public PointsLedgerStatus Status { get; set; }
        public int? SourceUserId { get; set; }
        public int? SourceProductId { get; set; }
        public string MonthKey { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime AvailableAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }

    public class PointsSummary
    {
        public int Available { get; set; }
        public int Pending { get; set; }
    }

    public static class PointsLedgerRules
    {
        public static readonly Dictionary<PointsLedgerEventType, (int Points, bool OneTime, int? MonthlyCountCap, int? MonthlyPointsCap, int HoldDays)> Rules = new()
        {
            [PointsLedgerEventType.EmailConfirmed] = (300, true, null, null, 0),
            [PointsLedgerEventType.ProfileCompleted] = (200, true, null, null, 0),
            [PointsLedgerEventType.ListingPosted] = (100, false, 10, 1000, 7),
            [PointsLedgerEventType.ReferralFirstListing] = (500, false, 5, 2500, 7),
            [PointsLedgerEventType.ReferralFiveListings] = (1000, false, 5, 5000, 7),
        };
    }
}