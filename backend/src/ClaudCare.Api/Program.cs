using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using ClaudCare.Api.Auth;
using ClaudCare.Api.Configuration;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.Configure<DatabaseOptions>(cfg.GetSection(DatabaseOptions.Section));
builder.Services.Configure<JwtOptions>(cfg.GetSection(JwtOptions.Section));
builder.Services.Configure<AuthOptions>(cfg.GetSection(AuthOptions.Section));
builder.Services.Configure<RateLimitOptions>(cfg.GetSection(RateLimitOptions.Section));
builder.Services.Configure<SeedOptions>(cfg.GetSection(SeedOptions.Section));
builder.Services.Configure<ClinicOptions>(cfg.GetSection(ClinicOptions.Section));
builder.Services.Configure<BookingOptions>(cfg.GetSection(BookingOptions.Section));

var dbOptions = cfg.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
var corsOptions = cfg.GetSection(CorsOptions.Section).Get<CorsOptions>() ?? new CorsOptions();
var jwt = cfg.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
var authOptions = cfg.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
var rateLimit = cfg.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();

// ---- فحوص أمان عند بدء التشغيل: لا نقلع بإعدادات خطرة خارج بيئة التطوير ----
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey مطلوب بطول 32 بايت على الأقل (مرّره عبر متغير البيئة Jwt__SigningKey).");
if (!builder.Environment.IsDevelopment())
{
    if (jwt.SigningKey.StartsWith("dev-only", StringComparison.Ordinal))
        throw new InvalidOperationException("مفتاح التوقيع التجريبي غير مسموح خارج بيئة التطوير.");
    if (!string.IsNullOrEmpty(authOptions.FixedOtpCode))
        throw new InvalidOperationException("Auth:FixedOtpCode مسموح في بيئة التطوير فقط.");
    if (cfg.GetSection(SeedOptions.Section).Get<SeedOptions>()?.DemoAccounts == true)
        throw new InvalidOperationException("Seed:DemoAccounts مسموح في بيئة التطوير فقط.");
}

// ---- قاعدة البيانات: MySQL/MariaDB (XAMPP) عبر Pomelo ----
var connectionString = cfg.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default غير مضبوط.");
var serverVersion = Version.Parse(dbOptions.ServerVersion);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ClaudCareDbContext>(o => o.UseMySql(
    connectionString,
    dbOptions.ServerType.Equals("MariaDb", StringComparison.OrdinalIgnoreCase) ? new MariaDbServerVersion(serverVersion) : new MySqlServerVersion(serverVersion),
    my =>
    {
        my.EnableRetryOnFailure(maxRetryCount: 3);
        my.CommandTimeout(dbOptions.CommandTimeoutSeconds);
    }));
builder.Services.AddScoped<DatabaseBootstrapper>();
builder.Services.AddScoped<DevSeeder>();
builder.Services.AddScoped<DemoDataSeeder>();

// ---- المصادقة والصلاحيات ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<PasswordPolicy>();
builder.Services.AddSingleton<IOtpSender, LoggingOtpSender>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<ClaudCare.Api.Services.Scheduling.IClinicClock, ClaudCare.Api.Services.Scheduling.ClinicClock>();
builder.Services.AddScoped<ClaudCare.Api.Services.Scheduling.AvailabilityEngine>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<DoctorCatalogService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.StaffScope>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.LabService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.RadiologyService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.PharmacyService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.NursingService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.AdmissionsService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.EmergencyService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.ReceptionService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.SurgeryService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.HrService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.FinanceService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.InsuranceService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Operations.DashboardService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Billing.BillingService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Communication.NotificationCenter>();
builder.Services.AddScoped<ClaudCare.Api.Services.Communication.MessagingService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Communication.AccountService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Clinical.PatientAccessService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Clinical.PatientRecordService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Clinical.ConsultationService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Clinical.MedicationService>();
builder.Services.AddScoped<ClaudCare.Api.Services.Clinical.OrdersService>();
builder.Services.AddScoped<ReferenceDataSeeder>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<JwtOptions>, TimeProvider>((o, jwtOpt, clock) =>
{
    o.MapInboundClaims = false; // نحتفظ بأسماء المطالبات كما أنشأناها
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwtOpt.Value.Issuer,
        ValidAudience = jwtOpt.Value.Audience, // رموز التحدي والاستعادة لها جمهور مختلف فتُرفض هنا
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpt.Value.SigningKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
        LifetimeValidator = JwtLifetime.Validator(clock),
    };
});

builder.Services.AddAuthorizationBuilder()
    // آمن افتراضيًا: كل نقطة نهاية تتطلب تسجيل دخول ما لم تُوسَم بـ [AllowAnonymous]
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireClaim(ClaimNames.TokenUse, "access").Build())
    .AddPolicy(Policies.Patient, p => p.RequireRole(nameof(UserRole.Patient)))
    .AddPolicy(Policies.Doctor, p => p.RequireRole(nameof(UserRole.Doctor)))
    .AddPolicy(Policies.Admin, p => p.RequireRole(nameof(UserRole.Admin)))
    .AddPolicy(Policies.Staff, p => p.RequireAuthenticatedUser().RequireAssertion(c => !c.User.IsInRole(nameof(UserRole.Patient))))
    .AddPolicy(Policies.Management, p => p.RequireRole(Policies.ManagementRoles.Select(r => r.ToString())))
    .AddPolicy(Policies.Clinical, p => p.RequireRole(Policies.ClinicalRoles.Select(r => r.ToString())));

// الحد الأقصى لجسم الطلب (لا رفع ملفات بعد): 1MB
const long MaxBodyBytes = 1_048_576;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxBodyBytes);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // سقف عام: المستخدم المسجَّل بمعرّفه، والزائر بعنوان IP؛ يمنع إغراق الخدمة بطلبات متتالية
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } uid ? "u:" + uid : "ip:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, ctx.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value.GlobalPermitPerMinute), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy(RateLimitOptions.AuthPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = ctx.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value.AuthPermitPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "طلبات كثيرة. حاول بعد قليل.",
            Extensions = { ["code"] = "rate_limited", ["retryAfterSeconds"] = 60 },
        }, ct);
    };
});

// ---- الويب: CORS لتطبيق React وتطبيقي Flutter (ويب) ----
builder.Services.AddCors(o => o.AddPolicy(CorsOptions.PolicyName, p =>
{
    p.SetIsOriginAllowed(origin =>
        corsOptions.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
        || (corsOptions.AllowAnyLocalhost && Uri.TryCreate(origin, UriKind.Absolute, out var u) && (u.Host is "localhost" or "127.0.0.1")))
     .AllowAnyHeader()
     .AllowAnyMethod()
     .WithExposedHeaders("Retry-After");
}));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter())) // التعدادات نصوص في JSON
    .ConfigureApiBehaviorOptions(o =>
    o.InvalidModelStateResponseFactory = ctx => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(ctx.ModelState)
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "بيانات غير صالحة",
        Instance = ctx.HttpContext.Request.Path,
        Extensions = { ["code"] = "validation_failed" },
    }));
builder.Services.AddOpenApi();

var app = builder.Build();

// إنشاء قاعدة البيانات وتطبيق الـ migrations والبيانات التجريبية (لا يؤثر على قواعد الخادم الأخرى)
await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<DatabaseBootstrapper>().EnsureDatabaseExistsAsync();
        if (dbOptions.MigrateOnStartup)
            await scope.ServiceProvider.GetRequiredService<ClaudCareDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "تعذّر تجهيز قاعدة البيانات. تأكد من تشغيل MySQL في XAMPP. سيعمل الـ API ويُرجع 503 في /api/health.");
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment()) app.UseHsts();

// ترويسات أمان على كل الردود + منع التخزين المؤقت لبيانات الـ API + رفض الأجسام الكبيرة مبكرًا
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (ctx.Request.Path.StartsWithSegments("/api")) { h.CacheControl = "no-store"; h.Pragma = "no-cache"; }
    if (ctx.Request.ContentLength > MaxBodyBytes)
    {
        ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await ctx.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 413, Title = "حجم الطلب كبير جدًا", Extensions = { ["code"] = "payload_too_large" } });
        return;
    }
    await next();
});
app.UseCors(CorsOptions.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(o => o.WithTitle("ClaudCare API")).AllowAnonymous();
}

app.MapControllers();
app.Run();

/// <summary>مكشوفة لاختبارات التكامل (WebApplicationFactory).</summary>
public partial class Program;
