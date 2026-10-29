using System.Text.RegularExpressions;
using ClaudCare.Api.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace ClaudCare.Api.Data;

/// <summary>
/// ينشئ قاعدة بيانات المشروع إن لم تكن موجودة. لا يلمس أي قاعدة أخرى على الخادم
/// (خادم XAMPP يحوي قواعد مشاريع أخرى).
/// </summary>
public sealed partial class DatabaseBootstrapper(IConfiguration config, IOptions<DatabaseOptions> options, ILogger<DatabaseBootstrapper> logger)
{
    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex SafeName();

    public async Task EnsureDatabaseExistsAsync(CancellationToken ct = default)
    {
        if (!options.Value.CreateIfMissing) return;

        var builder = new MySqlConnectionStringBuilder(config.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default غير مضبوط."));
        var name = builder.Database;
        if (!SafeName().IsMatch(name))
            throw new InvalidOperationException($"اسم قاعدة البيانات غير صالح: '{name}'. المسموح: حروف وأرقام وشرطة سفلية فقط.");

        builder.Database = string.Empty;
        await using var conn = new MySqlConnection(builder.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
        await cmd.ExecuteNonQueryAsync(ct);
        logger.LogInformation("قاعدة البيانات '{Database}' جاهزة.", name);
    }
}
