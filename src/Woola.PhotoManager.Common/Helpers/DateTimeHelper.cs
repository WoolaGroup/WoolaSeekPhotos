using System.Globalization;

namespace Woola.PhotoManager.Common.Helpers;

public static class DateTimeHelper
{
    public static string ToIsoString(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    public static DateTime FromIsoString(string? isoString)
    {
        if (string.IsNullOrEmpty(isoString))
            return DateTime.UtcNow;  // ← Valor por defecto

        return DateTime.Parse(isoString, null, DateTimeStyles.RoundtripKind);
    }
}