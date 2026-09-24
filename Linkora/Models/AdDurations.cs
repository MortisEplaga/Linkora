namespace Linkora.Models
{
    public static class AdDurations
    {
        public static readonly int[] Options = [1, 3, 7, 14, 30];
        public static readonly int[] Accepted = [1, 3, 7, 14, 30, 60, 90];
        public const int Default = 30;
        public static bool IsOption(int days) => Options.Contains(days);
        public static bool IsAccepted(int days) => Accepted.Contains(days);
        public static string OptionsHint => string.Join(", ", Options);
    }
}