using System.Text;
using System.Text.RegularExpressions;

namespace ClaudCare.Api.Services;

/// <summary>توحيد الجوال السعودي إلى الصيغة المحلية 05XXXXXXXX، وأدوات إخفاء بيانات الاتصال للعرض.</summary>
public static partial class PhoneNormalizer
{
    [GeneratedRegex(@"^05\d{8}$")] private static partial Regex Local();
    [GeneratedRegex(@"^[12]\d{9}$")] private static partial Regex NationalIdRx();

    /// <summary>يحوّل الأرقام العربية/الفارسية إلى لاتينية.</summary>
    public static string Digits(string s)
    {
        const string ar = "٠١٢٣٤٥٦٧٨٩", fa = "۰۱۲۳۴۵۶۷۸۹";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            var i = ar.IndexOf(c);
            if (i < 0) i = fa.IndexOf(c);
            sb.Append(i >= 0 ? (char)('0' + i) : c);
        }
        return sb.ToString();
    }

    /// <summary>يعيد 05XXXXXXXX أو null إن لم يكن رقمًا سعوديًا صالحًا.</summary>
    public static string? TryNormalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = new string(Digits(input).Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (s.StartsWith("+966")) s = "0" + s[4..];
        else if (s.StartsWith("00966")) s = "0" + s[5..];
        else if (s.StartsWith("966")) s = "0" + s[3..];
        else if (s.Length == 9 && s[0] == '5') s = "0" + s;
        return Local().IsMatch(s) ? s : null;
    }

    public static string? TryNationalId(string? input)
    {
        var s = Digits((input ?? string.Empty).Trim());
        return NationalIdRx().IsMatch(s) ? s : null;
    }

    /// <summary>05•• ••• 67</summary>
    public static string MaskPhone(string phone) => phone.Length >= 2 ? $"05•• ••• {phone[^2..]}" : "جوالك المسجّل";

    /// <summary>m•••@hospital.com</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "بريدك المسجّل" : $"{email[0]}•••{email[at..]}";
    }
}
