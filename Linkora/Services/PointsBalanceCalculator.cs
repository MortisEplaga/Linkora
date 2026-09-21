namespace Linkora.Services
{
    public sealed record PointsEvent(DateTime At, int Points, bool IsEarn);
    public static class PointsBalanceCalculator
    {
        public const int ExpiryMonths = 12;
        private sealed class Batch
        {
            public DateTime ExpiresAt;
            public int Remaining;
        }
        public static int Calculate(IEnumerable<PointsEvent> events, DateTime now)
        {
            var batches = new List<Batch>();

            foreach (var e in events.OrderBy(x => x.At).ThenByDescending(x => x.IsEarn))
            {
                batches.RemoveAll(b => b.ExpiresAt <= e.At);

                if (e.IsEarn)
                {
                    batches.Add(new Batch { ExpiresAt = e.At.AddMonths(ExpiryMonths), Remaining = e.Points });
                    continue;
                }

                var need = e.Points;
                foreach (var b in batches)
                {
                    var take = Math.Min(b.Remaining, need);
                    b.Remaining -= take;
                    need -= take;
                    if (need == 0) break;
                }
                batches.RemoveAll(b => b.Remaining == 0);
            }

            batches.RemoveAll(b => b.ExpiresAt <= now);
            return batches.Sum(b => b.Remaining);
        }
    }
}