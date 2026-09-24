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
            [PointsLedgerEventType.EmailConfirmed] = (30, true, null, null, 0),
            [PointsLedgerEventType.ProfileCompleted] = (20, true, null, null, 0),
            [PointsLedgerEventType.ListingPosted] = (10, false, 10, 100, 7),
            [PointsLedgerEventType.ReferralFirstListing] = (50, false, 5, 250, 7),
            [PointsLedgerEventType.ReferralFiveListings] = (100, false, 5, 500, 7),
        };
    }
}