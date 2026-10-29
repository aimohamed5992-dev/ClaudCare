namespace ClaudCare.Api.Configuration;

public sealed class ClinicOptions
{
    public const string Section = "Clinic";
    /// <summary>منطقة توقيت العيادات (السعودية UTC+3 بلا توقيت صيفي).</summary>
    public string TimeZone { get; set; } = "Asia/Riyadh";
    /// <summary>إزاحة احتياطية بالساعات إن لم تتوفر قاعدة المناطق الزمنية على الجهاز.</summary>
    public int FallbackUtcOffsetHours { get; set; } = 3;
}

public sealed class BookingOptions
{
    public const string Section = "Booking";
    /// <summary>أقل مهلة بين الآن وبداية الموعد (دقائق).</summary>
    public int MinLeadMinutes { get; set; } = 0;
    public int MaxAdvanceDays { get; set; } = 60;
    public int DefaultAvailabilityDays { get; set; } = 14;
    /// <summary>الإلغاء المجاني قبل هذا العدد من الساعات؛ بعده تُطبَّق رسوم.</summary>
    public int FreeCancellationHours { get; set; } = 24;
    public int LateCancellationFeePercent { get; set; } = 50;
}
