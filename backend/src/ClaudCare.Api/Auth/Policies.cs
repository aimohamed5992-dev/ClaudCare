using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Auth;

/// <summary>أسماء السياسات والأدوار المستخدمة في [Authorize].</summary>
public static class Policies
{
    public const string Patient = nameof(Patient);
    public const string Doctor = nameof(Doctor);
    /// <summary>أي حساب طاقم (كل الأدوار عدا المريض).</summary>
    public const string Staff = nameof(Staff);
    public const string Admin = nameof(Admin);
    /// <summary>الإدارة العليا: مدير النظام ومدير المستشفى ومدير الفرع.</summary>
    public const string Management = nameof(Management);
    public const string Clinical = nameof(Clinical);

    public static readonly UserRole[] ManagementRoles = [UserRole.Admin, UserRole.Hospital, UserRole.Branch];
    public static readonly UserRole[] ClinicalRoles = [UserRole.Doctor, UserRole.Nurse, UserRole.Lab, UserRole.Radiology, UserRole.Pharmacy, UserRole.Emergency, UserRole.Admissions, UserRole.Surgery];

    public static string Roles(params UserRole[] roles) => string.Join(',', roles);
}

public static class ClaimNames
{
    public const string TokenUse = "token_use";
    public const string BranchId = "branch";
    public const string PasswordStamp = "pwd";
    public const string Purpose = "purpose";
    public const string OtpId = "otp";
}
