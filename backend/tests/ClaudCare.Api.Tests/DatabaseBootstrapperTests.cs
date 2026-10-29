using ClaudCare.Api.Configuration;
using ClaudCare.Api.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Tests;

public class DatabaseBootstrapperTests
{
    private static DatabaseBootstrapper Create(string connectionString, bool create = true)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString }).Build();
        return new DatabaseBootstrapper(config, Options.Create(new DatabaseOptions { CreateIfMissing = create }), NullLogger<DatabaseBootstrapper>.Instance);
    }

    // خادم وهمي غير قابل للوصول: لو تجاوز الفحص ووصل للاتصال لفشل بغير InvalidOperationException ولم نلمس خادم XAMPP أبدًا
    [Theory]
    [InlineData("bad`name")]
    [InlineData("a b")]
    [InlineData("my-db")]
    [InlineData("نظام")]
    public async Task Rejects_unsafe_database_names_before_touching_the_server(string name)
    {
        var b = Create($"Server=203.0.113.1;Port=1;Database={name};User=x;Password=;ConnectionTimeout=1;");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => b.EnsureDatabaseExistsAsync());
        Assert.Contains("غير صالح", ex.Message);
    }

    [Fact]
    public async Task Accepts_a_safe_name_and_then_tries_to_connect()
    {
        // اسم سليم + خادم وهمي: يتجاوز الفحص ويفشل عند الاتصال (وليس بـ InvalidOperationException الخاص بالاسم)
        var b = Create("Server=203.0.113.1;Port=1;Database=claudcare_x1;User=x;Password=;ConnectionTimeout=1;");
        var ex = await Record.ExceptionAsync(() => b.EnsureDatabaseExistsAsync());
        Assert.NotNull(ex);
        Assert.DoesNotContain("غير صالح", ex!.Message);
    }

    [Fact]
    public async Task Does_nothing_when_creation_is_disabled()
    {
        // خادم غير موجود: لو حاول الاتصال لفشل الاختبار
        var b = Create("Server=203.0.113.1;Port=1;Database=whatever;User=x;Password=;ConnectionTimeout=1;", create: false);
        await b.EnsureDatabaseExistsAsync();
    }
}
