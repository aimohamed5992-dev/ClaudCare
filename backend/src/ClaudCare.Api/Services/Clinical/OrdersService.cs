using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Clinical;

/// <summary>طلبات التحاليل والأشعة بنطاق الدور: المريض طلباته، الطبيب ما طلبه، المختبر/الأشعة فرعهم، الإدارة الكل.</summary>
public sealed class OrdersService(ClaudCareDbContext db, ICurrentUser me)
{
    private IQueryable<LabOrder> LabScope()
    {
        var q = db.LabOrders.AsNoTracking();
        return me.Role switch
        {
            UserRole.Patient => q.Where(o => o.PatientId == me.Id),
            UserRole.Doctor => q.Where(o => o.DoctorId == me.Id),
            UserRole.Lab or UserRole.Nurse => me.BranchId is { } b ? q.Where(o => o.BranchId == b) : q.Where(_ => false),
            UserRole.Admin or UserRole.Hospital => q,
            _ => throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية عرض طلبات المختبر."),
        };
    }

    private IQueryable<ImagingOrder> ImagingScope()
    {
        var q = db.ImagingOrders.AsNoTracking();
        return me.Role switch
        {
            UserRole.Patient => q.Where(o => o.PatientId == me.Id),
            UserRole.Doctor => q.Where(o => o.DoctorId == me.Id),
            UserRole.Radiology => me.BranchId is { } b ? q.Where(o => o.BranchId == b) : q.Where(_ => false),
            UserRole.Admin or UserRole.Hospital => q,
            _ => throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية عرض طلبات الأشعة."),
        };
    }

    public async Task<PagedResult<LabOrderDto>> LabAsync(LabOrderStatus? status, int? patientId, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = LabScope();
        if (status is { } s) q = q.Where(o => o.Status == s);
        if (patientId is { } pid) q = q.Where(o => o.PatientId == pid);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(o => o.Items).Include(o => o.Doctor).Include(o => o.Patient).OrderByDescending(o => o.OrderedAt).ThenByDescending(o => o.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<LabOrderDto>(rows.Select(o => PatientRecordService.ToDto(o, Ref(o.Patient))).ToList(), p, size, total);
    }

    public async Task<LabOrderDto> LabAsync(int id, CancellationToken ct)
    {
        var o = await LabScope().Include(x => x.Items).Include(x => x.Doctor).Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("order_not_found", "الطلب غير موجود.");
        return PatientRecordService.ToDto(o, Ref(o.Patient));
    }

    public async Task<PagedResult<ImagingOrderDto>> ImagingAsync(ImagingOrderStatus? status, int? patientId, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = ImagingScope();
        if (status is { } s) q = q.Where(o => o.Status == s);
        if (patientId is { } pid) q = q.Where(o => o.PatientId == pid);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(o => o.Report).Include(o => o.Doctor).Include(o => o.Patient).OrderByDescending(o => o.OrderedAt).ThenByDescending(o => o.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        var drafts = SeesDrafts;
        return new PagedResult<ImagingOrderDto>(rows.Select(o => PatientRecordService.ToDto(o, Ref(o.Patient), drafts)).ToList(), p, size, total);
    }

    public async Task<ImagingOrderDto> ImagingAsync(int id, CancellationToken ct)
    {
        var o = await ImagingScope().Include(x => x.Report).Include(x => x.Doctor).Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("order_not_found", "الطلب غير موجود.");
        return PatientRecordService.ToDto(o, Ref(o.Patient), SeesDrafts);
    }

    private bool SeesDrafts => me.Role is UserRole.Radiology or UserRole.Admin or UserRole.Hospital;

    private static PersonRefDto Ref(User u) => new(u.Id, u.FullName, u.AvatarKey);
}
