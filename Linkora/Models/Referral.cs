namespace Linkora.Models
{
    public class ReferralInfo
    {
        public string Code { get; set; } = "";
        public string Link { get; set; } = "";
        public int InvitedCount { get; set; }
        public int EarnedPoints { get; set; }
        public int PendingPoints { get; set; }
    }
    public static class ReferralCode
    {
        public const string CookieName = "linkora_ref";
        public const int CookieDays = 30;
        public const int MaxLength = 256;

        public static string? Clean(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var code = raw.Trim();
            if (code.Length > MaxLength) return null;

            foreach (var ch in code)
                if (char.IsControl(ch) || char.IsWhiteSpace(ch) || ch is '/' or '\\' or '?' or '#' or '&' or '=' or '%' or '"' or '\'' or '<' or '>' or ';' or ',' or '+')
                    return null;

            return code;
        }
        public static string BuildLink(string baseUrl, string code) => $"{baseUrl.TrimEnd('/')}/r/{Uri.EscapeDataString(code)}";
    }
}