using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Infrastructure;

/// <summary>
/// يحوّل الاستثناءات إلى استجابات ProblemDetails موحدة: الأخطاء المتوقعة (ApiException) بحالتها ورمزها،
/// وأي استثناء آخر 500 دون تسريب تفاصيل داخلية.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        ProblemDetails problem;
        switch (ex)
        {
            case ValidationApiException v:
                problem = new ValidationProblemDetails(v.Errors) { Status = v.Status, Title = v.Message };
                break;
            case ApiException api:
                logger.LogWarning("{Code} ({Status}) في {Method} {Path}", api.Code, api.Status, ctx.Request.Method, ctx.Request.Path);
                problem = new ProblemDetails { Status = api.Status, Title = api.Message };
                if (api.Extensions is not null)
                    foreach (var (k, val) in api.Extensions) problem.Extensions[k] = val;
                break;
            default:
                logger.LogError(ex, "استثناء غير معالَج في {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "حدث خطأ غير متوقع",
                    Detail = env.IsDevelopment() ? ex.Message : "تعذّر إكمال الطلب. حاول مرة أخرى لاحقًا.",
                };
                break;
        }

        problem.Instance = ctx.Request.Path;
        problem.Extensions["traceId"] = ctx.TraceIdentifier;
        problem.Extensions["code"] = ex is ApiException a ? a.Code : "server_error";

        ctx.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        if (ex is ApiException { Extensions: not null } ae && ae.Extensions.TryGetValue("retryAfterSeconds", out var ra) && ra is int sec)
            ctx.Response.Headers.RetryAfter = sec.ToString();
        await ctx.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
