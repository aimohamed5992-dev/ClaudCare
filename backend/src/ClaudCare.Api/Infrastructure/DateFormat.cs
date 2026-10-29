using System.Globalization;

namespace ClaudCare.Api.Infrastructure;

/// <summary>
/// تنسيق التواريخ بالثقافة الثابتة دائمًا. ثقافة الجهاز قد تكون عربية بتقويم هجري فتتغير الأرقام (1448 بدل 2026).
/// </summary>
public static class DateFormat
{
    public static string Iso(this DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Compact(this DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
