using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Linkora.Repositories
{
    public static class SqlDataReaderExtensions
    {
        public static string? GetStringOrNull(this SqlDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetString(ordinal);
        public static string GetStringOrDefault(this SqlDataReader r, int ordinal, string defaultValue = "") => r.IsDBNull(ordinal) ? defaultValue : r.GetString(ordinal);
        public static int? GetInt32OrNull(this SqlDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetInt32(ordinal);
        public static int GetInt32OrDefault(this SqlDataReader r, int ordinal, int defaultValue = 0) => r.IsDBNull(ordinal) ? defaultValue : r.GetInt32(ordinal);
        public static decimal? GetDecimalOrNull(this SqlDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetDecimal(ordinal);
        public static DateTime? GetDateTimeOrNull(this SqlDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetDateTime(ordinal);
        public static bool GetBooleanOrDefault(this SqlDataReader r, int ordinal, bool defaultValue = false) => r.IsDBNull(ordinal) ? defaultValue : r.GetBoolean(ordinal);
        public static double GetDoubleOrDefault(this SqlDataReader r, int ordinal, double defaultValue = 0.0) => r.IsDBNull(ordinal) ? defaultValue : r.GetDouble(ordinal);
    }
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user) => int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        public static bool TryGetUserId(this ClaimsPrincipal user, out int userId) =>  int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);
    }
}