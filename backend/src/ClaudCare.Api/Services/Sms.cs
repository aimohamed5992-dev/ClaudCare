namespace ClaudCare.Api.Services;

/// <summary>إرسال رسالة نصية/بريد. التنفيذ الحقيقي (مزوّد SMS) يُستبدل هنا دون تغيير بقية الكود.</summary>
public interface IOtpSender
{
    Task SendAsync(string target, string message, CancellationToken ct = default);
}

/// <summary>للتطوير: يسجّل الرسالة في السجل بدل إرسالها فعليًا. لا يطبع الرمز خارج Development.</summary>
public sealed class LoggingOtpSender(ILogger<LoggingOtpSender> logger, IHostEnvironment env) : IOtpSender
{
    public Task SendAsync(string target, string message, CancellationToken ct = default)
    {
        if (env.IsDevelopment()) logger.LogInformation("[OTP→{Target}] {Message}", target, message);
        else logger.LogInformation("تم «إرسال» رسالة تحقق إلى {Target}", target[..Math.Min(4, target.Length)] + "•••");
        return Task.CompletedTask;
    }
}
