namespace ClaudCare.Api.Configuration;

public sealed class CorsOptions
{
    public const string Section = "Cors";
    public const string PolicyName = "Frontends";

    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>يسمح بأي منفذ على localhost (تطبيقا Flutter للويب يستخدمان منافذ عشوائية أثناء التطوير).</summary>
    public bool AllowAnyLocalhost { get; set; }
}
