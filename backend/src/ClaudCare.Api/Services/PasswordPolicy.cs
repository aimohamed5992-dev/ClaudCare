using System.Text.RegularExpressions;
using ClaudCare.Api.Configuration;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Services;

/// <summary>سياسة كلمة المرور الجديدة: 8 أحرف على الأقل، فيها حروف وأرقام، وليست شائعة. (تطابق شاشات التطبيقات.)</summary>
public sealed partial class PasswordPolicy(IOptions<AuthOptions> options)
{
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase) { "password1", "12345678a", "abc12345", "qwerty123", "password123", "admin1234" };

    [GeneratedRegex(@"[A-Za-z؀-ۿ]")] private static partial Regex Letter();
    [GeneratedRegex(@"\d")] private static partial Regex Digit();

    /// <summary>يعيد رسالة الخطأ أو null إن كانت صالحة.</summary>
    public string? Validate(string? password)
    {
        var p = password ?? string.Empty;
        var min = options.Value.MinPasswordLength;
        if (p.Length < min) return $"كلمة المرور {min} أحرف على الأقل";
        if (p.Length > 128) return "كلمة المرور طويلة جدًا";
        if (!Letter().IsMatch(p) || !Digit().IsMatch(p)) return "يجب أن تحتوي كلمة المرور على حروف وأرقام";
        if (Common.Contains(p)) return "كلمة المرور شائعة وسهلة التخمين";
        return null;
    }
}
