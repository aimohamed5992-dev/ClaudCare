namespace ClaudCare.Api.Infrastructure;

/// <summary>خطأ متوقع من منطق العمل: يُحوَّل إلى ProblemDetails بحالة HTTP ورمز خطأ ثابت يعتمد عليه العملاء.</summary>
public class ApiException(int status, string code, string message, IDictionary<string, object?>? extensions = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public IDictionary<string, object?>? Extensions { get; } = extensions;

    public static ApiException BadRequest(string code, string message) => new(StatusCodes.Status400BadRequest, code, message);
    public static ApiException Unauthorized(string code, string message) => new(StatusCodes.Status401Unauthorized, code, message);
    public static ApiException Forbidden(string code, string message) => new(StatusCodes.Status403Forbidden, code, message);
    public static ApiException NotFound(string code, string message) => new(StatusCodes.Status404NotFound, code, message);
    public static ApiException Conflict(string code, string message) => new(StatusCodes.Status409Conflict, code, message);
    public static ApiException Locked(string code, string message, int retryAfterSeconds) =>
        new(StatusCodes.Status423Locked, code, message, new Dictionary<string, object?> { ["retryAfterSeconds"] = retryAfterSeconds });
    public static ApiException TooMany(string code, string message, int retryAfterSeconds) =>
        new(StatusCodes.Status429TooManyRequests, code, message, new Dictionary<string, object?> { ["retryAfterSeconds"] = retryAfterSeconds });

    /// <summary>خطأ تحقق على حقول محددة (يُعرض بصيغة ValidationProblemDetails).</summary>
    public static ValidationApiException Validation(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class ValidationApiException(IDictionary<string, string[]> errors) : ApiException(StatusCodes.Status400BadRequest, "validation_failed", "بيانات غير صالحة")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
