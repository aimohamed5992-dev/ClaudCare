using ClaudCare.Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using MySqlConnector;

namespace ClaudCare.Api.Tests.Infrastructure;

/// <summary>
/// يشغّل الـ API كاملًا في الذاكرة على قاعدة اختبار مستقلة (claudcare_test) في نفس خادم XAMPP.
/// تُنشأ القاعدة تلقائيًا وتُحذف عند انتهاء الاختبارات؛ لا تلمس قاعدة التطوير.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestDatabase = "claudcare_test";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("CLAUDCARE_TEST_DB")
        ?? $"Server=127.0.0.1;Port=3306;Database={TestDatabase};User=root;Password=;CharSet=utf8mb4;";

    public ApiFactory()
    {
        // متغيرات البيئة تُقرأ قبل Program.cs فتُطبَّق على كل الإعدادات
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // حد الطلبات مرتفع افتراضيًا كي لا تتأثر الاختبارات المتتابعة؛ اختبار التقييد يخفضه صراحة
        // لا بيانات عرض في قاعدة الاختبار المشتركة: كل اختبار ينشئ بياناته (واختبار المزروعات يتراجع عن عمله)
        builder.UseSetting($"{SeedOptions.Section}:{nameof(SeedOptions.DemoAccounts)}", "false");
        builder.UseSetting($"{RateLimitOptions.Section}:{nameof(RateLimitOptions.AuthPermitPerMinute)}", "100000");
        builder.UseSetting($"{RateLimitOptions.Section}:{nameof(RateLimitOptions.GlobalPermitPerMinute)}", "1000000");
    }

    /// <summary>نسخة بساعة مزيّفة قابلة للتقديم (لاختبار انتهاء الصلاحيات والإقفال).</summary>
    public (WebApplicationFactory<Program> Factory, FakeTimeProvider Clock) WithFakeClock(DateTimeOffset? start = null)
    {
        var clock = new FakeTimeProvider(start ?? DateTimeOffset.UtcNow);
        var f = WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))));
        return (f, clock);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try
        {
            var b = new MySqlConnectionStringBuilder(ConnectionString);
            if (b.Database != TestDatabase) return; // لا نحذف إلا قاعدة الاختبار المعروفة
            b.Database = string.Empty;
            using var conn = new MySqlConnection(b.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{TestDatabase}`";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // تنظيف اختياري
        }
    }
}
