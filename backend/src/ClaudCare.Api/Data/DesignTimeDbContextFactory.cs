using ClaudCare.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClaudCare.Api.Data;

/// <summary>
/// يستخدمه `dotnet ef` لإنشاء السياق دون تشغيل التطبيق (فلا يُنفَّذ أي كود بدء تشغيل أثناء الـ migrations).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ClaudCareDbContext>
{
    public ClaudCareDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Server=127.0.0.1;Port=3306;Database=claudcare;User=root;Password=;CharSet=utf8mb4;";
        var opts = new DbContextOptionsBuilder<ClaudCareDbContext>()
            .UseMySql(cs, new MariaDbServerVersion(new Version(new DatabaseOptions().ServerVersion)))
            .Options;
        return new ClaudCareDbContext(opts, TimeProvider.System);
    }
}
