using ClaudCare.Api.Auth;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// نطاق الطاقم التشغيلي: الأدوار المسموحة لكل عملية، ونطاق الفرع. الإدارة العليا (النظام/المستشفى) تتجاوز قيد الفرع وتدخل كل العمليات للإشراف.
/// </summary>
public sealed class StaffScope(ICurrentUser me)
{
    public bool IsGlobal => me.Role is UserRole.Admin or UserRole.Hospital;
    public UserRole Role => me.Role;
    public int UserId => me.Id;

    /// <summary>يُسمح بالأدوار المذكورة والإدارة العليا؛ غيرهم 403.</summary>
    public void Require(params UserRole[] roles)
    {
        if (!IsGlobal && !roles.Contains(me.Role)) throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية هذا الإجراء.");
    }

    /// <summary>الفرع المقيِّد للاستعلامات: null للإدارة العليا (الكل)، وإلا فرع الموظف (وبلا فرع ⇒ لا شيء).</summary>
    public int? BranchFilter => IsGlobal ? null : me.BranchId ?? throw ApiException.Forbidden("no_branch", "حسابك غير مرتبط بفرع.");

    /// <summary>الفرع الذي يُنشأ فيه سجل جديد.</summary>
    public int BranchForNew(int? requested = null) => IsGlobal ? requested ?? me.BranchId ?? throw ApiException.Validation("branchId", "حدّد الفرع") : BranchFilter!.Value;

    public bool InBranch(int? entityBranch) => BranchFilter is not { } b || entityBranch == b;
}
