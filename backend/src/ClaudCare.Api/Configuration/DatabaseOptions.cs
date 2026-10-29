namespace ClaudCare.Api.Configuration;

/// <summary>إعدادات قاعدة البيانات (XAMPP: MariaDB 10.4 عبر Pomelo).</summary>
public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>MariaDb أو MySql — يحدد نسخة الخادم التي يستهدفها Pomelo.</summary>
    public string ServerType { get; set; } = "MariaDb";

    public string ServerVersion { get; set; } = "10.4.28";

    /// <summary>إنشاء قاعدة البيانات تلقائيًا عند بدء التشغيل إن لم تكن موجودة.</summary>
    public bool CreateIfMissing { get; set; } = true;

    /// <summary>تطبيق الـ migrations تلقائيًا عند بدء التشغيل (للتطوير فقط؛ عطّله في الإنتاج).</summary>
    public bool MigrateOnStartup { get; set; } = true;

    public int CommandTimeoutSeconds { get; set; } = 30;
}
