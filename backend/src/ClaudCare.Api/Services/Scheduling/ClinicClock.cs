using ClaudCare.Api.Configuration;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Services.Scheduling;

/// <summary>«الآن» بتوقيت العيادة (التواريخ والأوقات السريرية بتوقيت العيادة لا UTC).</summary>
public interface IClinicClock
{
    DateTime NowLocal { get; }
    DateOnly Today { get; }
    /// <summary>الدقائق منذ منتصف الليل بتوقيت العيادة.</summary>
    int NowMinutes { get; }
    /// <summary>الساعات المتبقية حتى بداية الموعد (سالبة إن مضى).</summary>
    double HoursUntil(DateOnly date, int startMinutes);
    /// <summary>بداية يوم العيادة (00:00 المحلي) كتوقيت UTC، لتجميع المعاملات حسب يوم العيادة.</summary>
    DateTime DayStartUtc(DateOnly date);
}

public sealed class ClinicClock(TimeProvider clock, IOptions<ClinicOptions> options) : IClinicClock
{
    private readonly TimeZoneInfo _tz = Resolve(options.Value);

    private static TimeZoneInfo Resolve(ClinicOptions o)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(o.TimeZone); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("clinic-fixed", TimeSpan.FromHours(o.FallbackUtcOffsetHours), "Clinic", "Clinic");
        }
    }

    public DateTime NowLocal => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), _tz).DateTime;
    public DateOnly Today => DateOnly.FromDateTime(NowLocal);
    public int NowMinutes => NowLocal.Hour * 60 + NowLocal.Minute;

    public DateTime DayStartUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), _tz);

    public double HoursUntil(DateOnly date, int startMinutes) =>
        (date.ToDateTime(TimeOnly.MinValue).AddMinutes(startMinutes) - NowLocal).TotalHours;
}
